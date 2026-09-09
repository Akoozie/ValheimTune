using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace ValheimTune.Patches
{
    [HarmonyPatch]
    public static class DirtyPatches
    {
        // Split so the watchdog can tell a fully-dead hook (both zero) from a half-dead one (one
        // setter got inlined away on this Mono build but the other didn't).
        public static long MarksData;
        public static long MarksOwner;
        public static long Marks => MarksData + MarksOwner;

        // Vanilla's area test moved from ZNetScene.InActiveArea(sector, zone, radius) to the ring
        // loops in ZDOMan.FindSectorObjects. Those visit a zone only when it is inside the
        // Chebyshev ring *and* passes ZonesWithinRadius, except in classic mode where the ring
        // alone decides. These two mirror that exactly, for one candidate zone instead of a sweep.
        public static int ZoneChebyshev(int ax, int ay, int bx, int by)
        {
            int dx = ax - bx; if (dx < 0) dx = -dx;
            int dy = ay - by; if (dy < 0) dy = -dy;
            return dx > dy ? dx : dy;
        }

        internal static bool InNear(Vector2s centre, Vector2s candidate, SimulationDistance sd)
        {
            if (ZoneChebyshev(centre.x, centre.y, candidate.x, candidate.y) > sd.NearSimulationDistance) return false;
            return sd.IsClassic
                || ZoneSystem.instance.ZonesWithinRadius(centre, candidate, sd.NearSimulationDistance);
        }

        internal static bool InDistant(Vector2s centre, Vector2s candidate, SimulationDistance sd)
        {
            if (ZoneChebyshev(centre.x, centre.y, candidate.x, candidate.y) > sd.TotalSimulationDistance) return false;
            return sd.IsClassic
                || ZoneSystem.instance.ZonesWithinRadius(centre, candidate, sd.TotalSimulationDistance, ghostZone: true);
        }
        public static int FullScans;
        public static int DirtyRounds;
        public static int Deferred;
        public static int LastDrained;
        public static bool Disabled;   // set by the watchdog

        // Watchdog's own counter: incremented alongside MarksData but never touched by
        // ResetCounters(), so Plugin's watchdog window doesn't race the log-cadence reset.
        public static long WatchdogMarks;
        // ZDOs deserialized from the network in the watchdog window (bumped by MeasurePatches.DeserializePostfix).
        // Every such ZDO also goes through the DataRevision setter in RPC_ZDOData, so "recv > 0 and marks == 0"
        // over the SAME window is a dead hook, not a lagging game counter (the old ZDOMan.GetRecvZDOs()
        // check lagged a second and false-tripped when the last player logged out).
        public static long WatchdogRecv;

        // ZDOPeer instances are created in ZDOMan.AddPeer and dropped in RemovePeer; a weak table
        // means we never have to hook either.
        private static readonly ConditionalWeakTable<ZDOMan.ZDOPeer, DirtyPeerState<ZDOID>> s_state =
            new ConditionalWeakTable<ZDOMan.ZDOPeer, DirtyPeerState<ZDOID>>();

        public static DirtyPeerState<ZDOID> StateFor(ZDOMan.ZDOPeer peer) =>
            s_state.GetValue(peer, _ => new DirtyPeerState<ZDOID>());

        private static bool Active => Compat.ReplacementsAllowed && Cfg.DirtySets.Value && !Disabled && ZNet.instance != null && ZNet.instance.IsServer();

        // Every revision change in the game goes through one of these two auto-property setters:
        // local Set* calls via IncreaseDataRevision (ZDO.cs:518), network receives via the direct
        // assignment in RPC_ZDOData (ZDOMan.cs:843), ownership via IncreaseOwnerRevision.
        [HarmonyPatch(typeof(ZDO), nameof(ZDO.DataRevision), MethodType.Setter)]
        [HarmonyPostfix]
        private static void DataRevisionSet(ZDO __instance) => Mark(__instance.m_uid, isOwner: false);

        [HarmonyPatch(typeof(ZDO), nameof(ZDO.OwnerRevision), MethodType.Setter)]
        [HarmonyPostfix]
        private static void OwnerRevisionSet(ZDO __instance) => Mark(__instance.m_uid, isOwner: true);

        private static void Mark(ZDOID id, bool isOwner)
        {
            if (id == ZDOID.None) return;
            if (isOwner) MarksOwner++; else { MarksData++; WatchdogMarks++; }
            if (!Active) return;
            var man = ZDOMan.instance;
            if (man == null) return;
            var peers = man.m_peers;
            for (int i = 0; i < peers.Count; i++)
                StateFor(peers[i]).Pending.Add(id);
        }

        public static void ResetCounters()
        {
            MarksData = 0;
            MarksOwner = 0;
            FullScans = 0;
            DirtyRounds = 0;
            Deferred = 0;
            LastDrained = 0;
        }

        // Pure so it's unit-testable without a live Unity/ZDOMan instance.
        public static bool WatchdogShouldTrip(bool dirtySetsOn, bool disabled, bool recvSeen, long marksInWindow) =>
            dirtySetsOn && !disabled && recvSeen && marksInWindow == 0;

        // Marks while disabled prove the hook is alive: re-arm. The next active round forces a full scan
        // per peer (DirtyPeerState.NeedsFullScan sees the inactive->active edge), so nothing is missed.
        public static bool WatchdogShouldRearm(bool disabled, long marksInWindow) =>
            disabled && marksInWindow > 0;

        private static readonly List<ZDOID> s_ids = new List<ZDOID>(256);
        private static int s_relayMinMs;

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.CreateSyncList))]
        [HarmonyPrefix]
        private static bool CreateSyncListPrefix(ZDOMan __instance, ZDOMan.ZDOPeer peer, List<ZDO> toSync, out bool __state)
        {
            __state = false;                       // true = this round was a full scan; postfix refills
            // One GetZDO lookup per id per call instead of one per Drain lambda (up to four). Local,
            // not a static field: reset every call anyway, and a bare static ZDOID field would force
            // DirtyPatches to eagerly resolve the game assembly on class load (breaks pure-logic unit
            // tests that touch this class without the game assembly on the test runner's path).
            ZDOID lastId = ZDOID.None;
            ZDO lastZdo = null;
            ZDO Z(ZDOID id)
            {
                if (id != lastId) { lastZdo = __instance.GetZDO(id); lastId = id; }
                return lastZdo;
            }
            bool active = Active;
            Vector3 refPos = peer.m_peer.GetRefPos();
            Vector2s zone = ZoneSystem.GetZone(refPos);
            var st = StateFor(peer);
            // Called every round regardless of Active so a peer's state always knows whether the
            // previous round was active; that is what forces a full scan on reactivation below.
            bool needsFull = st.NeedsFullScan((zone.x, zone.y), Time.time, Cfg.ReconcileSeconds.Value, active);
            if (!active) return true;

            if (needsFull)
            {
                FullScans++;
                __state = true;
                return true;                       // vanilla path, postfix captures its candidates
            }

            DirtyRounds++;
            // 1.0 removed ZoneSystem.m_activeArea/m_activeDistantArea; the radius is now a
            // per-peer SimulationDistance negotiated in ZNet.RPC_RequestValidSimulationDistance.
            SimulationDistance sd = peer.m_peer.m_simulationDistance;
            s_relayMinMs = Cfg.RelayMinIntervalMs.Value;
            s_ids.Clear();
            st.Drain(s_ids,
                exists: id => Z(id) != null,
                inArea: id =>
                {
                    ZDO z = Z(id);
                    Vector2s s = z.GetSector();
                    return InNear(zone, s, sd) || (z.Distant && InDistant(zone, s, sd));
                },
                shouldSend: id => peer.ShouldSend(Z(id)),
                deferSend: id => RelayThrottled(peer, Z(id)));
            LastDrained = s_ids.Count;

            // Vanilla only ships distant objects when there are fewer than 10 near ones; keep that.
            // Vanilla also sorts the near list only and appends distant objects unsorted; we run
            // both near and distant through the single ServerSortSendZDOS call below (deliberate,
            // harmless - it just means distant objects get distance-sorted too instead of raw order).
            int nearCount = 0;
            for (int i = 0; i < s_ids.Count; i++)
            {
                ZDO z = __instance.GetZDO(s_ids[i]);
                if (InNear(zone, z.GetSector(), sd)) { toSync.Add(z); nearCount++; }
            }
            if (nearCount < 10)
                for (int i = 0; i < s_ids.Count; i++)
                {
                    ZDO z = __instance.GetZDO(s_ids[i]);
                    if (!InNear(zone, z.GetSector(), sd)) toSync.Add(z);
                }

            __instance.ServerSortSendZDOS(toSync, refPos, peer);
            __instance.AddForceSendZdos(peer, toSync);
            return false;
        }

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.CreateSyncList))]
        [HarmonyPostfix]
        private static void CreateSyncListPostfix(ZDOMan.ZDOPeer peer, List<ZDO> toSync, bool __state)
        {
            if (!__state) return;
            var st = StateFor(peer);
            for (int i = 0; i < toSync.Count; i++) st.Pending.Add(toSync[i].m_uid);
        }

        // R2 relay throttle: a non-prioritized object (fish, drifting items, pieces) that was already
        // sent to this peer within RelayMinIntervalMs is skipped this round, not pruned - it ships on
        // a later round once the interval elapses. Owner's own updates and Prioritized objects are exempt.
        private static bool RelayThrottled(ZDOMan.ZDOPeer peer, ZDO z)
        {
            int minMs = s_relayMinMs;
            if (minMs <= 0) return false;
            if (z.Type == ZDO.ObjectType.Prioritized) return false;
            if (z.GetOwner() == peer.m_peer.m_uid) return false;
            if (!peer.m_zdos.TryGetValue(z.m_uid, out var info)) return false;
            bool throttled = (Time.time - info.m_syncTime) * 1000f < minMs;
            if (throttled) Deferred++;
            return throttled;
        }
    }
}

using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ValheimTune.Patches
{
    [HarmonyPatch]
    public static class SavePatches
    {
        public static long SaveMarks, LinkFixes;
        private static bool s_inFix;
        private static bool s_fixErrorLogged;
        private static readonly List<ZDOID> s_deadKeys = new List<ZDOID>();

        // S1: RPC_ZDOData sets revisions directly and calls Deserialize, never SetDirtySector
        // (ZDOMan.cs:1172-1181), so player-only changes never mark the chunk for the incremental save.
        [HarmonyPatch(typeof(ZDO), nameof(ZDO.Deserialize))]
        [HarmonyPostfix]
        private static void MarkDirtyOnReceive(ZDO __instance)
        {
            if (!Compat.ReplacementsAllowed || !Cfg.SaveDirtyFix.Value) return;
            var znet = ZNet.instance; var man = ZDOMan.instance;
            if (znet == null || man == null || !znet.IsServer() || !__instance.Persistent) return;
            if (Game.instance != null && Game.instance.PortalPrefabHash.Contains(__instance.GetPrefab())) man.SetDirtyPortals();
            else man.SetDirtySector(__instance);
            SaveMarks++;
        }

        // S2: connection hashes are regenerated each save, so a spawner/creature link split across a
        // written and an unwritten chunk breaks on restart. Runs from PrepareSave before BeginSave, where
        // SetDirtySector still feeds the set this save reads. Re-runs until no link is split.
        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.GetSaveClonePerChunk))]
        [HarmonyPostfix]
        private static void SaveLinkedChunks(ZDOMan __instance, ref List<Tuple<ZoneSystem.ChunkIndex, List<ZDO>>> __result)
        {
            if (s_inFix || !Compat.ReplacementsAllowed || !Cfg.SpawnerLinkFix.Value) return;
            // A save started before the previous one was finalised (shutdown or admin `save` during an
            // autosave) routes SetDirtySector into m_dirtyChunks[1], which this save never reads: the
            // closure could not converge. Such a save runs vanilla.
            if (__instance.m_currentSaveState != ZDOMan.SaveState.NotSaving) return;
            s_inFix = true;
            var vanilla = __result;
            try
            {
                int added = 0;
                for (int pass = 0; pass < 8; pass++)
                {
                    var written = new HashSet<ZDOID>();
                    foreach (var t in __result) foreach (var z in t.Item2) written.Add(z.m_uid);
                    int marked = 0;
                    foreach (var kv in ZDOExtraData.s_connections)
                    {
                        var c = kv.Value;
                        if (c.m_type == ZDOExtraData.ConnectionType.None || c.m_type == ZDOExtraData.ConnectionType.Portal) continue;
                        if (c.m_target == ZDOID.None) continue;
                        ZDO a = __instance.GetZDO(kv.Key), b = __instance.GetZDO(c.m_target);
                        if (a == null || b == null || !a.Persistent || !b.Persistent) continue;
                        bool wa = written.Contains(a.m_uid), wb = written.Contains(b.m_uid);
                        if (wa == wb) continue;
                        __instance.SetDirtySector(wa ? b : a); marked++;
                    }
                    if (marked == 0) break;
                    added += marked;
                    __result = __instance.GetSaveClonePerChunk();   // re-enters this postfix, which returns at once (s_inFix)
                }
                if (added > 0) Plugin.Log.LogInfo($"[ValheimTune] save: {added} split spawner link(s): other side marked for this save");
                LinkFixes += added;
            }
            catch (Exception e)
            {
                __result = vanilla;
                if (!s_fixErrorLogged) { s_fixErrorLogged = true; Plugin.Log.LogError($"[ValheimTune] spawner link fix failed, saving as vanilla: {e}"); }
            }
            finally { s_inFix = false; }
        }

        // G1: m_deadZDOs only stops a late client update resurrecting a destroyed object (ZDOMan.cs:1182);
        // vanilla never prunes it. Entries are ZNet time ticks at destroy.
        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.PrepareSave))]
        [HarmonyPostfix]
        private static void PruneDead(ZDOMan __instance)
        {
            if (!Compat.ReplacementsAllowed || !Cfg.DeadZdoPrune.Value || ZNet.instance == null) return;
            long cutoff = ZNet.instance.GetTime().Ticks - TimeSpan.FromHours(1).Ticks;
            s_deadKeys.Clear();
            foreach (var kv in __instance.m_deadZDOs) if (kv.Value < cutoff) s_deadKeys.Add(kv.Key);
            foreach (var k in s_deadKeys) __instance.m_deadZDOs.Remove(k);
            s_deadKeys.Clear();
        }
    }
}

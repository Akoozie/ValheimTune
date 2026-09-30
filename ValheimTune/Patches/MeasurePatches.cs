using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ValheimTune.Patches
{
    // Times the two server-side sync functions and captures Z (candidates per peer).
    // CreateSyncList and SendZDOs are private; both are publicized at build time.
    [HarmonyPatch]
    public static class MeasurePatches
    {
        public static readonly RollingStats FrameMs = new RollingStats();
        public static readonly RollingStats SyncListMs = new RollingStats();
        public static readonly RollingStats SendMs = new RollingStats();
        public static readonly ChurnTally Recv = new ChurnTally();
        public static int MaxZ;
        public static int Rounds;

        private static readonly Stopwatch s_sync = new Stopwatch();
        private static readonly Stopwatch s_send = new Stopwatch();
        private static readonly Dictionary<ZDOID, (int count, int prefab, Vector3 pos)> s_hot = new Dictionary<ZDOID, (int, int, Vector3)>();

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.CreateSyncList))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void SyncListPrefix() => s_sync.Restart();

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.CreateSyncList))]
        [HarmonyPostfix]
        private static void SyncListPostfix(ZDOMan __instance)
        {
            SyncListMs.Add(s_sync.Elapsed.TotalMilliseconds);
            int z = __instance.m_tempSectorObjects.Count;
            if (z > MaxZ) MaxZ = z;
        }

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.SendZDOs))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void SendPrefix() => s_send.Restart();

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.SendZDOs))]
        [HarmonyPostfix]
        private static void SendPostfix()
        {
            SendMs.Add(s_send.Elapsed.TotalMilliseconds);
            Rounds++;
        }

        [HarmonyPatch(typeof(ZDO), nameof(ZDO.Deserialize))]
        [HarmonyPostfix]
        private static void DeserializePostfix(ZDO __instance)
        {
            Recv.Add(__instance.GetPrefab());
            DirtyPatches.WatchdogRecv++;
            ZDOID id = __instance.m_uid;
            s_hot.TryGetValue(id, out var h);
            s_hot[id] = (h.count + 1, __instance.GetPrefab(), __instance.GetPosition());
        }

        // Max ms of the two rare main-thread spikes in the log window.
        public static double RemovePeerMaxMs, ReleaseMaxMs;
        private static readonly Stopwatch s_remove = new Stopwatch();
        private static readonly Stopwatch s_release = new Stopwatch();
        private static int s_gc0 = System.GC.CollectionCount(0);
        public static int GcDelta => System.GC.CollectionCount(0) - s_gc0;
        public static long HeapMb => System.GC.GetTotalMemory(false) / 1048576;

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.RemovePeer))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void RemovePeerPrefix() => s_remove.Restart();

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.RemovePeer))]
        [HarmonyPostfix]
        private static void RemovePeerPostfix() => RemovePeerMaxMs = System.Math.Max(RemovePeerMaxMs, s_remove.Elapsed.TotalMilliseconds);

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.ReleaseZDOS))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void ReleasePrefix() => s_release.Restart();

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.ReleaseZDOS))]
        [HarmonyPostfix]
        private static void ReleasePostfix() => ReleaseMaxMs = System.Math.Max(ReleaseMaxMs, s_release.Elapsed.TotalMilliseconds);

        public static List<KeyValuePair<ZDOID, (int count, int prefab, Vector3 pos)>> HotTop(int n)
        {
            return s_hot.OrderByDescending(x => x.Value.count).Take(n).ToList();
        }

        public static void ResetAll()
        {
            FrameMs.Reset(); SyncListMs.Reset(); SendMs.Reset(); MaxZ = 0; Rounds = 0; Recv.Reset(); s_hot.Clear();
            RemovePeerMaxMs = 0; ReleaseMaxMs = 0; s_gc0 = System.GC.CollectionCount(0);
            SavePatches.SaveMarks = 0; SavePatches.LinkFixes = 0; NetPatches.KeyDedupes = 0;
        }
    }
}

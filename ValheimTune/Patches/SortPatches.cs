using System;
using HarmonyLib;
using UnityEngine;

namespace ValheimTune.Patches
{
    [HarmonyPatch]
    public static class SortPatches
    {
        private static bool Active => Compat.ReplacementsAllowed && Cfg.TopKSort.Value && ZNet.instance != null && ZNet.instance.IsServer();

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.ServerSortSendZDOS))]
        [HarmonyPrefix]
        private static bool Prefix(System.Collections.Generic.List<ZDO> objects, Vector3 refPos, ZDOMan.ZDOPeer peer)
        {
            if (!Active) return true;
            float time = Time.time;
            long receiver = peer.m_peer.m_uid;
            var zdos = peer.m_zdos;
            int k = Cfg.TopK.Value > 0 ? Cfg.TopK.Value : Math.Max(64, Cfg.SendWindowBytes.Value / 64);
            // Deliberately does not write ZDO.m_tempSortValue. Nothing else on the server reads it
            // except ZDO.SaveClone (src_server/ZDO.cs:172-186), where a negative value makes Reset()
            // skip releasing the field tables. Vanilla's sort leaves it negative for nearby stale
            // objects - that is the leak in OPTIMIZATION.md item 13. Not writing it closes that leak.
            TopK.Select(objects, z =>
            {
                // Same order as vanilla ServerSendCompare (ZDOMan.cs:956-980), folded into one key:
                //   bucket 0: Prioritized AND owned by someone other than the receiver
                //   then by Type descending: Terrain, Solid, Prioritized, Default  (buckets 1..4)
                //   within a bucket: distance - 1.5 * clamp(time since last sync to this peer, 0, 100); never synced = 100
                bool flag = z.Type == ZDO.ObjectType.Prioritized && z.HasOwner() && z.GetOwner() != receiver;
                int bucket = flag ? 0 : 1 + (3 - (int)z.Type);
                float stale = 100f;
                if (zdos.TryGetValue(z.m_uid, out var info)) stale = Mathf.Clamp(time - info.m_syncTime, 0f, 100f);
                double sortValue = Vector3.Distance(z.GetPosition(), refPos) - stale * 1.5f;
                return bucket * 100000.0 + sortValue;
            }, k);
            return false;
        }
    }
}

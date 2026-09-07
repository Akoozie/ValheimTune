using System.Collections.Generic;
using UnityEngine;

namespace ValheimTune
{
    // Items and logs thrown into water never settle: their rigidbody never sleeps, so their ZDO is
    // re-sent every client send round forever. This finds them (item-drop and tree-log prefabs, not fish,
    // sitting at/below the water line over underwater terrain) and optionally deletes them
    // the same way vanilla deletes invalid ZDOs on the server: take ownership, DestroyZDO.
    public static class FloatingDrops
    {
        private static readonly Dictionary<int, bool> s_isCandidate = new Dictionary<int, bool>();

        private static bool IsFloatingCandidate(int hash)
        {
            if (s_isCandidate.TryGetValue(hash, out bool v)) return v;
            GameObject go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(hash) : null;
            v = go != null && (go.GetComponent<ItemDrop>() != null || go.GetComponent<TreeLog>() != null) && go.GetComponent<Fish>() == null;
            s_isCandidate[hash] = v;
            return v;
        }

        // Returns (found, deleted) and logs a per-prefab summary. Pure scan when delete == false.
        public static (int found, int deleted) Run(bool delete, BepInEx.Logging.ManualLogSource log)
        {
            var man = ZDOMan.instance;
            if (man == null || ZNetScene.instance == null || ZoneSystem.instance == null || WorldGenerator.instance == null)
            {
                log.LogWarning("[ValheimTune] floating drops: world not ready");
                return (0, 0);
            }
            float water = ZoneSystem.instance.m_waterLevel;
            var hits = new List<ZDOID>();
            var tally = new ChurnTally();
            var sample = new List<(string name, Vector3 pos)>();
            foreach (var kv in man.m_objectsByID)
            {
                ZDO z = kv.Value;
                if (!IsFloatingCandidate(z.GetPrefab())) continue;
                Vector3 p = z.GetPosition();
                if (p.y > water + 0.25f) continue;                                // above the water line: on land (or resting on a dock)
                if (WorldGenerator.instance.GetHeight(p.x, p.z) >= water) continue; // ground under it is dry: on a beach
                hits.Add(kv.Key);
                tally.Add(z.GetPrefab());
                if (sample.Count < 10)
                {
                    GameObject go = ZNetScene.instance.GetPrefab(z.GetPrefab());
                    sample.Add((go != null ? go.name : z.GetPrefab().ToString(), p));
                }
            }
            var sb = new System.Text.StringBuilder();
            sb.Append("[ValheimTune] floating drops: ").Append(hits.Count).Append(delete ? " found, deleting:" : " found (dry run):");
            foreach (var t in tally.Top(10))
            {
                GameObject go = ZNetScene.instance.GetPrefab(t.Key);
                sb.Append(' ').Append(go != null ? go.name : t.Key.ToString()).Append('=').Append(t.Value);
            }
            log.LogInfo(sb.ToString());
            if (sample.Count > 0)
            {
                var sampleSb = new System.Text.StringBuilder("[ValheimTune] floating drops sample:");
                foreach (var s in sample)
                    sampleSb.Append(' ').Append(s.name).Append('@').Append('(').Append((int)s.pos.x).Append(',').Append((int)s.pos.y).Append(',').Append((int)s.pos.z).Append(')');
                log.LogInfo(sampleSb.ToString());
            }
            int deleted = 0;
            if (delete)
            {
                long me = ZDOMan.GetSessionID();
                foreach (var id in hits)
                {
                    ZDO z = man.GetZDO(id);
                    if (z == null) continue;
                    z.SetOwner(me);
                    man.DestroyZDO(z);
                    deleted++;
                }
                log.LogInfo($"[ValheimTune] floating drops: {deleted} queued for destruction");
            }
            return (hits.Count, deleted);
        }
    }
}

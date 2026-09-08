using HarmonyLib;
using UnityEngine;

namespace ValheimTune.Patches
{
    // G1: hold the hourly Resources.UnloadUnusedAssets() until the server is empty.
    //
    // Both vanilla entry points (CollectResourcesCheckPeriodic, hourly; CollectResourcesCheck,
    // 20 min) route through Game.CollectResources, so one prefix covers them. When we defer we
    // deliberately do NOT let m_lastCollectResources advance - vanilla only re-checks once an
    // hour, so Plugin.Update re-triggers the collection itself the moment the last player leaves.
    [HarmonyPatch(typeof(Game), nameof(Game.CollectResources))]
    public static class AssetUnloadPatch
    {
        private static float s_deferredSince = -1f;   // Time.realtimeSinceStartup, -1 = nothing held
        public static bool Pending => s_deferredSince >= 0f;

        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (!Compat.ReplacementsAllowed || !Cfg.DeferAssetUnload.Value) return true;
            if (ZNet.instance == null || !ZNet.instance.IsDedicated()) return true;

            int peers = ZNet.instance.GetPeerConnections();
            float now = Time.realtimeSinceStartup;
            double heldFor = s_deferredSince < 0f ? 0.0 : now - s_deferredSince;
            double maxDefer = Cfg.AssetUnloadMaxDeferMinutes.Value * 60.0;

            if (AssetUnload.Decide(peers, heldFor, maxDefer) == AssetUnload.Decision.Defer)
            {
                if (s_deferredSince < 0f)
                {
                    s_deferredSince = now;
                    Plugin.Log.LogInfo($"[ValheimTune] asset unload deferred: {peers} player(s) online, will run when the server empties (backstop {Cfg.AssetUnloadMaxDeferMinutes.Value} min)");
                }
                return false;
            }

            if (s_deferredSince >= 0f)
            {
                Plugin.Log.LogInfo($"[ValheimTune] asset unload running after {heldFor:F0} s deferred ({peers} player(s) online)");
                s_deferredSince = -1f;
            }
            return true;
        }

        // Called from Plugin.Update: vanilla would not retry for another hour, so we retry
        // ourselves once the condition that made us defer has cleared.
        public static void RunIfDue()
        {
            if (!Pending || !Cfg.DeferAssetUnload.Value) return;
            var znet = ZNet.instance;
            if (znet == null) return;
            double heldFor = Time.realtimeSinceStartup - s_deferredSince;
            if (AssetUnload.Decide(znet.GetPeerConnections(), heldFor, Cfg.AssetUnloadMaxDeferMinutes.Value * 60.0)
                != AssetUnload.Decision.Run) return;
            Game.instance?.CollectResources();     // prefix above now allows it and clears the flag
        }
    }
}

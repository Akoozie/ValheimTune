using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HarmonyLib;
using Steamworks;

namespace ValheimTune.Patches
{
    [HarmonyPatch]
    public static class ConstPatches
    {
        // ZDOMan.SendZDOs has 10240 twice and 2048 once as inline constants (ZDOMan.cs:727,731,732).
        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.SendZDOs))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SendZDOsWindow(IEnumerable<CodeInstruction> code)
        {
            var original = new List<CodeInstruction>(code);
            bool on = Cfg.OverrideSendWindow.Value;
            if (!ConstSwap.ShouldOverride(on, Cfg.SendWindowBytes.Value, 10240) && !ConstSwap.ShouldOverride(on, Cfg.MinHeadroomBytes.Value, 2048))
            {
                Plugin.Log.LogInfo($"[ValheimTune] SendZDOs window left vanilla ({(on ? "values are vanilla" : "OverrideSendWindow = false")})");
                return original;
            }
            var map = new Dictionary<int, int>
            {
                { 10240, Cfg.SendWindowBytes.Value },
                { 2048, Cfg.MinHeadroomBytes.Value },
            };
            var result = new List<CodeInstruction>(ConstSwap.Replace(original, map));
            if (ConstSwap.LastReplaced != 3)
            {
                Plugin.Log.LogError($"[ValheimTune] SendZDOs: {ConstSwap.LastReplaced} constants replaced, expected 3; leaving the method untouched");
                return original;
            }
            Plugin.Log.LogInfo($"[ValheimTune] SendZDOs window {Cfg.SendWindowBytes.Value}/{Cfg.MinHeadroomBytes.Value}, {ConstSwap.LastReplaced} constants replaced (expected 3)");
            return result;
        }

        // Vanilla: one peer per frame, then a 0.05 s pause (ZDOMan.cs:559). This serves all peers per round.
        private static bool s_allPeersDisabledLogged;

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.SendZDOToPeers2))]
        [HarmonyPrefix]
        private static bool AllPeersPerRound(ZDOMan __instance, float dt)
        {
            if (!Compat.ReplacementsAllowed || !Cfg.AllPeersPerRound.Value || DirtyPatches.Disabled)
            {
                if (DirtyPatches.Disabled && !s_allPeersDisabledLogged)
                {
                    s_allPeersDisabledLogged = true;
                    Plugin.Log.LogWarning("[ValheimTune] AllPeersPerRound skipped: DirtySets watchdog disabled dirty sets, falling back to vanilla one-peer-per-frame to avoid stacking full scans in one frame.");
                }
                return true;
            }
            if (__instance.m_peers.Count == 0) return false;
            __instance.m_sendTimer += dt;
            if (__instance.m_sendTimer < Cfg.RoundSeconds.Value) return false;
            __instance.m_sendTimer = 0f;
            for (int i = 0; i < __instance.m_peers.Count; i++)
            {
                try { __instance.SendZDOs(__instance.m_peers[i], flush: false); }
                catch (Exception e) { Plugin.Log.LogError($"[ValheimTune] SendZDOs threw for peer {i}: {e}"); }
            }
            return false;
        }

        // Vanilla pins SendRateMin = SendRateMax = 153600 globally (ZSteamSocket.cs:79-83).
        // Re-apply with our values right after the game sets its own. Each key is only
        // written when it differs from vanilla, so a key we don't change stays whatever
        // another networking mod set it to.
        private const int VanillaSendRate = 153600;
        private static bool s_rateLogged;

        [HarmonyPatch(typeof(ZSteamSocket), nameof(ZSteamSocket.RegisterGlobalCallbacks))]
        [HarmonyPostfix]
        private static void SendRate()
        {
            bool on = Cfg.OverrideSendRate.Value;
            bool min = ConstSwap.ShouldOverride(on, Cfg.SendRateMin.Value, VanillaSendRate);
            bool max = ConstSwap.ShouldOverride(on, Cfg.SendRateMax.Value, VanillaSendRate);
            if (min) SetGlobalInt(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMin, Cfg.SendRateMin.Value);
            if (max) SetGlobalInt(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMax, Cfg.SendRateMax.Value);
            if (!s_rateLogged)
            {
                s_rateLogged = true;
                Plugin.Log.LogInfo(min || max
                    ? $"[ValheimTune] Steam send rate min {(min ? Cfg.SendRateMin.Value.ToString() : "untouched")} max {(max ? Cfg.SendRateMax.Value.ToString() : "untouched")} B/s"
                    : $"[ValheimTune] Steam send rate left untouched ({(on ? "values are vanilla" : "OverrideSendRate = false")})");
            }
        }

        private static void SetGlobalInt(ESteamNetworkingConfigValue key, int value)
        {
            GCHandle h = GCHandle.Alloc(value, GCHandleType.Pinned);
            try
            {
                bool ok = SteamGameServerNetworkingUtils.SetConfigValue(key,
                    ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global, IntPtr.Zero,
                    ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32, h.AddrOfPinnedObject());
                if (!ok) Plugin.Log.LogError($"[ValheimTune] Steam rejected {key} = {value}");
            }
            finally { h.Free(); }
        }
    }
}

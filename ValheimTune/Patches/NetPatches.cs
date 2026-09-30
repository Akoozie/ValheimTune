using System.Collections.Generic;
using HarmonyLib;

namespace ValheimTune.Patches
{
    [HarmonyPatch]
    public static class NetPatches
    {
        public static long KeyDedupes;

        // N1: vanilla sleeps 100 ms on the main thread in Close (ZSteamSocket.cs:168-189) so queued
        // data can flush, and closes without linger. Linger lets Steam flush it instead.
        [HarmonyPatch(typeof(ZSteamSocket), nameof(ZSteamSocket.Close))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> CloseNoSleep(IEnumerable<CodeInstruction> code)
        {
            var original = new List<CodeInstruction>(code);
            if (!Compat.ReplacementsAllowed || !Cfg.DisconnectNoSleep.Value) return original;
            if (!CloseSwap.TryApply(original, out var result))
            {
                Plugin.Log.LogError("[ValheimTune] ZSteamSocket.Close: expected one Thread.Sleep and one CloseConnection(..., false) not found; leaving Close untouched");
                return original;
            }
            Plugin.Log.LogInfo("[ValheimTune] ZSteamSocket.Close: 100 ms sleep removed, linger on");
            return result;
        }

        // G2: vanilla stores keys lowercased but compares the raw name, so a mixed-case key is
        // re-set and re-broadcast every time (ZoneSystem.cs:736-765, 3254-3261).
        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.RPC_SetGlobalKey))]
        [HarmonyPrefix]
        private static bool SetGlobalKeyDedupe(ZoneSystem __instance, string name)
        {
            if (!Compat.ReplacementsAllowed || !Cfg.GlobalKeyDedupe.Value || name == null) return true;
            string key = ZoneSystem.GetKeyValue(name.ToLower(), out string value, out _);
            if (__instance.m_globalKeysValues.TryGetValue(key, out string cur) && cur == value) { KeyDedupes++; return false; }
            return true;
        }
    }
}

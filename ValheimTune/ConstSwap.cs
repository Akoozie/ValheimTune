using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace ValheimTune
{
    // Swaps inlined int constants in a method body. Used where the game hard-codes a
    // number in the middle of a method (10240, 2048 in ZDOMan.SendZDOs).
    public static class ConstSwap
    {
        public static int LastReplaced;

        // Network overrides only engage when switched on AND asked to change something.
        // At vanilla values we leave the game alone, so another networking mod's value stands.
        public static bool ShouldOverride(bool enabled, int value, int vanilla) => enabled && value != vanilla;

        public static IEnumerable<CodeInstruction> Replace(IEnumerable<CodeInstruction> code, IReadOnlyDictionary<int, int> map)
        {
            LastReplaced = 0;
            foreach (var ci in code)
            {
                if (ci.opcode == OpCodes.Ldc_I4 && ci.operand is int v && map.TryGetValue(v, out int nv))
                {
                    LastReplaced++;
                    yield return new CodeInstruction(OpCodes.Ldc_I4, nv) { labels = ci.labels, blocks = ci.blocks };
                    continue;
                }
                yield return ci;
            }
        }
    }
}

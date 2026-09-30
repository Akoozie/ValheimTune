using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ValheimTune
{
    // N1: rewrites ZSteamSocket.Close. Removes "ldc 100; call Thread.Sleep" and flips the
    // bEnableLinger argument (ldc.i4.0 right before CloseConnection) to true.
    public static class CloseSwap
    {
        // Pure matcher over (opcode, "Type.Method" for calls else "") pairs so it can be unit-tested.
        // Exactly one Sleep fed by a constant, exactly one CloseConnection preceded by ldc.i4.0.
        public static bool TryFind(IReadOnlyList<(OpCode op, string name)> ins, out int sleepCall, out int lingerArg)
        {
            sleepCall = -1; lingerArg = -1;
            int sleeps = 0, closes = 0;
            for (int i = 0; i < ins.Count; i++)
            {
                if (!IsCall(ins[i].op)) continue;
                string n = ins[i].name ?? "";
                if (n == "Thread.Sleep") { sleeps++; sleepCall = i; }
                else if (n.EndsWith(".CloseConnection") || n == "CloseConnection") { closes++; lingerArg = i - 1; }
            }
            if (sleeps != 1 || closes != 1) return false;
            if (sleepCall < 1 || (ins[sleepCall - 1].op != OpCodes.Ldc_I4_S && ins[sleepCall - 1].op != OpCodes.Ldc_I4)) return false;
            return lingerArg >= 0 && ins[lingerArg].op == OpCodes.Ldc_I4_0;
        }

        private static bool IsCall(OpCode op) => op == OpCodes.Call || op == OpCodes.Callvirt;

        public static bool TryApply(List<CodeInstruction> code, out List<CodeInstruction> result)
        {
            result = code;
            var pairs = new List<(OpCode, string)>(code.Count);
            foreach (var ci in code)
                pairs.Add((ci.opcode, ci.operand is MethodBase mb ? (mb.DeclaringType?.Name + "." + mb.Name) : ""));
            if (!TryFind(pairs, out int sleepCall, out int lingerArg)) return false;
            result = new List<CodeInstruction>(code);
            foreach (int i in new[] { sleepCall - 1, sleepCall })
                result[i] = new CodeInstruction(OpCodes.Nop) { labels = code[i].labels, blocks = code[i].blocks };
            result[lingerArg] = new CodeInstruction(OpCodes.Ldc_I4_1) { labels = code[lingerArg].labels, blocks = code[lingerArg].blocks };
            return true;
        }
    }
}

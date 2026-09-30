using System.Collections.Generic;
using System.Reflection.Emit;
using ValheimTune;
using Xunit;

public class CloseSwapTests
{
    private static readonly (OpCode, string) Sleep = (OpCodes.Call, "Thread.Sleep");
    private static readonly (OpCode, string) Close = (OpCodes.Call, "SteamGameServerNetworkingSockets.CloseConnection");
    private static (OpCode, string) Op(OpCode o) => (o, "");

    [Fact]
    public void HappyPath()
    {
        var ins = new List<(OpCode, string)> { Op(OpCodes.Ldarg_0), Op(OpCodes.Ldc_I4_S), Sleep, Op(OpCodes.Ldc_I4_0), Close, Op(OpCodes.Ret) };
        Assert.True(CloseSwap.TryFind(ins, out int sleep, out int linger));
        Assert.Equal(2, sleep);
        Assert.Equal(3, linger);
    }

    [Fact]
    public void NoSleepFails() =>
        Assert.False(CloseSwap.TryFind(new List<(OpCode, string)> { Op(OpCodes.Ldc_I4_0), Close }, out _, out _));

    [Fact]
    public void TwoSleepsFail() =>
        Assert.False(CloseSwap.TryFind(new List<(OpCode, string)> { Op(OpCodes.Ldc_I4_S), Sleep, Op(OpCodes.Ldc_I4_S), Sleep, Op(OpCodes.Ldc_I4_0), Close }, out _, out _));

    [Fact]
    public void CloseNotPrecededByLdcI4_0Fails() =>
        Assert.False(CloseSwap.TryFind(new List<(OpCode, string)> { Op(OpCodes.Ldc_I4_S), Sleep, Op(OpCodes.Ldc_I4_1), Close }, out _, out _));

    [Fact]
    public void ApplyNopsSleepAndFlipsLinger()
    {
        var code = new List<HarmonyLib.CodeInstruction>
        {
            new HarmonyLib.CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)100),
            new HarmonyLib.CodeInstruction(OpCodes.Call, typeof(System.Threading.Thread).GetMethod("Sleep", new[] { typeof(int) })),
            new HarmonyLib.CodeInstruction(OpCodes.Ldc_I4_0),
            new HarmonyLib.CodeInstruction(OpCodes.Call, typeof(Stub).GetMethod("CloseConnection")),
        };
        Assert.True(CloseSwap.TryApply(code, out var r));
        Assert.Equal(OpCodes.Nop, r[0].opcode);
        Assert.Equal(OpCodes.Nop, r[1].opcode);
        Assert.Equal(OpCodes.Ldc_I4_1, r[2].opcode);
        Assert.Equal(OpCodes.Ldc_I4_0, code[2].opcode);
    }

    public static class Stub { public static void CloseConnection(bool b) { } }
}

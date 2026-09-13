using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using ValheimTune;
using Xunit;

public class ConstSwapTests
{
    [Fact]
    public void ReplacesMappedLdcI4OperandsOnly()
    {
        var code = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldc_I4, 10240),
            new CodeInstruction(OpCodes.Ldc_I4, 2048),
            new CodeInstruction(OpCodes.Ldc_I4, 7),
            new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)10),
            new CodeInstruction(OpCodes.Ret),
        };
        var map = new Dictionary<int, int> { { 10240, 32768 }, { 2048, 4096 } };

        var result = ConstSwap.Replace(code, map).ToList();

        Assert.Equal(32768, (int)result[0].operand);
        Assert.Equal(4096, (int)result[1].operand);
        Assert.Equal(7, (int)result[2].operand);
        Assert.Equal(OpCodes.Ldc_I4_S, result[3].opcode);
        Assert.Equal((sbyte)10, (sbyte)result[3].operand);
        Assert.Equal(2, ConstSwap.LastReplaced);
    }

    // Issue #1: with another networking mod installed, off or vanilla must mean hands off.
    [Theory]
    [InlineData(true, 1048576, 153600, true)]
    [InlineData(true, 153600, 153600, false)]
    [InlineData(false, 1048576, 153600, false)]
    [InlineData(false, 153600, 153600, false)]
    public void OverrideOnlyWhenEnabledAndNotVanilla(bool enabled, int value, int vanilla, bool expected) =>
        Assert.Equal(expected, ConstSwap.ShouldOverride(enabled, value, vanilla));

    [Fact]
    public void PreservesLabelsAndBlocksOnReplacedInstruction()
    {
        var label = new Label();
        var block = new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock);
        var ci = new CodeInstruction(OpCodes.Ldc_I4, 10240);
        ci.labels.Add(label);
        ci.blocks.Add(block);
        var map = new Dictionary<int, int> { { 10240, 32768 } };

        var result = ConstSwap.Replace(new List<CodeInstruction> { ci }, map).ToList();

        Assert.Equal(32768, (int)result[0].operand);
        Assert.Same(ci.labels, result[0].labels);
        Assert.Same(ci.blocks, result[0].blocks);
        Assert.Contains(label, result[0].labels);
        Assert.Contains(block, result[0].blocks);
    }
}

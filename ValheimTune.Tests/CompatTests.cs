using ValheimTune;
using Xunit;

public class CompatTests
{
    [Fact]
    public void ExactMatch() => Assert.True(Compat.IsKnown("0.221.12", "0.221.12"));

    [Fact]
    public void MatchesTrimmedEntryInList() => Assert.True(Compat.IsKnown("0.221.12", " 0.221.11 , 0.221.12 "));

    [Fact]
    public void UnknownVersionIsFalse() => Assert.False(Compat.IsKnown("0.222.1", "0.221.12"));

    [Fact]
    public void EmptyListIsFalse() => Assert.False(Compat.IsKnown("0.221.12", ""));
}

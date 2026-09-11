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

    [Fact]
    public void ShippedDefaultCoversThePortedBuild() =>
        Assert.True(Compat.IsKnown("1.0.12", Compat.DefaultKnownGoodBuilds));

    [Fact]
    public void ShippedDefaultStillCoversThePreviousBuild() =>
        Assert.True(Compat.IsKnown("1.0.7", Compat.DefaultKnownGoodBuilds));

    // The upgrade case: a 0.7.0 config that predates 1.0.12 must still get patches.
    [Fact]
    public void StaleConfigStillGetsTheShippedBuild() =>
        Assert.True(Compat.IsKnownOrShipped("1.0.12", "1.0.7"));

    [Fact]
    public void ConfigCanStillAddAnUnshippedBuild() =>
        Assert.True(Compat.IsKnownOrShipped("1.0.13", "1.0.13"));

    [Fact]
    public void TrulyUnknownBuildIsStillRefused() =>
        Assert.False(Compat.IsKnownOrShipped("2.0.0", "1.0.7"));
}

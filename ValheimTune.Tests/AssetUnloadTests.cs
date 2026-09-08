using ValheimTune;
using Xunit;

public class AssetUnloadTests
{
    private const double Max = 240 * 60;   // 4 h backstop, the config default

    [Fact]
    public void EmptyServerCollectsImmediately() =>
        Assert.Equal(AssetUnload.Decision.Run, AssetUnload.Decide(0, 0, Max));

    [Fact]
    public void PlayersOnlineDefer() =>
        Assert.Equal(AssetUnload.Decision.Defer, AssetUnload.Decide(3, 0, Max));

    [Fact]
    public void StillDeferredWellInsideTheBackstop() =>
        Assert.Equal(AssetUnload.Decision.Defer, AssetUnload.Decide(1, Max - 1, Max));

    [Fact]
    public void BackstopForcesTheCollectionWithPlayersOnline() =>
        Assert.Equal(AssetUnload.Decision.Run, AssetUnload.Decide(6, Max, Max));

    [Fact]
    public void EmptyServerBeatsTheBackstopEitherWay() =>
        Assert.Equal(AssetUnload.Decision.Run, AssetUnload.Decide(0, Max + 1, Max));
}

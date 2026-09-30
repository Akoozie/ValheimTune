using Xunit;

public class SmokeTests
{
    [Fact]
    public void PluginVersionIsSet() => Assert.Equal("0.7.7", ValheimTune.Plugin.Version);
}

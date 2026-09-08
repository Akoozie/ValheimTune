using Xunit;

public class SmokeTests
{
    [Fact]
    public void PluginVersionIsSet() => Assert.Equal("0.6.0", ValheimTune.Plugin.Version);
}

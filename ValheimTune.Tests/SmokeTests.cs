using Xunit;

public class SmokeTests
{
    [Fact]
    public void PluginVersionIsSet() => Assert.Equal("0.7.4", ValheimTune.Plugin.Version);
}

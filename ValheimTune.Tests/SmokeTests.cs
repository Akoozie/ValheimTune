using Xunit;

public class SmokeTests
{
    [Fact]
    public void PluginVersionIsSet() => Assert.Equal("0.5.0", ValheimTune.Plugin.Version);
}

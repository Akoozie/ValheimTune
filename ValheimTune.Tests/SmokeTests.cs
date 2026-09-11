using Xunit;

public class SmokeTests
{
    [Fact]
    public void PluginVersionIsSet() => Assert.Equal("0.7.1", ValheimTune.Plugin.Version);
}

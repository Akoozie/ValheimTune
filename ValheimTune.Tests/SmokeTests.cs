using Xunit;

public class SmokeTests
{
    [Fact]
    public void PluginVersionIsSet() => Assert.Equal("0.4.2", ValheimTune.Plugin.Version);
}

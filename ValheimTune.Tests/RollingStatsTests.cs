using ValheimTune;
using Xunit;

public class RollingStatsTests
{
    [Fact]
    public void AvgMaxCountOverAddedValues()
    {
        var s = new RollingStats();
        s.Add(2); s.Add(4); s.Add(9);
        Assert.Equal(3, s.Count);
        Assert.Equal(5.0, s.Avg, 6);
        Assert.Equal(9.0, s.Max, 6);
    }

    [Fact]
    public void EmptyIsZeroNotNaN()
    {
        var s = new RollingStats();
        Assert.Equal(0, s.Count);
        Assert.Equal(0.0, s.Avg);
        Assert.Equal(0.0, s.Max);
    }

    [Fact]
    public void ResetClears()
    {
        var s = new RollingStats();
        s.Add(7);
        s.Reset();
        Assert.Equal(0, s.Count);
        Assert.Equal(0.0, s.Max);
    }
}

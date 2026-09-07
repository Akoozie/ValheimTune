using ValheimTune;
using Xunit;

public class ChurnTallyTests
{
    [Fact]
    public void CountsAndOrdersDescending()
    {
        var t = new ChurnTally();
        foreach (var k in new[] { 1, 2, 2, 3, 3, 3 }) t.Add(k);
        var top = t.Top(2);
        Assert.Equal(6, t.Total);
        Assert.Equal(3, top[0].Key); Assert.Equal(3, top[0].Value);
        Assert.Equal(2, top[1].Key); Assert.Equal(2, top[1].Value);
    }

    [Fact]
    public void ResetClears()
    {
        var t = new ChurnTally();
        t.Add(7);
        t.Reset();
        Assert.Equal(0, t.Total);
        Assert.Empty(t.Top(5));
    }
}

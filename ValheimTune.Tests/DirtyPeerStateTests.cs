using System.Collections.Generic;
using ValheimTune;
using Xunit;

public class DirtyPeerStateTests
{
    [Fact]
    public void FirstRoundIsAFullScan()
    {
        var s = new DirtyPeerState<int>();
        Assert.True(s.NeedsFullScan((0, 0), now: 100f, reconcileSeconds: 30f, active: true));
        Assert.False(s.NeedsFullScan((0, 0), now: 101f, reconcileSeconds: 30f, active: true));
        Assert.Equal(1, s.FullScans);
    }

    [Fact]
    public void ZoneChangeForcesFullScan()
    {
        var s = new DirtyPeerState<int>();
        s.NeedsFullScan((0, 0), 100f, 30f, true);
        Assert.True(s.NeedsFullScan((1, 0), 101f, 30f, true));
        Assert.False(s.NeedsFullScan((1, 0), 102f, 30f, true));
    }

    [Fact]
    public void ReconcileTimerForcesFullScan()
    {
        var s = new DirtyPeerState<int>();
        s.NeedsFullScan((0, 0), 100f, 30f, true);
        Assert.False(s.NeedsFullScan((0, 0), 129f, 30f, true));
        Assert.True(s.NeedsFullScan((0, 0), 131f, 30f, true));
    }

    [Fact]
    public void ReactivationForcesFullScan()
    {
        var s = new DirtyPeerState<int>();
        Assert.True(s.NeedsFullScan((0, 0), 100f, 30f, true));    // first round: full scan
        s.NeedsFullScan((0, 0), 105f, 30f, false);                // DirtySets watchdog disabled it: records inactive
        Assert.True(s.NeedsFullScan((0, 0), 110f, 30f, true));    // reactivated within interval, same zone -> still forced
    }

    [Fact]
    public void DrainKeepsUnsentInAreaIdsAndPrunesTheRest()
    {
        var s = new DirtyPeerState<int>();
        foreach (var id in new[] { 1, 2, 3, 4 }) s.Pending.Add(id);
        var into = new List<int>();

        s.Drain(into,
            exists:     id => id != 1,          // 1 was destroyed
            inArea:     id => id != 2,          // 2 is outside the peer's area
            shouldSend: id => id != 3);         // 3 was already delivered

        Assert.Equal(new[] { 4 }, into);
        // 4 is still pending: it may not fit the window this round; it is removed
        // on a later drain when shouldSend turns false.
        Assert.Equal(new HashSet<int> { 4 }, s.Pending);
    }

    [Fact]
    public void DrainKeepsDeferredIdsPending()
    {
        var s = new DirtyPeerState<int>();
        foreach (var id in new[] { 1, 2 }) s.Pending.Add(id);
        var into = new List<int>();

        s.Drain(into,
            exists:     id => true,
            inArea:     id => true,
            shouldSend: id => true,
            deferSend:  id => id == 2);        // 2 is relay-throttled this round

        Assert.Equal(new[] { 1 }, into);
        Assert.Equal(new HashSet<int> { 1, 2 }, s.Pending);
    }
}

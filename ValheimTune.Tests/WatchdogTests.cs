using ValheimTune.Patches;
using Xunit;

public class WatchdogTests
{
    [Fact]
    public void TripsOnlyWhenAllFourConditionsHold()
    {
        Assert.True(DirtyPatches.WatchdogShouldTrip(dirtySetsOn: true, disabled: false, recvSeen: true, marksInWindow: 0));
    }

    [Fact]
    public void DoesNotTripWhenMarksSeenOrRecvNotSeen()
    {
        Assert.False(DirtyPatches.WatchdogShouldTrip(dirtySetsOn: true, disabled: false, recvSeen: true, marksInWindow: 1));
        Assert.False(DirtyPatches.WatchdogShouldTrip(dirtySetsOn: true, disabled: false, recvSeen: false, marksInWindow: 0));
    }
}

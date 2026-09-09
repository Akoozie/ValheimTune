using Xunit;
using ValheimTune.Patches;

// The 1.0 port replaced ZNetScene.InActiveArea(sector, zone, radius) with a Chebyshev ring test
// mirroring ZDOMan.FindSectorObjects. InNear/InDistant need ZoneSystem.instance and cannot run
// without the game; the ring maths they gate on can, and an off-by-one there silently drops ZDOs.
public class ZoneRingTests
{
    [Fact]
    public void SameZoneIsDistanceZero() => Assert.Equal(0, DirtyPatches.ZoneChebyshev(3, -7, 3, -7));

    [Theory]
    [InlineData(0, 0, 2, 0, 2)]    // straight along x
    [InlineData(0, 0, 0, -2, 2)]   // straight along y, negative
    [InlineData(0, 0, 2, 2, 2)]    // diagonal: Chebyshev, not Manhattan
    [InlineData(0, 0, -3, 1, 3)]   // larger axis wins
    [InlineData(5, 5, 4, 3, 2)]    // away from the origin
    public void RingDistanceIsChebyshev(int ax, int ay, int bx, int by, int expected)
        => Assert.Equal(expected, DirtyPatches.ZoneChebyshev(ax, ay, bx, by));

    [Fact]
    public void IsSymmetric()
        => Assert.Equal(DirtyPatches.ZoneChebyshev(1, 2, -4, 9), DirtyPatches.ZoneChebyshev(-4, 9, 1, 2));
}

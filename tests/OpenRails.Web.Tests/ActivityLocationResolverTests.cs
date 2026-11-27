using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class ActivityLocationResolverTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(1024, -1024, 1, 0, -1024, -1024)]
    [InlineData(-1024.01, 1024.01, -1, 1, 1023.99, -1023.99)]
    [InlineData(3072, -3072, 2, -1, -1024, -1024)]
    public void ConvertsWorldCoordinatesToCenteredMstsTiles(
        double worldX, double worldZ, int tileX, int tileZ, double localX, double localZ)
    {
        var result = ActivityLocationResolver.Resolve(new TrackWorldPosition(worldX, 0, worldZ));
        Assert.Equal(tileX, result.TileX);
        Assert.Equal(tileZ, result.TileZ);
        Assert.Equal(localX, result.X, 2);
        Assert.Equal(localZ, result.Z, 2);
    }

    [Fact]
    public void RejectsNonFiniteWorldCoordinates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ActivityLocationResolver.Resolve(new TrackWorldPosition(double.NaN, 0, 0)));
    }
}

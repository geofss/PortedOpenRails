using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class SignalOccupancyResolverTests
{
    [Fact]
    public void IncludesEveryVectorNodeTouchedByAllCars()
    {
        var occupied = SignalOccupancyResolver.Resolve(CreateRoute(), new[]
        {
            new TrainCarRouteOccupancy(5, 12),
            new TrainCarRouteOccupancy(28, 16)
        });
        Assert.Equal(new uint[] { 2, 3 }, occupied.OrderBy(value => value));
    }

    [Fact]
    public void IncludesTrailingCarBehindLeadCar()
    {
        var occupied = SignalOccupancyResolver.Resolve(CreateRoute(), new[]
        {
            new TrainCarRouteOccupancy(35, 10),
            new TrainCarRouteOccupancy(12, 10)
        });
        Assert.Contains((uint)2, occupied);
        Assert.Contains((uint)3, occupied);
    }

    [Fact]
    public void ReturnsAnImmutableOccupiedCircuitSet()
    {
        var occupied = SignalOccupancyResolver.Resolve(CreateRoute(), new[]
        {
            new TrainCarRouteOccupancy(5, 12)
        });

        Assert.False(occupied is ISet<uint>);
        Assert.Equal(new uint[] { 2 }, occupied.OrderBy(value => value));
    }

    [Theory]
    [InlineData(double.NaN, 10)]
    [InlineData(0, 0)]
    [InlineData(0, double.PositiveInfinity)]
    public void RejectsInvalidVehicleIntervals(double center, double length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SignalOccupancyResolver.Resolve(CreateRoute(),
                new[] { new TrainCarRouteOccupancy(center, length) }));
    }

    private static TrackNetworkRoute CreateRoute()
    {
        var first = new TrackVectorPath(2, new[] { 1, 5 }, Array.Empty<TrackSectionPlacement>(), 20);
        var second = new TrackVectorPath(3, new[] { 5, 6 }, Array.Empty<TrackSectionPlacement>(), 20);
        return new TrackNetworkRoute(
            new[] { new TrackNetworkRouteSegment(first, false), new TrackNetworkRouteSegment(second, false) },
            Array.Empty<string>(), 40);
    }
}

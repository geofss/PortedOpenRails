using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class ServiceRoutePositionResolverTests
{
    [Fact]
    public void MapsNativeTileCoordinatesToConnectedRouteStartDistance()
    {
        var profile = BuildProfile(CreatePath(100));
        var trace = new RoutePathTrace(new[] { 0 },
            new[] { new RoutePathPoint(1, 0, -2048, 0, 40, 1, 1) },
            Array.Empty<string>());

        var result = ServiceRoutePositionResolver.Resolve(trace, profile);

        Assert.Equal(40, result.DistanceMeters, 3);
        Assert.Equal(0, result.OffsetMeters, 3);
    }

    [Fact]
    public void RejectsServicePathStartingOnAnotherTrack()
    {
        var profile = BuildProfile(CreatePath(100));
        var trace = new RoutePathTrace(new[] { 0 },
            new[] { new RoutePathPoint(0, 0, 100, 0, 40, 1, 1) },
            Array.Empty<string>());

        var exception = Assert.Throws<InvalidDataException>(() =>
            ServiceRoutePositionResolver.Resolve(trace, profile, maximumOffsetMeters: 20));

        Assert.Contains("from the selected connected route", exception.Message);
    }

    [Fact]
    public void RejectsServicePathThatStartsOnRouteButBranchesAwayLater()
    {
        var profile = BuildProfile(CreatePath(100));
        var trace = new RoutePathTrace(new[] { 0, 1 },
            new[]
            {
                new RoutePathPoint(0, 0, 0, 0, 10, 1, 1),
                new RoutePathPoint(0, 0, 100, 0, 80, 1, 1)
            },
            Array.Empty<string>());

        var exception = Assert.Throws<InvalidDataException>(() =>
            ServiceRoutePositionResolver.Resolve(trace, profile, maximumOffsetMeters: 20));

        Assert.Contains("Service path point 1", exception.Message);
    }

    [Fact]
    public void ReturnsOrderedWaypointDistancesForAiPathFollowing()
    {
        var profile = BuildProfile(CreatePath(100));
        var trace = new RoutePathTrace(new[] { 0, 1, 2 },
            new[]
            {
                new RoutePathPoint(0, 0, 0, 0, 10, 1, 1),
                new RoutePathPoint(0, 0, 0, 0, 50, 1, 1),
                new RoutePathPoint(0, 0, 0, 0, 90, 1, 1)
            },
            Array.Empty<string>());

        var result = ServiceRoutePositionResolver.Resolve(trace, profile);

        Assert.Equal(new[] { 10d, 50d, 90d }, result.RouteWaypointDistancesMeters);
        Assert.Equal(1, result.Direction);
    }

    [Fact]
    public void RejectsServicePathThatReversesWithoutNativeReversalTransition()
    {
        var profile = BuildProfile(CreatePath(100));
        var trace = new RoutePathTrace(new[] { 0, 1, 2 },
            new[]
            {
                new RoutePathPoint(0, 0, 0, 0, 10, 1, 1),
                new RoutePathPoint(0, 0, 0, 0, 80, 1, 1),
                new RoutePathPoint(0, 0, 0, 0, 20, 1, 1)
            },
            Array.Empty<string>());

        var exception = Assert.Throws<InvalidDataException>(() =>
            ServiceRoutePositionResolver.Resolve(trace, profile));

        Assert.Contains("reverses direction", exception.Message);
    }

    [Fact]
    public void PreservesNativeWaitingPointFlagsFromPathNodes()
    {
        var profile = BuildProfile(CreatePath(100));
        var trace = new RoutePathTrace(new[] { 0, 1, 2 },
            new[]
            {
                new RoutePathPoint(0, 0, 0, 0, 10, 1, 1),
                new RoutePathPoint(0, 0, 0, 0, 50, 1, 1),
                new RoutePathPoint(0, 0, 0, 0, 90, 1, 1)
            }, Array.Empty<string>());
        var path = new RoutePathDefinition("test.pat", "test", "test", "", "", 0,
            Array.Empty<RoutePathPoint>(),
            new[]
            {
                new RoutePathNode(0, 1, uint.MaxValue, 0),
                new RoutePathNode(2, 2, uint.MaxValue, 1),
                new RoutePathNode(0, uint.MaxValue, uint.MaxValue, 2)
            }, Array.Empty<string>());

        var result = ServiceRoutePositionResolver.Resolve(trace, profile, path: path);

        Assert.False(result.RouteWaypoints![0].IsWaitingPoint);
        Assert.True(result.RouteWaypoints[1].IsWaitingPoint);
        Assert.False(result.RouteWaypoints[2].IsWaitingPoint);
    }

    [Fact]
    public void RejectsBrokenServicePathTrace()
    {
        var profile = BuildProfile(CreatePath(100));
        var trace = new RoutePathTrace(new[] { 0 }, Array.Empty<RoutePathPoint>(),
            new[] { "Path link points to missing node 5." });

        Assert.Throws<InvalidDataException>(() => ServiceRoutePositionResolver.Resolve(trace, profile));
    }

    private static TrackNetworkRouteProfile BuildProfile(TrackVectorPath path)
    {
        var route = new TrackNetworkRoute(
            new[] { new TrackNetworkRouteSegment(path, false) },
            Array.Empty<string>(), path.LengthMeters);
        return TrackNetworkRouteProfile.Build(route, 5);
    }

    private static TrackVectorPath CreatePath(double length)
    {
        var start = new TrackWorldPosition(0, 0, 0);
        var end = new TrackWorldPosition(0, 0, length);
        var source = new TrackVectorSection(42, 0, 0, 0, 0, 0, 1, "00",
            0, 0, 0, 0, 0, 0, 0, 0);
        var geometry = new TrackSectionGeometry(42, null, (float)length, null, null, false);
        var section = new TrackSectionPlacement(source, geometry, start, length);
        return new TrackVectorPath(1, new[] { 2, 3 }, new[] { section }, length, end);
    }
}

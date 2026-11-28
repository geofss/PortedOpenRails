using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackNetworkRouteProfileTests
{
    [Fact]
    public void RejectsEmptyOrNullRouteSegmentsBeforeBuildingProfile()
    {
        Assert.Throws<ArgumentException>(() => TrackNetworkRouteProfile.Build(
            new TrackNetworkRoute(Array.Empty<TrackNetworkRouteSegment>(),
                Array.Empty<string>(), 0)));
        Assert.Throws<ArgumentException>(() => TrackNetworkRouteProfile.Build(
            new TrackNetworkRoute(new TrackNetworkRouteSegment[] { null! },
                Array.Empty<string>(), 0)));
    }

    [Fact]
    public void SamplesDownhillGradeFromElevationAlongRoute()
    {
        var path = CreateSlopePath(100, 10);
        var route = new TrackNetworkRoute(
            new[] { new TrackNetworkRouteSegment(path, false) },
            Array.Empty<string>(),
            100);

        var profile = TrackNetworkRouteProfile.Build(route, 5);
        var sample = profile.Sample(50);

        Assert.Equal(-Math.Atan2(10, 100), sample.GradeRadians, 4);
        Assert.Equal(5, sample.Position.Y, 3);
        Assert.Equal(50, sample.Position.Z, 3);
        Assert.Equal(0, sample.HeadingRadians, 3);
        Assert.Equal(100, profile.LengthMeters, 3);
    }

    [Fact]
    public void ReversingRouteSegmentReversesGradeSignAndHeading()
    {
        var path = CreateSlopePath(100, 10);
        var route = new TrackNetworkRoute(
            new[] { new TrackNetworkRouteSegment(path, true) },
            Array.Empty<string>(),
            100);

        var profile = TrackNetworkRouteProfile.Build(route, 5);
        var sample = profile.Sample(50);

        Assert.Equal(Math.Atan2(10, 100), sample.GradeRadians, 4);
        Assert.Equal(5, sample.Position.Y, 3);
        Assert.Equal(50, sample.Position.Z, 3);
        Assert.Equal(Math.PI, Math.Abs(sample.HeadingRadians), 3);
    }

    [Fact]
    public void JoinsReversedConnectedVectorNodesIntoOneDistanceProfile()
    {
        var first = CreateSlopePath(100, 0);
        var second = CreateSlopePath(
            length: 100,
            rise: 0,
            startZ: 200,
            endZ: 100,
            heading: Math.PI,
            trackNodeIndex: 2,
            endpointNodes: new[] { 3, 2 });

        var route = new TrackNetworkRoute(
            new[]
            {
                new TrackNetworkRouteSegment(first, false),
                new TrackNetworkRouteSegment(second, true)
            },
            Array.Empty<string>(),
            200);

        var profile = TrackNetworkRouteProfile.Build(route, 5);
        var sample = profile.Sample(150);

        Assert.Equal(200, profile.LengthMeters, 3);
        Assert.Equal(150, sample.Position.Z, 3);
        Assert.Equal(0, sample.Position.Y, 3);
        Assert.Equal(0, sample.HeadingRadians, 3);
    }

    [Fact]
    public void MapsSignalPositionOnForwardAndReversedVectorSegments()
    {
        var path = CreateSlopePath(100, 0, trackNodeIndex: 42);
        var forward = TrackNetworkRouteProfile.Build(new TrackNetworkRoute(
            new[] { new TrackNetworkRouteSegment(path, false) }, Array.Empty<string>(), 100), 5);
        var reverse = TrackNetworkRouteProfile.Build(new TrackNetworkRoute(
            new[] { new TrackNetworkRouteSegment(path, true) }, Array.Empty<string>(), 100), 5);

        Assert.True(forward.TryGetRouteDistanceForTrackNode(42, 25, out var forwardDistance));
        Assert.True(reverse.TryGetRouteDistanceForTrackNode(42, 25, out var reverseDistance));
        Assert.Equal(25, forwardDistance, 3);
        Assert.Equal(75, reverseDistance, 3);
    }

    [Fact]
    public void MapsSignalDistanceUsingCumulativeOffsetAcrossConnectedVectorNodes()
    {
        var first = CreateSlopePath(100, 0, trackNodeIndex: 1, endpointNodes: new[] { 2, 3 });
        var second = CreateSlopePath(100, 0, startZ: 100, trackNodeIndex: 2,
            endpointNodes: new[] { 3, 4 });
        var profile = TrackNetworkRouteProfile.Build(new TrackNetworkRoute(
            new[]
            {
                new TrackNetworkRouteSegment(first, false),
                new TrackNetworkRouteSegment(second, false)
            },
            Array.Empty<string>(), 200), 5);

        Assert.True(profile.TryGetRouteDistanceForTrackNode(2, 25, out var distance));
        Assert.Equal(125, distance, 3);
    }

    [Fact]
    public void DoesNotMapSignalsFromVectorNodesOutsideTheConnectedRoute()
    {
        var profile = TrackNetworkRouteProfile.Build(new TrackNetworkRoute(
            new[] { new TrackNetworkRouteSegment(CreateSlopePath(100, 0), false) },
            Array.Empty<string>(), 100), 5);

        Assert.False(profile.TryGetRouteDistanceForTrackNode(999, 10, out _));
    }

    [Fact]
    public void ClampsSamplesAtBothEndsOfTheConnectedRoute()
    {
        var path = CreateSlopePath(100, 10);
        var route = new TrackNetworkRoute(
            new[] { new TrackNetworkRouteSegment(path, false) },
            Array.Empty<string>(),
            100);
        var profile = TrackNetworkRouteProfile.Build(route, 5);

        Assert.Equal(0, profile.Sample(-25).Position.Z, 3);
        Assert.Equal(100, profile.Sample(125).Position.Z, 3);
    }

    [Fact]
    public void FindsNearestProjectedDistanceAndLateralOffset()
    {
        var path = CreateSlopePath(100, 0);
        var route = new TrackNetworkRoute(
            new[] { new TrackNetworkRouteSegment(path, false) },
            Array.Empty<string>(), 100);
        var profile = TrackNetworkRouteProfile.Build(route, 5);

        var matched = profile.TryFindNearestDistance(
            new TrackWorldPosition(3, 0, 30), 5,
            out var distance, out var offset);

        Assert.True(matched);
        Assert.Equal(30, distance, 3);
        Assert.Equal(3, offset, 3);
    }

    [Fact]
    public void RejectsNearestPointOutsideConfiguredOffset()
    {
        var path = CreateSlopePath(100, 0);
        var route = new TrackNetworkRoute(
            new[] { new TrackNetworkRouteSegment(path, false) },
            Array.Empty<string>(), 100);
        var profile = TrackNetworkRouteProfile.Build(route, 5);

        var matched = profile.TryFindNearestDistance(
            new TrackWorldPosition(40, 0, 30), 10,
            out _, out var offset);

        Assert.False(matched);
        Assert.Equal(40, offset, 3);
    }

    private static TrackVectorPath CreateSlopePath(
        double length,
        double rise,
        double startZ = 0,
        double? endZ = null,
        double heading = 0,
        uint trackNodeIndex = 1,
        int[]? endpointNodes = null)
    {
        var start = new TrackWorldPosition(0, 0, startZ);
        var end = new TrackWorldPosition(0, rise, endZ ?? startZ + length);
        var source = new TrackVectorSection(42, 0, 0, 0, 0, 0, 1, "00",
            0, 0, 0, 0, (float)startZ, 0, (float)heading, 0);
        var geometry = new TrackSectionGeometry(42, null, (float)length, null, null, false);
        var section = new TrackSectionPlacement(source, geometry, start, length);
        return new TrackVectorPath(
            trackNodeIndex, endpointNodes ?? new[] { 2, 3 }, new[] { section }, length, end);
    }
}

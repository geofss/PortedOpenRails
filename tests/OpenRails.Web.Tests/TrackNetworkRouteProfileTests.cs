using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackNetworkRouteProfileTests
{
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
        Assert.Equal(100, profile.LengthMeters, 3);
    }

    [Fact]
    public void ReversingRouteSegmentReversesGradeSign()
    {
        var path = CreateSlopePath(100, 10);
        var route = new TrackNetworkRoute(
            new[] { new TrackNetworkRouteSegment(path, true) },
            Array.Empty<string>(),
            100);

        var profile = TrackNetworkRouteProfile.Build(route, 5);
        var sample = profile.Sample(50);

        Assert.Equal(Math.Atan2(10, 100), sample.GradeRadians, 4);
    }

    private static TrackVectorPath CreateSlopePath(double length, double rise)
    {
        var start = new TrackWorldPosition(0, 0, 0);
        var end = new TrackWorldPosition(0, rise, length);
        var source = new TrackVectorSection(42, 0, 0, 0, 0, 0, 1, "00",
            0, 0, 0, 0, 0, 0, 0, 0);
        var geometry = new TrackSectionGeometry(42, null, (float)length, null, null, false);
        var section = new TrackSectionPlacement(source, geometry, start, length);
        return new TrackVectorPath(1, new[] { 2, 3 }, new[] { section }, length, end);
    }
}

using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackGeometryTessellatorTests
{
    [Fact]
    public void SamplesPositiveCurveUsingOpenRailsHeadingConvention()
    {
        var points = BuildCurve(90);

        Assert.True(points.Count > 10);
        Assert.Equal(0, points[0].X, 6);
        Assert.Equal(0, points[0].Z, 6);
        Assert.Equal(100, points[^1].X, 5);
        Assert.Equal(100, points[^1].Z, 5);
    }

    [Fact]
    public void SamplesNegativeCurveOnTheMirroredSide()
    {
        var points = BuildCurve(-90);

        Assert.True(points.Count > 10);
        Assert.Equal(-100, points[^1].X, 5);
        Assert.Equal(100, points[^1].Z, 5);
    }

    [Fact]
    public void SamplesStraightTrackUsingHeadingMeasuredRightOfNorth()
    {
        var source = CreateSourceSection(1, MathF.PI / 2);
        var geometry = new TrackSectionGeometry(1, null, 10, null, null, false);
        var placement = new TrackSectionPlacement(source, geometry, new TrackWorldPosition(4, 2, 8), 10);
        var path = new TrackVectorPath(1, new[] { 2, 3 }, new[] { placement }, 10,
            new TrackWorldPosition(14, 2, 8));

        var points = new TrackGeometryTessellator().BuildPolyline(path, 2);

        Assert.Equal(6, points.Count);
        Assert.Equal(14, points[^1].X, 5);
        Assert.Equal(8, points[^1].Z, 5);
    }

    private static IReadOnlyList<TrackWorldPosition> BuildCurve(float angleDegrees)
    {
        const double radius = 100;
        var source = CreateSourceSection(1, 0);
        var geometry = new TrackSectionGeometry(1, null, null, (float)radius, angleDegrees, false);
        var length = geometry.CalculatedLengthMeters;
        var signedAngle = angleDegrees * Math.PI / 180d;
        var direction = Math.Sign(signedAngle);
        var end = new TrackWorldPosition(
            direction * radius * (1 - Math.Cos(Math.Abs(signedAngle))),
            0,
            radius * Math.Sin(Math.Abs(signedAngle)));
        var placement = new TrackSectionPlacement(source, geometry, new TrackWorldPosition(0, 0, 0), length);
        var path = new TrackVectorPath(1, new[] { 2, 3 }, new[] { placement }, length, end);

        return new TrackGeometryTessellator().BuildPolyline(path, 10);
    }

    private static TrackVectorSection CreateSourceSection(uint index, float heading) =>
        new(index, 0, 0, 0, 0, 0, 1, "00", 0, 0, 0, 0, 0, 0, heading, 0);
}

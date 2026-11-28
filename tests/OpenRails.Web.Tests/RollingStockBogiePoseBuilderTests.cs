using System.Numerics;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class RollingStockBogiePoseBuilderTests
{
    [Fact]
    public void YawsBogieToTheLocalRouteTangent()
    {
        var mesh = new TrackShapeMesh(0, 0,
            new[]
            {
                new TrackShapeMatrix("BOGIE1", 1,0,0, 0,1,0, 0,0,1, 0,0,2),
                new TrackShapeMatrix("BOGIE2", 1,0,0, 0,1,0, 0,0,1, 0,0,-2)
            },
            new[] { new TrackShapeLodMesh(500, new[] { -1, -1 }, Array.Empty<TrackShapeSubObjectMesh>()) });
        var points = new[]
        {
            new TrackWorldPosition(0, 0, 0),
            new TrackWorldPosition(0, 0, 10),
            new TrackWorldPosition(10, 0, 10)
        };

        var poses = RollingStockBogiePoseBuilder.Build(mesh, points, new[] { 0d, 10d, 20d },
            routeDistanceMeters: 10, vehicleHeadingRadians: Math.PI / 2);

        var pose = Assert.Single(poses);
        Assert.Equal(0, pose.Key);
        Assert.True(Matrix4x4.Decompose(pose.Value, out _, out var rotation, out _));
        Assert.InRange(MathF.Abs(Quaternion.Dot(rotation, Quaternion.CreateFromAxisAngle(Vector3.UnitY, -(float)Math.PI / 2))),
            0.9999f, 1.0001f);
    }

    [Fact]
    public void ReturnsNoOverridesForStraightTrackOrNoBogieNodes()
    {
        var mesh = new TrackShapeMesh(0, 0,
            new[] { new TrackShapeMatrix("BODY", 1,0,0, 0,1,0, 0,0,1, 0,0,0) },
            new[] { new TrackShapeLodMesh(500, new[] { -1 }, Array.Empty<TrackShapeSubObjectMesh>()) });
        var points = new[]
        {
            new TrackWorldPosition(0, 0, 0),
            new TrackWorldPosition(0, 0, 100)
        };

        Assert.Empty(RollingStockBogiePoseBuilder.Build(mesh, points, new[] { 0d, 100d }, 50, 0));
    }
}

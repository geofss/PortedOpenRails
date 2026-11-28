using System.Numerics;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class RollingStockWheelPoseBuilderTests
{
    [Fact]
    public void RotatesNativeWheelNodesByDistanceAndDeclaredRadius()
    {
        var mesh = new TrackShapeMesh(0, 0,
            new[] { new TrackShapeMatrix("WHEELS1", 1,0,0, 0,1,0, 0,0,1, 0,0,0) },
            new[] { new TrackShapeLodMesh(500, new[] { -1 }, Array.Empty<TrackShapeSubObjectMesh>()) });
        var metadata = new RollingStockMetadata("Wagon", "Freight", 20_000, 2.5f, 4, 20, 4, 8,
            null, null, null, "wagon.wag", WheelRadiusM: 1);

        var pose = RollingStockWheelPoseBuilder.Build(mesh, metadata, false, Math.PI);

        var wheel = Assert.Single(pose);
        Assert.Equal(0, wheel.Key);
        var rotatedUp = Vector3.TransformNormal(Vector3.UnitY, wheel.Value);
        Assert.InRange(rotatedUp.Y, -1.001f, -0.999f);
    }

    [Fact]
    public void IgnoresNonWheelAndHighMountedWheelNamedObjects()
    {
        var mesh = new TrackShapeMesh(0, 0,
            new[] { new TrackShapeMatrix("WHEELS1", 1,0,0, 0,1,0, 0,0,1, 0,4,0) },
            new[] { new TrackShapeLodMesh(500, new[] { -1 }, Array.Empty<TrackShapeSubObjectMesh>()) });

        Assert.Empty(RollingStockWheelPoseBuilder.Build(mesh, null, false, 100));
    }
}

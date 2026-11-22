using System.Numerics;
using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class RollingStockTrackPlacementBuilderTests
{
    [Fact]
    public void PlacesVehicleMeshesAlongRouteCenterlineWithTrackHeading()
    {
        var consist = new ConsistMetadata("Freight", new[]
        {
            new ConsistCar("WagonA", "Freight", 10, false, false, false),
            new ConsistCar("WagonB", "Freight", 11, false, false, false)
        }, "TRAINS/CONSISTS/Freight.con");
        var assets = new[]
        {
            CreateReference("Freight", 10, "WagonA", 20),
            CreateReference("Freight", 11, "WagonB", 20)
        };
        var meshes = new Dictionary<int, TrackShapeAssetMesh>
        {
            [10] = CreateMesh(10),
            [11] = CreateMesh(11)
        };
        var path = CreateStraightPath(length: 100, heading: Math.PI / 2);

        var result = new RollingStockTrackPlacementBuilder().Build(consist, assets, meshes, path);

        Assert.Equal(2, result.Placements.Count);
        Assert.Empty(result.Issues);
        Assert.Equal(40, result.UsedLengthMeters, 5);
        Assert.Equal(10, result.Placements[0].Anchor.X, 1);
        Assert.Equal(0, result.Placements[0].Anchor.Z, 1);
        Assert.Equal(30, result.Placements[1].Anchor.X, 1);
        Assert.Equal(0, result.Placements[1].Anchor.Z, 1);
        Assert.Equal(Math.PI / 2, result.Placements[0].HeadingRadians, 0.02);
        Assert.Equal((uint)42, result.Placements[0].TrackSectionIndex);
    }

    [Fact]
    public void MapsEachSimulatedCarDistanceToItsOwnRoutePlacement()
    {
        var consist = new ConsistMetadata("Moving", new[]
        {
            new ConsistCar("WagonA", "Freight", 40, false, false, false),
            new ConsistCar("WagonB", "Freight", 41, false, false, true)
        }, "moving.con");
        var assets = new[]
        {
            CreateReference("Moving", 40, "WagonA", 20),
            CreateReference("Moving", 41, "WagonB", 20)
        };
        var meshes = new Dictionary<int, TrackShapeAssetMesh>
        {
            [40] = CreateMesh(40),
            [41] = CreateMesh(41)
        };
        var motion = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(20_000, 5, 15),
            new TrainCarMotionState(20_000, 5, 45)
        });

        var result = new RollingStockTrackPlacementBuilder().BuildAtMotionState(
            consist, assets, meshes, CreateStraightPath(100, 0), motion);

        Assert.Equal(2, result.Placements.Count);
        Assert.Equal(15, result.Placements[0].Anchor.Z, 1);
        Assert.Equal(45, result.Placements[1].Anchor.Z, 1);
        Assert.Equal(Math.PI, Math.Abs(result.Placements[1].HeadingRadians), 0.02);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void SupportsConsistOffsetsAlongTheSelectedRoutePath()
    {
        var consist = new ConsistMetadata("Moving", new[]
        {
            new ConsistCar("WagonA", "Freight", 30, false, false, false),
            new ConsistCar("WagonB", "Freight", 31, false, false, false)
        }, "moving.con");
        var assets = new[]
        {
            CreateReference("Moving", 30, "WagonA", 20),
            CreateReference("Moving", 31, "WagonB", 20)
        };
        var meshes = new Dictionary<int, TrackShapeAssetMesh>
        {
            [30] = CreateMesh(30),
            [31] = CreateMesh(31)
        };

        var result = new RollingStockTrackPlacementBuilder().Build(
            consist, assets, meshes, CreateStraightPath(100, 0),
            startDistanceMeters: 20);

        Assert.Equal(2, result.Placements.Count);
        Assert.Equal(30, result.Placements[0].Anchor.Z, 1);
        Assert.Equal(50, result.Placements[1].Anchor.Z, 1);
        Assert.Equal(40, result.UsedLengthMeters, 5);
    }

    [Fact]
    public void FollowsCurvedTrackAndUsesLocalTangentForVehicleHeading()
    {
        var consist = new ConsistMetadata("Curve", new[]
        {
            new ConsistCar("CurveWagon", "Freight", 21, false, false, false)
        }, "curve.con");
        var assets = new[] { CreateReference("Curve", 21, "CurveWagon", 10) };
        var meshes = new Dictionary<int, TrackShapeAssetMesh> { [21] = CreateMesh(21) };
        var radius = 100d;
        var angleDegrees = 90f;
        var length = radius * Math.PI / 2d;
        var source = new TrackVectorSection(43, 0, 0, 0, 0, 0, 1, "00",
            0, 0, 0, 0, 0, 0, 0, 0);
        var geometry = new TrackSectionGeometry(43, null, null, (float)radius, angleDegrees, false);
        var section = new TrackSectionPlacement(source, geometry,
            new TrackWorldPosition(0, 0, 0), length);
        var path = new TrackVectorPath(2, new[] { 3, 4 }, new[] { section }, length);

        var result = new RollingStockTrackPlacementBuilder().Build(consist, assets, meshes, path);

        var placement = Assert.Single(result.Placements);
        Assert.True(placement.Anchor.X > 0);
        Assert.True(placement.Anchor.Z > 0);
        Assert.Equal(0.05, placement.HeadingRadians, 0.01);
    }

    [Fact]
    public void RespectsFlippedCarsAndReportsCarsBeyondAvailableTrack()
    {
        var consist = new ConsistMetadata("Short", new[]
        {
            new ConsistCar("Flipped", "Freight", 1, false, false, true),
            new ConsistCar("Beyond", "Freight", 2, false, false, false)
        }, "short.con");
        var assets = new[]
        {
            CreateReference("Short", 1, "Flipped", 15),
            CreateReference("Short", 2, "Beyond", 15)
        };
        var meshes = new Dictionary<int, TrackShapeAssetMesh> { [1] = CreateMesh(1), [2] = CreateMesh(2) };
        var path = CreateStraightPath(length: 20, heading: 0);

        var result = new RollingStockTrackPlacementBuilder().Build(consist, assets, meshes, path);

        Assert.Single(result.Placements);
        Assert.Single(result.Issues);
        Assert.Contains("too short", result.Issues[0]);
        Assert.Equal(Math.PI, Math.Abs(result.Placements[0].HeadingRadians), 0.02);
    }

    private static ConsistCarAssetReference CreateReference(string consist, int uid, string name, float length) =>
        new(consist, uid, name, "Freight", false, false, $"{name}.wag", true, null,
            new RollingStockMetadata(name, "Freight", 10000, 3, 4, length, 4, 8, null, null, null, $"{name}.wag"),
            $"{name}.s", true);

    private static TrackShapeAssetMesh CreateMesh(uint index)
    {
        var primitive = new TrackShapePrimitiveMesh(0, 0, 0, 0, new[] { -1 },
            Array.Empty<int>(), "Tex", "body.ace", 0, 0, 0, false, 0);
        var subObject = new TrackShapeSubObjectMesh(Array.Empty<TrackShapeMeshVertex>(),
            Array.Empty<int>(), new[] { primitive });
        var lod = new TrackShapeLodMesh(1000, new[] { -1 }, new[] { subObject });
        var mesh = new TrackShapeMesh(0, 0, Array.Empty<TrackShapeMatrix>(), new[] { lod });
        return new TrackShapeAssetMesh(index, $"vehicle{index}.s", $"TRAINS/TRAINSET/Freight/vehicle{index}.s", mesh);
    }

    private static TrackVectorPath CreateStraightPath(double length, double heading)
    {
        var source = new TrackVectorSection(42, 0, 0, 0, 0, 0, 1, "00",
            0, 0, 0, 0, 0, 0, (float)heading, 0);
        var geometry = new TrackSectionGeometry(42, null, (float)length, null, null, false);
        var section = new TrackSectionPlacement(source, geometry, new TrackWorldPosition(0, 0, 0), length);
        return new TrackVectorPath(1, new[] { 2, 3 }, new[] { section }, length);
    }
}

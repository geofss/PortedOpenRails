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
            new RollingStockMetadata(name, "Freight", 10000, 3, 4, length, 4, 8, null, null, null, $"{name}.wag"));

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

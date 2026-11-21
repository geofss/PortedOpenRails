using System.Numerics;
using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackShapePlacementBuilderTests
{
    [Fact]
    public void PlacesOriginalTrackShapeAtSectionAnchorAndHeading()
    {
        var database = new TrackDatabase(new TrackNode?[1], Array.Empty<string>());
        var source = new TrackVectorSection(10, 7, 0, 0, 0, 0, 0, "", 2, 3, 100, 12, 200, 0, MathF.PI / 2, 0);
        var geometry = new TrackSectionGeometry(10, null, 25, null, null, false);
        var shape = new TrackShapeDefinition(7, "rail.s", 1, 0, 0, Array.Empty<TrackShapePath>(), false, false, false);
        var section = new TrackSectionPlacement(source, geometry, new TrackWorldPosition(4196, 12, 6344), 25, shape);
        var path = new TrackVectorPath(4, Array.Empty<int>(), new[] { section }, 25);
        var network = new TrackNetwork(database, new[] { path }, Array.Empty<string>());
        var mesh = new TrackShapeMesh(0, 0, Array.Empty<TrackShapeMatrix>(), Array.Empty<TrackShapeLodMesh>());
        var asset = new TrackShapeAssetMesh(7, "rail.s", "GLOBAL/SHAPES/rail.s", mesh);

        var result = Assert.Single(new TrackShapePlacementBuilder().Build(network, new[] { asset }));
        Assert.Equal((uint)4, result.TrackNodeIndex);
        Assert.Equal((uint)10, result.TrackSectionIndex);
        Assert.Equal((uint)7, result.ShapeIndex);
        Assert.Equal(new Vector3(4196, 12, 6344), result.WorldTransform.Translation);
        Assert.Equal(MathF.PI / 2, result.HeadingRadians, 5);
        Assert.True(Matrix4x4.Invert(result.WorldTransform, out _));
    }

    [Fact]
    public void SkipsPlacementWhenOriginalMeshIsMissing()
    {
        var database = new TrackDatabase(new TrackNode?[1], Array.Empty<string>());
        var source = new TrackVectorSection(10, 7, 0, 0, 0, 0, 0, "", 0, 0, 0, 0, 0, 0, 0, 0);
        var geometry = new TrackSectionGeometry(10, null, 10, null, null, false);
        var shape = new TrackShapeDefinition(7, "rail.s", 1, 0, 0, Array.Empty<TrackShapePath>(), false, false, false);
        var section = new TrackSectionPlacement(source, geometry, new TrackWorldPosition(0, 0, 0), 10, shape);
        var path = new TrackVectorPath(1, Array.Empty<int>(), new[] { section }, 10);
        var network = new TrackNetwork(database, new[] { path }, Array.Empty<string>());

        Assert.Empty(new TrackShapePlacementBuilder().Build(network, Array.Empty<TrackShapeAssetMesh>()));
    }
}
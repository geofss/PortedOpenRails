using System.Numerics;
using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackShapeRenderGeometryBuilderTests
{
    [Fact]
    public void ExpandsPrimitiveUsingOriginalMstsMatrixConversionAndPlacement()
    {
        var matrix = new TrackShapeMatrix("root",
            1, 0, 0, 0, 1, 0, 0, 0, 1, 10, 0, 20);
        var vertices = new[]
        {
            new TrackShapeMeshVertex(new Vector3(1, 2, -3), Vector3.UnitY, Vector2.Zero, 0, 0, 0),
            new TrackShapeMeshVertex(new Vector3(2, 2, -3), Vector3.UnitY, Vector2.Zero, 0, 0, 0),
            new TrackShapeMeshVertex(new Vector3(1, 3, -3), Vector3.UnitY, Vector2.Zero, 0, 0, 0)
        };
        var primitive = new TrackShapePrimitiveMesh(0, 0, 0, 0,
            new[] { 1, -1 }, new[] { 0, 1, 2 }, "Tex", null, 0, 0, 0, false, 0);
        var subObject = new TrackShapeSubObjectMesh(vertices, Array.Empty<int>(), new[] { primitive });
        var lod = new TrackShapeLodMesh(1000, new[] { 1, -1 }, new[] { subObject });
        var mesh = new TrackShapeMesh(0, 0, new[] { matrix }, new[] { lod });
        var asset = new TrackShapeAssetMesh(4, "rail.s", "GLOBAL/SHAPES/rail.s", mesh);
        var placement = new TrackShapePlacement(1, 2, 4,
            new TrackWorldPosition(100, 0, 200), 0, asset,
            Matrix4x4.CreateTranslation(100, 0, 200));

        var result = new TrackShapeRenderGeometryBuilder().Build(new[] { placement });

        Assert.False(result.WasTruncated);
        Assert.Equal(new Vector3(111, 2, 177), result.Vertices[0]);
        Assert.Equal(new[] { 0, 1, 2 }, result.Indices);
    }
}

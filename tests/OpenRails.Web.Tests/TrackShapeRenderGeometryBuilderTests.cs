using System.Numerics;
using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackShapeRenderGeometryBuilderTests
{
    [Fact]
    public void BuildsTextureAwarePrimitiveBatchesAndPreservesOriginalTransform()
    {
        var matrix = new TrackShapeMatrix("root",
            1, 0, 0, 0, 1, 0, 0, 0, 1, 10, 0, 20);
        var vertices = new[]
        {
            new TrackShapeMeshVertex(new Vector3(1, 2, -3), Vector3.UnitY, new Vector2(0.1f, 0.2f), 0, 0, 0),
            new TrackShapeMeshVertex(new Vector3(2, 2, -3), Vector3.UnitY, new Vector2(0.3f, 0.4f), 0, 0, 0),
            new TrackShapeMeshVertex(new Vector3(1, 3, -3), Vector3.UnitY, new Vector2(0.5f, 0.6f), 0, 0, 0)
        };
        var textured = new TrackShapePrimitiveMesh(0, 0, 0, 0,
            new[] { 1, -1 }, new[] { 0, 1, 2 }, "Tex", "rail.ace", 0, 0, 2, true, 0);
        var untextured = new TrackShapePrimitiveMesh(1, 0, 0, 0,
            new[] { 1, -1 }, new[] { 0, 1, 2 }, "Tex", null, 0, 0, 0, false, 0);
        var subObject = new TrackShapeSubObjectMesh(vertices, Array.Empty<int>(), new[] { textured, untextured });
        var lod = new TrackShapeLodMesh(1000, new[] { 1, -1 }, new[] { subObject });
        var mesh = new TrackShapeMesh(0, 0, new[] { matrix }, new[] { lod });
        var asset = new TrackShapeAssetMesh(4, "rail.s", "GLOBAL/SHAPES/rail.s", mesh);
        var placement = new TrackShapePlacement(1, 2, 4,
            new TrackWorldPosition(100, 0, 200), 0, asset,
            Matrix4x4.CreateTranslation(100, 0, 200));
        var texture = new AceTextureData(1, 1, AceTexturePixelFormat.Rgba8, false,
            false, 0, new[] { new byte[] { 255, 255, 255, 255 } }, Array.Empty<AceTextureChannel>());
        var loadedTexture = new LoadedTrackShapeTexture(
            new TrackShapeTextureAssetReference(4, "rail.ace", null, "ROUTES/Test/TEXTURES/rail.ace"),
            texture);

        var result = new TrackShapeRenderGeometryBuilder().BuildTextured(new[] { placement }, new[] { loadedTexture });

        Assert.False(result.WasTruncated);
        Assert.Equal(2, result.Batches.Count);
        var texturedBatch = Assert.Single(result.Batches, batch => batch.TextureIndex == 0);
        Assert.True(texturedBatch.AlphaTest);
        Assert.Equal(2, texturedBatch.TextureAddressMode);
        Assert.Equal(new[] { 111f, 2f, 177f, 0.1f, 0.2f }, texturedBatch.Vertices.Take(5));
        Assert.Equal(new[] { 0, 1, 2 }, texturedBatch.Indices);
        var fallbackBatch = Assert.Single(result.Batches, batch => batch.TextureIndex == -1);
        Assert.False(fallbackBatch.AlphaTest);
    }

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
    [Fact]
    public void TexturedBuilderHonorsConfiguredVertexBudget()
    {
        var vertices = new[]
        {
            new TrackShapeMeshVertex(Vector3.Zero, Vector3.UnitY, Vector2.Zero, 0, 0, 0),
            new TrackShapeMeshVertex(Vector3.UnitX, Vector3.UnitY, Vector2.Zero, 0, 0, 0),
            new TrackShapeMeshVertex(Vector3.UnitY, Vector3.UnitY, Vector2.Zero, 0, 0, 0)
        };
        var primitive = new TrackShapePrimitiveMesh(0, 0, 0, 0,
            new[] { 1, -1 }, new[] { 0, 1, 2 }, "Solid", null, 0, 0, 1, false, 0);
        var subObject = new TrackShapeSubObjectMesh(vertices, Array.Empty<int>(), new[] { primitive });
        var matrix = new TrackShapeMatrix("root", 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0);
        var mesh = new TrackShapeMesh(0, 0, new[] { matrix },
            new[] { new TrackShapeLodMesh(1000, new[] { 1, -1 }, new[] { subObject }) });
        var asset = new TrackShapeAssetMesh(4, "test.s", "GLOBAL/SHAPES/test.s", mesh);
        var placement = new TrackShapePlacement(1, 2, 4, new TrackWorldPosition(0, 0, 0),
            0, asset, Matrix4x4.Identity);

        var result = new TrackShapeRenderGeometryBuilder(2, 3).BuildTextured(
            new[] { placement }, Array.Empty<LoadedTrackShapeTexture>());

        Assert.True(result.WasTruncated);
        Assert.Empty(result.Batches);
    }

    [Fact]
    public void RejectsNonpositiveGeometryBudgets()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrackShapeRenderGeometryBuilder(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrackShapeRenderGeometryBuilder(1, 0));
    }

}

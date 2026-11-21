using System.Numerics;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class WorldSceneryRenderGeometryBuilderTests
{
    [Fact]
    public void AppliesOriginalWorldMatrixAndQuaternionTransformsAndUsesDecodedTextures()
    {
        var matrixIdentity = new WorldMatrix3x3(1, 0, 0, 0, 1, 0, 0, 0, 1);
        var staticObject = new StaticWorldObject(10, "building.s", new WorldTilePosition(4, 5, 6),
            new WorldQuaternion(0, 0, 0, 1), matrixIdentity, 0, 0);
        var trackObject = new TrackWorldObject(11, 0, 0, 0, "building.s",
            new WorldTilePosition(10, 5, 20), new WorldQuaternion(0, 0, 0, 1), 0, 0, null);
        var tileData = new WorldTileData("ROUTES/Test/WORLD/W-000001+000002.w", -1, 2,
            new[] { staticObject }, new[] { trackObject }, Array.Empty<string>());
        var placements = new WorldObjectPlacementBuilder().Build(tileData);

        var vertices = new[]
        {
            new TrackShapeMeshVertex(new Vector3(1, 2, -3), Vector3.UnitY, new Vector2(0.1f, 0.2f), 0, 0, 0),
            new TrackShapeMeshVertex(new Vector3(2, 2, -3), Vector3.UnitY, new Vector2(0.3f, 0.4f), 0, 0, 0),
            new TrackShapeMeshVertex(new Vector3(1, 3, -3), Vector3.UnitY, new Vector2(0.5f, 0.6f), 0, 0, 0)
        };
        var primitive = new TrackShapePrimitiveMesh(0, 0, 0, 0,
            new[] { 1, -1 }, new[] { 0, 1, 2 }, "Tex", "rail.ace", 0, 0, 1, true, 0);
        var subObject = new TrackShapeSubObjectMesh(vertices, Array.Empty<int>(), new[] { primitive });
        var lod = new TrackShapeLodMesh(1000, new[] { 1, -1 }, new[] { subObject });
        var identityMatrix = new TrackShapeMatrix("root", 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0);
        var mesh = new TrackShapeMesh(0, 0, new[] { identityMatrix }, new[] { lod });
        var assets = new[]
        {
            new TrackShapeAssetMesh(10, "building.s", "GLOBAL/SHAPES/building.s", mesh),
            new TrackShapeAssetMesh(11, "building.s", "GLOBAL/SHAPES/building.s", mesh)
        };
        var shapeDiscovery = new WorldShapeAssetDiscovery(
            new[]
            {
                new WorldShapeAssetReference(-1, 2, 10, "building.s", "Static", "GLOBAL/SHAPES/building.s"),
                new WorldShapeAssetReference(-1, 2, 11, "building.s", "TrackObj", "GLOBAL/SHAPES/building.s")
            }, Array.Empty<WorldShapeAssetIssue>());
        var textureData = new AceTextureData(1, 1, AceTexturePixelFormat.Rgba8, false,
            false, 0, new[] { new byte[] { 255, 255, 255, 255 } }, Array.Empty<AceTextureChannel>());
        var loadedTextures = new[]
        {
            new LoadedTrackShapeTexture(new TrackShapeTextureAssetReference(10, "rail.ace", null, "ROUTES/Test/TEXTURES/rail.ace"), textureData),
            new LoadedTrackShapeTexture(new TrackShapeTextureAssetReference(11, "rail.ace", null, "ROUTES/Test/TEXTURES/rail.ace"), textureData)
        };
        var textureDiscovery = new TrackShapeTextureAssetDiscovery(
            loadedTextures.Select(texture => texture.Reference), Array.Empty<TrackShapeTextureAssetIssue>());
        var textureResult = new TrackShapeTextureAssetLoadResult(loadedTextures,
            Array.Empty<TrackShapeTextureAssetIssue>(), Array.Empty<TrackShapeTextureLoadIssue>());
        var loadedTile = new LoadedWorldSceneryTile(tileData, placements, shapeDiscovery, assets,
            Array.Empty<WorldShapeMeshIssue>(), textureDiscovery, textureResult);

        var result = new WorldSceneryRenderGeometryBuilder().Build(new[] { loadedTile });

        Assert.False(result.WasTruncated);
        Assert.Single(result.Textures);
        var batch = Assert.Single(result.Batches);
        Assert.Equal(0, batch.TextureIndex);
        Assert.True(batch.AlphaTest);
        Assert.Equal(6, batch.Indices.Count);
        Assert.Equal(new[] { -2043f, 7f, 4105f, 0.1f, 0.2f }, batch.Vertices.Take(5));
        Assert.Equal(new[] { -2037f, 7f, 4119f, 0.1f, 0.2f }, batch.Vertices.Skip(15).Take(5));
    }
}

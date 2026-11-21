using System.Numerics;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TerrainMeshBuilderTests
{
    [Fact]
    public void BuildsOriginalOpenRailsPatchGridTextureCoordinatesAndTriangleWinding()
    {
        var (tile, patchSet) = CreateFlatTile(flags: null);

        var mesh = BuildWithClampedEdgeSamples(tile, patchSet);

        var patch = Assert.Single(mesh.Patches);
        Assert.Equal(289, patch.Vertices.Count);
        Assert.Equal(1536, patch.Indices.Count);
        Assert.Equal(new Vector3(-8, 10, -8), patch.Vertices[0].Position);
        Assert.Equal(Vector3.UnitY, patch.Vertices[0].Normal);
        Assert.Equal(new Vector2(0.1f, 0.2f), patch.Vertices[0].TextureCoordinate);
        Assert.Equal(new Vector2(0.26f, 0.52f), patch.Vertices[^1].TextureCoordinate);
        Assert.Equal(new[] { 0, 18, 17, 0, 1, 18 }, patch.Indices.Take(6));
        Assert.Equal(new Vector3(0, 10, 2048), patch.PatchOrigin);
        Assert.Equal(2048, patch.PatchSizeMeters);
        Assert.Equal(10, patch.AverageRelativeElevation);
        Assert.Equal("Grass", patch.Shader?.Name);
        Assert.Equal("grass.ace", Assert.Single(patch.Shader!.TextureSlots).FileName);
    }

    [Fact]
    public void OmitsTerrainTrianglesTouchingHiddenVertices()
    {
        var flags = new byte[33 * 33];
        flags[0] = 0x04;
        var (tile, patchSet) = CreateFlatTile(flags);

        var patch = Assert.Single(BuildWithClampedEdgeSamples(tile, patchSet).Patches);

        Assert.True(patch.Indices.Count < 1536);
        Assert.DoesNotContain(new[] { 0, 18, 17 }, patch.Indices.Chunk(3).Select(triangle => triangle.ToArray()),
            new IntArrayComparer());
    }

    private static TerrainTileMesh BuildWithClampedEdgeSamples(TerrainTileContent tile, TerrainPatchSet patchSet)
    {
        var resolver = new TerrainTileSampleResolver(new[] { tile });
        return new TerrainMeshBuilder().Build(tile, patchSet, resolver);
    }

    private static (TerrainTileContent Tile, TerrainPatchSet PatchSet) CreateFlatTile(byte[]? flags)
    {
        var samples = new TerrainSampleData
        {
            SampleCount = 33,
            Floor = 10,
            Scale = 0.5f,
            SampleSize = 1
        };
        var terrain = new TerrainTileData(
            "ROUTES/Test/TILES/tile.t",
            1,
            0,
            null,
            samples,
            Array.Empty<TerrainPatchSet>(),
            new[] { new TerrainShaderDefinition("Grass", new[] { new TerrainTextureSlot("grass.ace", 0, 0) }, Array.Empty<TerrainUvCalculation>()) });
        var altitude = new TerrainAltitudeData(33, Enumerable.Repeat((ushort)20, 33 * 33).ToArray());
        var flagData = flags is null ? null : new TerrainFlagsData(33, flags);
        var reference = new TerrainTileReference(
            1, -2,
            "ROUTES/Test/TILES/tile.t",
            "ROUTES/Test/TILES/tile_y.raw",
            flags is null ? null : "ROUTES/Test/TILES/tile_f.raw");
        var content = new TerrainTileContent(reference, terrain, altitude, flagData);
        var patch = new TerrainPatch(
            0,
            1024, 0, 1024,
            1, 0, 8,
            0,
            0.1f, 0.2f, 0.01f, 0, 0, 0.02f,
            0);
        var patchSet = new TerrainPatchSet(100, 1, new[] { patch });
        return (content, patchSet);
    }

    private sealed class IntArrayComparer : IEqualityComparer<int[]>
    {
        public bool Equals(int[]? x, int[]? y) => x is not null && y is not null && x.SequenceEqual(y);
        public int GetHashCode(int[] obj) => obj.Aggregate(17, (hash, value) => hash * 31 + value);
    }
}

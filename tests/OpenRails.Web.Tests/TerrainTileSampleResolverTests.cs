using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TerrainTileSampleResolverTests
{
    [Fact]
    public void ResolvesSamplesAcrossAllFourAdjacentTerrainTiles()
    {
        var origin = CreateTile(0, 0, 1);
        var east = CreateTile(1, 0, 2);
        var south = CreateTile(0, -1, 3);
        var north = CreateTile(0, 1, 4);
        var west = CreateTile(-1, 0, 5);
        var resolver = new TerrainTileSampleResolver(new[] { origin, east, south, north, west });

        Assert.Equal(2, resolver.GetElevationMeters(origin, 256, 0));
        Assert.Equal(3, resolver.GetElevationMeters(origin, 0, 256));
        Assert.Equal(4, resolver.GetElevationMeters(origin, 0, -1));
        Assert.Equal(5, resolver.GetElevationMeters(origin, -1, 0));
    }

    [Fact]
    public void ResolvesHiddenFlagsFromNeighborAndFallsBackToEdgeWhenNeighborIsMissing()
    {
        var origin = CreateTile(0, 0, 1);
        var eastFlags = new byte[256 * 256];
        eastFlags[0] = 0x04;
        var east = CreateTile(1, 0, 2, eastFlags);
        var resolver = new TerrainTileSampleResolver(new[] { origin, east });

        Assert.True(resolver.IsVertexHidden(origin, 256, 0));
        Assert.Equal(2, resolver.GetElevationMeters(origin, 256, 0));

        var fallback = new TerrainTileSampleResolver(new[] { origin });
        Assert.False(fallback.IsVertexHidden(origin, 256, 0));
        Assert.Equal(1, fallback.GetElevationMeters(origin, 256, 0));
    }

    private static TerrainTileContent CreateTile(int tileX, int tileZ, ushort height, byte[]? flags = null)
    {
        var samples = new TerrainSampleData
        {
            SampleCount = 256,
            Floor = 0,
            Scale = 1,
            SampleSize = 8
        };
        var terrain = new TerrainTileData(
            $"ROUTES/Test/TILES/{tileX}_{tileZ}.t",
            1,
            0,
            null,
            samples,
            Array.Empty<TerrainPatchSet>());
        var altitude = new TerrainAltitudeData(256, Enumerable.Repeat(height, 256 * 256).ToArray());
        var flagData = flags is null ? null : new TerrainFlagsData(256, flags);
        var reference = new TerrainTileReference(
            tileX,
            tileZ,
            terrain.SourcePath,
            terrain.SourcePath + "_y.raw",
            flags is null ? null : terrain.SourcePath + "_f.raw");
        return new TerrainTileContent(reference, terrain, altitude, flagData);
    }
}

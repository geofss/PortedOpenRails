using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TerrainLodSelectorTests
{
    private static TerrainTileMesh Level(int distance) =>
        new(new TerrainTileReference(0, 0, "tile.t", "tile_y.raw", null),
            distance, Array.Empty<TerrainPatchMesh>());

    [Fact]
    public void SelectsNearestDistanceLevelRegardlessOfSourceOrder()
    {
        var levels = new[] { Level(1000), Level(100), Level(400) };

        Assert.Equal(100, TerrainLodSelector.Select(levels, 50)!.LodDistance);
        Assert.Equal(400, TerrainLodSelector.Select(levels, 250)!.LodDistance);
        Assert.Equal(1000, TerrainLodSelector.Select(levels, 800)!.LodDistance);
        Assert.Equal(1000, TerrainLodSelector.Select(levels, 5000)!.LodDistance);
    }

    [Fact]
    public void EmptyLevelsReturnNullAndInvalidDistanceIsRejected()
    {
        Assert.Null(TerrainLodSelector.Select(Array.Empty<TerrainTileMesh>(), 10));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TerrainLodSelector.Select(new[] { Level(100) }, double.NaN));
    }
}

using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TerrainPatchMaterialBuilderTests
{
    [Fact]
    public void UsesPrimaryAndExplicitOverlayTexturesAndOverlayUvScale()
    {
        var terrain = CreateTerrain(
            new TerrainTextureSlot("grass.ace", 0, 0),
            new TerrainTextureSlot("rock.ace", 0, 0),
            overlayScale: 64);

        var material = new TerrainPatchMaterialBuilder().Build(terrain, 0, usesCustomIndexBuffer: false);

        Assert.NotNull(material);
        Assert.Equal("TerrainShared", material.MaterialName);
        Assert.Equal("terrain-shader", material.ShaderName);
        Assert.Equal("grass.ace", material.PrimaryTextureFileName);
        Assert.Equal("rock.ace", material.OverlayTextureFileName);
        Assert.Equal(64, material.OverlayScale);
    }

    [Fact]
    public void UsesImplicitMicrotextureWhenShaderHasOnlyOneTextureSlot()
    {
        var terrain = CreateTerrain(new TerrainTextureSlot("grass.ace", 0, 0));

        var material = new TerrainPatchMaterialBuilder().Build(terrain, 0, usesCustomIndexBuffer: true);

        Assert.NotNull(material);
        Assert.Equal("grass.ace", material.PrimaryTextureFileName);
        Assert.Equal("microtex.ace", material.OverlayTextureFileName);
        Assert.Equal(32, material.OverlayScale);
        Assert.Equal("Terrain", material.MaterialName);
    }

    [Fact]
    public void ReturnsNullForMissingShaderIndex()
    {
        var terrain = CreateTerrain(new TerrainTextureSlot("grass.ace", 0, 0));

        Assert.Null(new TerrainPatchMaterialBuilder().Build(terrain, 1, usesCustomIndexBuffer: false));
        Assert.Null(new TerrainPatchMaterialBuilder().Build(terrain, -1, usesCustomIndexBuffer: false));
    }

    private static TerrainTileData CreateTerrain(params TerrainTextureSlot[] slots)
    {
        var shader = new TerrainShaderDefinition(
            "terrain-shader",
            slots,
            new[]
            {
                new TerrainUvCalculation(0, 0, 0, 1),
                new TerrainUvCalculation(0, 0, 0, 64)
            });
        return new TerrainTileData(
            "ROUTES/Test/TILES/test.t",
            1,
            0,
            null,
            new TerrainSampleData(),
            Array.Empty<TerrainPatchSet>(),
            new[] { shader });
    }
}

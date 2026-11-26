using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TerrainTextureAssetLocatorTests
{
    [Fact]
    public void ResolvesTerrainTexturesFromDefaultAndSeasonalTerrTexFolders()
    {
        var tile = CreateTile(new TerrainShaderDefinition(
            "BlendATexDiff",
            new[]
            {
                new TerrainTextureSlot("grass.ace", 0, 0),
                new TerrainTextureSlot("rock.ace", 1, 0)
            },
            Array.Empty<TerrainUvCalculation>()));
        using var store = CreateStore(
            ("ROUTES/Test/TERRTEX/grass.ace", "base grass"),
            ("ROUTES/Test/TERRTEX/Winter/grass.ace", "winter grass"),
            ("ROUTES/Test/TERRTEX/rock.ace", "base rock"));

        var result = new TerrainTextureAssetLocator().Locate(store, "ROUTES/Test", new[] { tile });

        Assert.Equal(3, result.Found.Count);
        Assert.Contains(result.Found, item => item.FileName == "grass.ace" && item.SeasonFolder is null);
        Assert.Contains(result.Found, item => item.FileName == "grass.ace" && item.SeasonFolder == "Winter");
        Assert.Contains(result.Found, item => item.FileName == "rock.ace" && item.SeasonFolder is null);
        Assert.Empty(result.Missing);
    }

    [Fact]
    public void AddsImplicitMicrotextureOverlayForSingleSlotTerrainShaders()
    {
        var tile = CreateTile(new TerrainShaderDefinition(
            "BlendATexDiff",
            new[] { new TerrainTextureSlot("ground.ace", 0, 0) },
            Array.Empty<TerrainUvCalculation>()));
        using var store = CreateStore(
            ("ROUTES/Test/TERRTEX/ground.ace", "ground"),
            ("ROUTES/Test/TERRTEX/microtex.ace", "micro"));

        var result = new TerrainTextureAssetLocator().Locate(store, "ROUTES/Test", new[] { tile });

        Assert.Equal(2, result.Found.Count);
        Assert.Contains(result.Found, item => item.FileName == "microtex.ace" && item.IsImplicitOverlay);
        Assert.Empty(result.Missing);
    }

    [Fact]
    public void ReportsMissingAndUnsafeTerrainTexturePaths()
    {
        var tile = CreateTile(new TerrainShaderDefinition(
            "BlendATexDiff",
            new[]
            {
                new TerrainTextureSlot("missing.ace", 0, 0),
                new TerrainTextureSlot("../outside.ace", 1, 0)
            },
            Array.Empty<TerrainUvCalculation>()));
        using var store = CreateStore(("ROUTES/Test/TERRTEX/other.ace", "texture"));

        var result = new TerrainTextureAssetLocator().Locate(store, "ROUTES/Test", new[] { tile });

        Assert.Empty(result.Found);
        Assert.Equal(2, result.Missing.Count);
        Assert.Contains(result.Missing, item => item.FileName == "missing.ace" && item.Reason.Contains("not found"));
        Assert.Contains(result.Missing, item => item.FileName == "../outside.ace" && item.Reason.Contains("safe"));
    }

    private static TerrainTileContent CreateTile(TerrainShaderDefinition shader)
    {
        var samples = new TerrainSampleData { SampleCount = 1, Floor = 0, Scale = 1, SampleSize = 1 };
        var terrain = new TerrainTileData(
            "ROUTES/Test/TILES/tile.t", 1, 0, null, samples,
            Array.Empty<TerrainPatchSet>(), new[] { shader });
        var reference = new TerrainTileReference(
            0, 0, terrain.SourcePath, terrain.SourcePath + "_y.raw", null);
        return new TerrainTileContent(reference, terrain, new TerrainAltitudeData(1, new ushort[] { 0 }), null);
    }

    private static ZipContentStore CreateStore(params (string Path, string Content)[] files)
    {
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Path);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(file.Content);
            }
        }
        memory.Position = 0;
        return new ZipContentStore(memory);
    }
}

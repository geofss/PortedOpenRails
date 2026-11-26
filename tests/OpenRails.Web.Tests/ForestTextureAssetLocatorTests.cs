using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class ForestTextureAssetLocatorTests
{
    [Fact]
    public void DiscoversBaseAndAllOriginalSeasonalForestTextureVariants()
    {
        using var store = CreateStore(
            "ROUTES/Test/TEXTURES/trees.ace",
            "ROUTES/Test/TEXTURES/Spring/trees.ace",
            "ROUTES/Test/TEXTURES/Autumn/trees.ace",
            "ROUTES/Test/TEXTURES/Winter/trees.ace",
            "ROUTES/Test/TEXTURES/SpringSnow/trees.ace",
            "ROUTES/Test/TEXTURES/AutumnSnow/trees.ace",
            "ROUTES/Test/TEXTURES/WinterSnow/trees.ace");

        var forest = new WorldObjectPlacement(10, "Forest", null,
            new WorldTilePosition(0, 0, 0), null, null, 0, 0, null, 0, 0, null,
            "trees.ace");
        var result = new ForestTextureAssetLocator().Locate(store, "ROUTES/Test", new[] { forest });

        Assert.Equal(7, result.Found.Count);
        Assert.Empty(result.Missing);
        Assert.Contains(result.Found, item => item.SeasonFolder is null);
        Assert.Contains(result.Found, item => item.SeasonFolder == "Spring");
        Assert.Contains(result.Found, item => item.SeasonFolder == "Autumn");
        Assert.Contains(result.Found, item => item.SeasonFolder == "Winter");
        Assert.Contains(result.Found, item => item.SeasonFolder == "SpringSnow");
        Assert.Contains(result.Found, item => item.SeasonFolder == "AutumnSnow");
        Assert.Contains(result.Found, item => item.SeasonFolder == "WinterSnow");
    }

    [Fact]
    public void RejectsUnsafeForestTexturePathsAndReportsMissingAssets()
    {
        using var store = CreateStore();
        var unsafeForest = new WorldObjectPlacement(1, "Forest", null,
            new WorldTilePosition(0, 0, 0), null, null, 0, 0, null, 0, 0, null,
            "../outside.ace");
        var missingForest = unsafeForest with { Uid = 2, TreeTexture = "missing.ace" };

        var result = new ForestTextureAssetLocator().Locate(store, "ROUTES/Test",
            new[] { unsafeForest, missingForest });

        Assert.Equal(2, result.Missing.Count);
        Assert.Contains(result.Missing, item => item.ShapeIndex == 1 && item.Reason.Contains("safe"));
        Assert.Contains(result.Missing, item => item.ShapeIndex == 2 && item.Reason.Contains("not found"));
    }

    private static ZipContentStore CreateStore(params string[] paths)
    {
        var memory = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(memory,
            System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var path in paths)
            {
                var entry = archive.CreateEntry(path);
                using var output = entry.Open();
                output.WriteByte(0);
            }
        }
        memory.Position = 0;
        return new ZipContentStore(memory);
    }
}

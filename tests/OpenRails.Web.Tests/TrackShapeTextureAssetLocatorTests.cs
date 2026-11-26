using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackShapeTextureAssetLocatorTests
{
    [Fact]
    public void ResolvesShapePrimitiveTexturesAndSeasonalVariants()
    {
        var asset = CreateShapeAsset("rail.ace", "ballast.ace");
        using var store = CreateStore(
            ("ROUTES/Test/TEXTURES/rail.ace", "rail"),
            ("ROUTES/Test/TEXTURES/Winter/rail.ace", "winter rail"),
            ("ROUTES/Test/TEXTURES/ballast.ace", "ballast"));

        var result = new TrackShapeTextureAssetLocator().Locate(store, "ROUTES/Test", new[] { asset });

        Assert.Equal(3, result.Found.Count);
        Assert.Contains(result.Found, item => item.ImageName == "rail.ace" && item.SeasonFolder is null);
        Assert.Contains(result.Found, item => item.ImageName == "rail.ace" && item.SeasonFolder == "Winter");
        Assert.Contains(result.Found, item => item.ImageName == "ballast.ace" && item.SeasonFolder is null);
        Assert.Empty(result.Missing);
    }

    [Fact]
    public void ReportsMissingAndUnsafeShapeTextureReferences()
    {
        var asset = CreateShapeAsset("missing.ace", "../outside.ace");
        using var store = CreateStore(("ROUTES/Test/TEXTURES/other.ace", "other"));

        var result = new TrackShapeTextureAssetLocator().Locate(store, "ROUTES/Test", new[] { asset });

        Assert.Empty(result.Found);
        Assert.Equal(2, result.Missing.Count);
        Assert.Contains(result.Missing, item => item.ImageName == "missing.ace" && item.Reason.Contains("not found"));
        Assert.Contains(result.Missing, item => item.ImageName == "../outside.ace" && item.Reason.Contains("safe"));
    }

    [Fact]
    public void ResolvesGlobalShapeTextureInsideDemoModelParentFolder()
    {
        var asset = CreateShapeAsset("rail.ace");
        using var store = CreateStore(("Demo Model 1/GLOBAL/TEXTURES/rail.ace", "rail"));

        var result = new TrackShapeTextureAssetLocator().Locate(store,
            "Demo Model 1/ROUTES/SCE", new[] { asset });

        Assert.Equal("Demo Model 1/GLOBAL/TEXTURES/rail.ace", Assert.Single(result.Found).Path);
        Assert.Empty(result.Missing);
    }

    private static TrackShapeAssetMesh CreateShapeAsset(params string[] imageNames)
    {
        var primitives = imageNames.Select(name => new TrackShapePrimitiveMesh(
            0, 0, 0, 0, new[] { -1 }, Array.Empty<int>(), "Tex", name,
            0, 0, 0, false, 0)).ToArray();
        var subObject = new TrackShapeSubObjectMesh(
            Array.Empty<TrackShapeMeshVertex>(),
            Array.Empty<int>(),
            primitives);
        var lod = new TrackShapeLodMesh(1000, new[] { -1 }, new[] { subObject });
        var mesh = new TrackShapeMesh(0, 0, Array.Empty<TrackShapeMatrix>(), new[] { lod });
        return new TrackShapeAssetMesh(5, "track.s", "GLOBAL/SHAPES/track.s", mesh);
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

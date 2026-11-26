using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class WorldShapeAssetLocatorTests
{
    [Fact]
    public void ResolvesStaticAndTrackObjectShapesFromRouteAndGlobalContent()
    {
        var tile = new WorldTileData(
            "ROUTES/Test/WORLD/W+000000+000000.w", 0, 0,
            new[] { new StaticWorldObject(1, "building.s", null, null, null, 0, 0) },
            new[] { new TrackWorldObject(2, 0, 0, 0, "signal.s", null, null, 0, 0, null) },
            Array.Empty<string>());
        using var store = CreateStore(
            ("ROUTES/Test/SHAPES/building.s", "route building"),
            ("GLOBAL/SHAPES/signal.s", "global signal"));

        var result = new WorldShapeAssetLocator().Locate(store, "ROUTES/Test", new[] { tile });

        Assert.Equal(2, result.Found.Count);
        Assert.Contains(result.Found, item => item.ObjectUid == 1 && item.Path == "ROUTES/Test/SHAPES/building.s");
        Assert.Contains(result.Found, item => item.ObjectUid == 2 && item.Path == "GLOBAL/SHAPES/signal.s");
        Assert.Empty(result.Missing);
    }

    [Fact]
    public void PreservesSpecializedWorldObjectKindDuringShapeDiscovery()
    {
        var tile = new WorldTileData(
            "ROUTES/Test/WORLD/W+000000+000000.w", 0, 0,
            new[] { new StaticWorldObject(7, "signal.s", null, null, null, 0, 0, "Signal") },
            Array.Empty<TrackWorldObject>(),
            Array.Empty<string>());
        using var store = CreateStore(("GLOBAL/SHAPES/signal.s", "signal shape"));

        var result = new WorldShapeAssetLocator().Locate(store, "ROUTES/Test", new[] { tile });

        var asset = Assert.Single(result.Found);
        Assert.Equal("Signal", asset.ObjectKind);
        Assert.Equal((uint)7, asset.ObjectUid);
    }

    [Fact]
    public void ReportsMissingAndUnsafeWorldObjectShapeReferences()
    {
        var tile = new WorldTileData(
            "ROUTES/Test/WORLD/W+000000+000000.w", 0, 0,
            new[]
            {
                new StaticWorldObject(1, "missing.s", null, null, null, 0, 0),
                new StaticWorldObject(2, "../escape.s", null, null, null, 0, 0)
            },
            Array.Empty<TrackWorldObject>(),
            Array.Empty<string>());
        using var store = CreateStore(("GLOBAL/SHAPES/other.s", "shape"));

        var result = new WorldShapeAssetLocator().Locate(store, "ROUTES/Test", new[] { tile });

        Assert.Empty(result.Found);
        Assert.Equal(2, result.Missing.Count);
        Assert.Contains(result.Missing, item => item.ObjectUid == 1 && item.Reason.Contains("not found"));
        Assert.Contains(result.Missing, item => item.ObjectUid == 2 && item.Reason.Contains("safe"));
    }

    [Fact]
    public void ResolvesGlobalWorldShapeInsideDemoModelParentFolder()
    {
        var tile = new WorldTileData(
            "Demo Model 1/ROUTES/SCE/WORLD/W+000000+000000.w", 0, 0,
            new[] { new StaticWorldObject(1, "building.s", null, null, null, 0, 0) },
            Array.Empty<TrackWorldObject>(), Array.Empty<string>());
        using var store = CreateStore(("Demo Model 1/GLOBAL/SHAPES/building.s", "global building"));

        var result = new WorldShapeAssetLocator().Locate(store,
            "Demo Model 1/ROUTES/SCE", new[] { tile });

        Assert.Equal("Demo Model 1/GLOBAL/SHAPES/building.s", Assert.Single(result.Found).Path);
        Assert.Empty(result.Missing);
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

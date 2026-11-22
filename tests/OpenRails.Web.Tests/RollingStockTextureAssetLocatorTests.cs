using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class RollingStockTextureAssetLocatorTests
{
    [Fact]
    public void ResolvesImagesBesideVehicleAndInTrainsetTexturesFolder()
    {
        var assets = new[]
        {
            CreateShapeAsset(12, "body.ace"),
            CreateShapeAsset(13, "details/wheels.ace")
        };
        using var store = CreateStore(
            ("TRAINS/TRAINSET/Freight/body.ace", "body"),
            ("TRAINS/TRAINSET/Freight/TEXTURES/details/wheels.ace", "wheels"));

        var result = new RollingStockTextureAssetLocator().Locate(store, "Freight", assets);

        Assert.Equal(2, result.Found.Count);
        Assert.Contains(result.Found, item => item.ShapeIndex == 12 && item.Path.EndsWith("/body.ace"));
        Assert.Contains(result.Found, item => item.ShapeIndex == 13 && item.Path.EndsWith("/wheels.ace"));
        Assert.Empty(result.Missing);
    }

    [Fact]
    public void ReportsMissingAndRejectsUnsafeTexturePaths()
    {
        var asset = CreateShapeAsset(7, "missing.ace", "../outside.ace");
        using var store = CreateStore(("TRAINS/TRAINSET/Freight/other.ace", "other"));

        var result = new RollingStockTextureAssetLocator().Locate(store, "Freight", new[] { asset });

        Assert.Empty(result.Found);
        Assert.Equal(2, result.Missing.Count);
        Assert.Contains(result.Missing, issue => issue.ImageName == "missing.ace" && issue.Reason.Contains("not found"));
        Assert.Contains(result.Missing, issue => issue.ImageName == "../outside.ace" && issue.Reason.Contains("safe"));
    }

    private static TrackShapeAssetMesh CreateShapeAsset(uint shapeIndex, params string[] imageNames)
    {
        var primitives = imageNames.Select(name => new TrackShapePrimitiveMesh(
            0, 0, 0, 0, new[] { -1 }, Array.Empty<int>(), "Tex", name,
            0, 0, 0, false, 0)).ToArray();
        var subObject = new TrackShapeSubObjectMesh(
            Array.Empty<TrackShapeMeshVertex>(), Array.Empty<int>(), primitives);
        var lod = new TrackShapeLodMesh(1000, new[] { -1 }, new[] { subObject });
        var mesh = new TrackShapeMesh(0, 0, Array.Empty<TrackShapeMatrix>(), new[] { lod });
        return new TrackShapeAssetMesh(shapeIndex, "vehicle.s", "TRAINS/TRAINSET/Freight/vehicle.s", mesh);
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

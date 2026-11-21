using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackShapeAssetLocatorTests
{
    [Fact]
    public void PrefersRouteShapeAssetOverGlobalAsset()
    {
        var shape = new TrackShapeDefinition(7, "track.s", 0, 0, 0,
            Array.Empty<TrackShapePath>(), false, false, false);
        var network = CreateNetwork(shape);
        using var store = CreateStore(
            ("ROUTES/Test/SHAPES/track.s", "route shape"),
            ("GLOBAL/SHAPES/track.s", "global shape"));

        var result = new TrackShapeAssetLocator().Locate(store, "ROUTES/Test", network);

        Assert.Equal("ROUTES/Test/SHAPES/track.s", Assert.Single(result.Found).Path);
        Assert.Empty(result.Missing);
    }

    [Fact]
    public void ReportsMissingAndUnsafeShapePathsWithoutOpeningThem()
    {
        var missing = new TrackShapeDefinition(7, "missing.s", 0, 0, 0,
            Array.Empty<TrackShapePath>(), false, false, false);
        var unsafeShape = new TrackShapeDefinition(8, "../outside.s", 0, 0, 0,
            Array.Empty<TrackShapePath>(), false, false, false);
        var network = CreateNetwork(missing, unsafeShape);
        using var store = CreateStore(("GLOBAL/SHAPES/other.s", "shape"));

        var result = new TrackShapeAssetLocator().Locate(store, "ROUTES/Test", network);

        Assert.Empty(result.Found);
        Assert.Equal(2, result.Missing.Count);
        Assert.Contains(result.Missing, item => item.ShapeIndex == 8 && item.Reason.Contains("safe"));
        Assert.Contains(result.Missing, item => item.ShapeIndex == 7 && item.Reason.Contains("not found"));
    }

    private static TrackNetwork CreateNetwork(params TrackShapeDefinition[] shapes)
    {
        var source = new TrackVectorSection(1, 7, 0, 0, 0, 0, 1, "00", 0, 0, 0, 0, 0, 0, 0, 0);
        var geometry = new TrackSectionGeometry(1, null, 10, null, null, false);
        var placements = shapes.Select(shape => new TrackSectionPlacement(
            source with { ShapeIndex = shape.ShapeIndex }, geometry, new TrackWorldPosition(0, 0, 0), 10, shape));
        var path = new TrackVectorPath(1, new[] { 2, 3 }, placements, placements.Sum(p => p.LengthMeters));
        var vector = new TrackNode(1, TrackNodeKind.Vector, null, null, new[] { source },
            new[] { new TrackPin(2, 0), new TrackPin(3, 1) });
        var endA = new TrackNode(2, TrackNodeKind.End, null, null, Array.Empty<TrackVectorSection>(), Array.Empty<TrackPin>());
        var endB = new TrackNode(3, TrackNodeKind.End, null, null, Array.Empty<TrackVectorSection>(), Array.Empty<TrackPin>());
        var database = new TrackDatabase(new TrackNode?[] { null, vector, endA, endB }, Array.Empty<string>());
        return new TrackNetwork(database, new[] { path }, Array.Empty<string>());
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

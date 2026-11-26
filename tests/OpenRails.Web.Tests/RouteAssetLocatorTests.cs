using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class RouteAssetLocatorTests
{
    [Fact]
    public void LocatesTrackDatabaseAndGlobalAndRouteTrackSectionFiles()
    {
        using var store = CreateStore(
            ("ROUTES/TestRoute/Test.trk", "Tr_RouteFile ( )"),
            ("ROUTES/TestRoute/TDB/TestDatabase.tdb", "TrackDB ( )"),
            ("ROUTES/TestRoute/TSECTION.DAT", "TrackSections ( )"),
            ("GLOBAL/TSECTION.DAT", "TrackSections ( )"));

        var metadata = new RouteMetadata(
            "TESTROUTE", "Test Route", "Fixture", false, 0,
            new RouteStartCoordinates(0, 0, 0, 0), "TestDatabase");

        var assets = new RouteAssetLocator().Locate(store, "ROUTES/TestRoute/Test.trk", metadata);

        Assert.Equal("ROUTES/TestRoute", assets.RouteRoot);
        Assert.Equal("ROUTES/TestRoute/TDB/TestDatabase.tdb", assets.TrackDatabasePath);
        Assert.Equal("GLOBAL/TSECTION.DAT", assets.GlobalTrackSectionsPath);
        Assert.Equal("ROUTES/TestRoute/TSECTION.DAT", assets.RouteTrackSectionsPath);
    }

    [Fact]
    public void UsesRouteIdWhenTrkHasNoFileName()
    {
        using var store = CreateStore(
            ("ROUTES/TestRoute/Test.trk", "Tr_RouteFile ( )"),
            ("ROUTES/TestRoute/TestRoute.tdb", "TrackDB ( )"),
            ("GLOBAL/TSECTION.DAT", "TrackSections ( )"));

        var metadata = new RouteMetadata(
            "TestRoute", "Test Route", "Fixture", false, 0,
            new RouteStartCoordinates(0, 0, 0, 0));

        var assets = new RouteAssetLocator().Locate(store, "ROUTES/TestRoute/Test.trk", metadata);

        Assert.Equal("ROUTES/TestRoute/TestRoute.tdb", assets.TrackDatabasePath);
        Assert.Null(assets.RouteTrackSectionsPath);
    }

    [Fact]
    public void RejectsMissingSelectedTrkFile()
    {
        using var store = CreateStore(
            ("ROUTES/TestRoute/TestRoute.tdb", "TrackDB ( )"),
            ("GLOBAL/TSECTION.DAT", "TrackSections ( )"));

        var metadata = new RouteMetadata(
            "TestRoute", "Test Route", "Fixture", false, 0,
            new RouteStartCoordinates(0, 0, 0, 0));

        Assert.Throws<FileNotFoundException>(() =>
            new RouteAssetLocator().Locate(store, "ROUTES/TestRoute/missing.trk", metadata));
    }

    [Fact]
    public void RejectsAmbiguousRouteSpecificTrackSectionFiles()
    {
        using var store = CreateStore(
            ("ROUTES/TestRoute/Test.trk", "Tr_RouteFile ( )"),
            ("ROUTES/TestRoute/TestRoute.tdb", "TrackDB ( )"),
            ("ROUTES/TestRoute/TSECTION.DAT", "TrackSections ( )"),
            ("ROUTES/TestRoute/Sub/TSECTION.DAT", "TrackSections ( )"),
            ("GLOBAL/TSECTION.DAT", "TrackSections ( )"));

        var metadata = new RouteMetadata(
            "TestRoute", "Test Route", "Fixture", false, 0,
            new RouteStartCoordinates(0, 0, 0, 0));

        Assert.Throws<InvalidDataException>(() =>
            new RouteAssetLocator().Locate(store, "ROUTES/TestRoute/Test.trk", metadata));
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

using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class RouteDiscoveryServiceTests
{
    [Fact]
    public void DiscoversMultipleValidTrkFilesInStablePathOrder()
    {
        using var store = CreateStore(
            ("ROUTES/Zulu/ZULU.trk", Route("ZULU", "Zulu Route")),
            ("ROUTES/Alpha/ALPHA.trk", Route("ALPHA", "Alpha Route")),
            ("ROUTES/Alpha/track.tdb", "TrackDB ( )"));

        var result = new RouteDiscoveryService().Discover(store);

        Assert.Equal(2, result.Routes.Count);
        Assert.Equal("ROUTES/Alpha/ALPHA.trk", result.Routes[0].MetadataFile);
        Assert.Equal("Alpha Route", result.Routes[0].Metadata.Name);
        Assert.Equal("ROUTES/Zulu/ZULU.trk", result.Routes[1].MetadataFile);
        Assert.Empty(result.Failures);
        Assert.True(result.HasRoutes);
    }

    [Fact]
    public void ReportsInvalidTrkFilesAndContinuesDiscoveringValidRoutes()
    {
        using var store = CreateStore(
            ("ROUTES/Broken/BROKEN.trk", "SIMISA@@@@@@@@@@JINX0T0t\nNotARoute ( )"),
            ("ROUTES/Good/GOOD.trk", Route("GOOD", "Good Route")));

        var result = new RouteDiscoveryService().Discover(store);

        var route = Assert.Single(result.Routes);
        Assert.Equal("GOOD", route.Metadata.RouteId);
        var failure = Assert.Single(result.Failures);
        Assert.Equal("ROUTES/Broken/BROKEN.trk", failure.MetadataFile);
        Assert.False(string.IsNullOrWhiteSpace(failure.Reason));
    }

    [Fact]
    public void ReturnsEmptyResultWhenNoTrkFilesExist()
    {
        using var store = CreateStore(("GLOBAL/tsection.dat", "TrackSections ( )"));

        var result = new RouteDiscoveryService().Discover(store);

        Assert.Empty(result.Routes);
        Assert.Empty(result.Failures);
        Assert.False(result.HasRoutes);
    }

    private static string Route(string id, string name) => $$"""
        SIMISA@@@@@@@@@@JINX0T0t
        Tr_RouteFile (
            RouteID ( "{{id}}" )
            Name ( "{{name}}" )
            Description ( "Route discovery fixture" )
            Electrified ( 1 )
            MaxLineVoltage ( 25000 )
            RouteStart ( 1 2 3 4 )
        )
        """;

    private static ZipContentStore CreateStore(params (string Path, string Content)[] files)
    {
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Path);
                using var stream = entry.Open();
                var bytes = Encoding.UTF8.GetBytes(file.Content);
                stream.Write(bytes);
            }
        }

        memory.Position = 0;
        return new ZipContentStore(memory);
    }
}

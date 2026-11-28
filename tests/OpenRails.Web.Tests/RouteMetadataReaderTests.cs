using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class RouteMetadataReaderTests
{
    [Fact]
    public void ReadsRequiredRouteIdentityAndStartCoordinatesFromTrkFile()
    {
        using var store = CreateStore(("ROUTES/TestRoute/TEST.trk", """
            SIMISA@@@@@@@@@@JINX0T0t
            Tr_RouteFile (
                RouteID ( "TESTROUTE" )
                Name ( "Test Route" )
                Description ( "A representative route fixture" )
                FileName ( "TESTDB" )
                Electrified ( 1 )
                MaxLineVoltage ( 25000 )
                RouteStart ( 12 34 56.5 78.5 )
            )
            """));

        var metadata = new RouteMetadataReader().Read(store, "ROUTES/TestRoute/TEST.trk");

        Assert.Equal("TESTROUTE", metadata.RouteId);
        Assert.Equal("Test Route", metadata.Name);
        Assert.Equal("A representative route fixture", metadata.Description);
        Assert.Equal("TESTDB", metadata.FileName);
        Assert.True(metadata.Electrified);
        Assert.Equal(25000, metadata.MaxLineVoltage);
        Assert.Equal(new RouteStartCoordinates(12, 34, 56.5, 78.5), metadata.RouteStart);
    }

    [Fact]
    public void ReadsUtf16BomRouteMetadataLikeRealMstsRoutePackages()
    {
        const string routeText = """
            SIMISA@@@@@@@@@@JINX0r0t
            Tr_RouteFile (
                RouteID ( SCE )
                Name ( "Scottish Capital Express" )
                Description ( "A UTF-16 route metadata fixture with multiple fields." )
                FileName ( SCE )
                Electrified ( 00000000 )
                RouteStart ( -6144 15144 877.176 -869.309 )
            )
            """;
        var routeBytes = Encoding.Unicode.GetPreamble()
            .Concat(Encoding.Unicode.GetBytes(routeText))
            .ToArray();
        using var store = CreateStoreBytes(("ROUTES/SCE/SCE.trk", routeBytes));

        var metadata = new RouteMetadataReader().Read(store, "ROUTES/SCE/SCE.trk");

        Assert.Equal("SCE", metadata.RouteId);
        Assert.Equal("Scottish Capital Express", metadata.Name);
        Assert.Equal("SCE", metadata.FileName);
        Assert.False(metadata.Electrified);
        Assert.Equal(new RouteStartCoordinates(-6144, 15144, 877.176, -869.309), metadata.RouteStart);
    }

    [Theory]
    [InlineData("NaN 0 0 0")]
    [InlineData("0 Infinity 0 0")]
    [InlineData("1e308 0 1e308 0")]
    public void RejectsNonFiniteOrUnrepresentableRouteStart(string coordinates)
    {
        using var store = CreateStore(("ROUTES/TestRoute/TEST.trk", $"""
            SIMISA@@@@@@@@@@JINX0T0t
            Tr_RouteFile (
                RouteID ( "TESTROUTE" )
                Name ( "Test Route" )
                Description ( "Invalid route start fixture" )
                RouteStart ( {coordinates} )
            )
            """));

        Assert.Throws<InvalidDataException>(() =>
            new RouteMetadataReader().Read(store, "ROUTES/TestRoute/TEST.trk"));
    }

    [Fact]
    public void RejectsRouteFileWithoutTrRouteFileBlock()
    {
        using var store = CreateStore(("ROUTES/TestRoute/TEST.trk", """
            SIMISA@@@@@@@@@@JINX0T0t
            SomeOtherRoot ( value )
            """));

        Assert.Throws<InvalidDataException>(() =>
            new RouteMetadataReader().Read(store, "ROUTES/TestRoute/TEST.trk"));
    }

    private static ZipContentStore CreateStoreBytes(params (string Path, byte[] Content)[] files)
    {
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Path);
                using var stream = entry.Open();
                stream.Write(file.Content);
            }
        }

        memory.Position = 0;
        return new ZipContentStore(memory);
    }

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

using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class RoutePathFileReaderTests
{
    [Fact]
    public void PreservesLinkedMainAndSidingTopologyAndPdpCoordinates()
    {
        using var store = CreateStore(("ROUTES/RouteA/PATHS/Main.pat", """
            SIMISA@@@@@@@@@@JINX0P0t______
            Serial ( 1 )
            TrackPDPs (
                TrackPDP ( -12557 14761 -6.1 1173.7 72.5 2 0 )
                TrackPDP ( -12557 14761 -204.3 1173.7 976.0 2 0 )
                TrackPDP ( -12557 14762 -287.2 1173.7 -971.7 1 1 )
            )
            TrackPath (
                TrPathName ( MainLine )
                Name ( "Main Line" )
                TrPathStart ( Station A )
                TrPathEnd ( Station B )
                TrPathFlags ( 00000000 )
                TrPathNodes ( 3
                    TrPathNode ( 00000000 1 4294967295 0 )
                    TrPathNode ( 00000002 2 4294967295 1 )
                    TrPathNode ( 00000001 4294967295 0 2 )
                )
            )
            """));

        var path = new RoutePathFileReader().Read(store, "ROUTES/RouteA/PATHS/Main.pat");

        Assert.Equal("MainLine", path.PathId);
        Assert.Equal("Main Line", path.Name);
        Assert.Equal(3, path.TrackPoints.Count);
        Assert.Equal(-12557, path.TrackPoints[0].TileX);
        Assert.Equal(1u, path.Nodes[0].NextMainNode);
        Assert.False(path.Nodes[0].HasNextSidingNode);
        Assert.True(path.Nodes[1].IsWaitingPoint);
        Assert.True(path.Nodes[2].IsReversalPoint);
        Assert.True(path.IsPlayerPath);
        Assert.Empty(path.Diagnostics);
    }

    [Fact]
    public void ReportsOutOfRangeLinksAndMissingPdpReferences()
    {
        using var store = CreateStore(("ROUTES/RouteA/PATHS/Broken.pat", """
            SIMISA@@@@@@@@@@JINX0P0t______
            TrackPDPs ( TrackPDP ( 0 0 0 0 0 1 1 ) )
            TrackPath (
                TrPathName ( Broken )
                Name ( Broken )
                TrPathNodes ( 1
                    TrPathNode ( 00000000 5 4294967295 8 )
                )
            )
            """));

        var path = new RoutePathFileReader().Read(store, "ROUTES/RouteA/PATHS/Broken.pat");

        Assert.Equal(2, path.Diagnostics.Count);
        Assert.Contains(path.Diagnostics, item => item.Contains("successor 5"));
        Assert.Contains(path.Diagnostics, item => item.Contains("TrackPDP 8"));
    }

    [Fact]
    public void RejectsDeclaredNodeCountMismatch()
    {
        using var store = CreateStore(("ROUTES/RouteA/PATHS/Short.pat", """
            SIMISA@@@@@@@@@@JINX0P0t______
            TrackPDPs ( TrackPDP ( 0 0 0 0 0 1 1 ) )
            TrackPath (
                TrPathName ( Short )
                Name ( Short )
                TrPathNodes ( 2 TrPathNode ( 00000000 4294967295 4294967295 0 ) )
            )
            """));
        Assert.Throws<InvalidDataException>(() =>
            new RoutePathFileReader().Read(store, "ROUTES/RouteA/PATHS/Short.pat"));
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

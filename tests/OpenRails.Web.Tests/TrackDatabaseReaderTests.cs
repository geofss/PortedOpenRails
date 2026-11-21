using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackDatabaseReaderTests
{
    [Fact]
    public void ReadsTrackNodesVectorSectionsPinsAndJunctionMetadata()
    {
        using var store = CreateStore(("ROUTES/Test/TEST.tdb", """
            SIMISA@@@@@@@@@@JINX0t1t
            TrackDB (
              TrackNodes ( 3
                TrackNode ( 1
                  UiD ( 0 0 10 0 1000 1000 0 0 0 0 0 0 )
                  TrEndNode ( )
                  TrPins ( 1 0 TrPin ( 2 0 ) )
                )
                TrackNode ( 2
                  TrVectorNode (
                    TrVectorSections ( 2
                      1 0 1000 1000 12 0 1 "00" 1000 1000 0 0 0 0 0 0
                      2 0 1000 1000 13 0 1 "00" 1000 1000 0 0 25 0 0 0
                    )
                    TrItemRefs ( 0 )
                  )
                  TrPins ( 1 1 TrPin ( 1 1 ) TrPin ( 3 0 ) )
                )
                TrackNode ( 3
                  UiD ( 0 0 11 0 1000 1025 0 0 25 0 0 0 )
                  TrJunctionNode ( "switchshape" 4 )
                  TrPins ( 2 1 TrPin ( 2 1 ) TrPin ( 2 0 ) TrPin ( 2 1 ) )
                )
              )
              TrItemTable ( 0 )
            )
            """));

        var database = new TrackDatabaseReader().Read(store, "ROUTES/Test/TEST.tdb");

        Assert.Equal(3, database.NodeCount);
        Assert.Equal(TrackNodeKind.End, database.GetRequiredNode(1).Kind);
        Assert.Equal(TrackNodeKind.Vector, database.GetRequiredNode(2).Kind);
        Assert.Equal(TrackNodeKind.Junction, database.GetRequiredNode(3).Kind);

        var vector = database.GetRequiredNode(2);
        Assert.Equal(2, vector.Sections.Count);
        Assert.Equal((uint)1, vector.Sections[0].SectionIndex);
        Assert.Equal((uint)2, vector.Sections[1].SectionIndex);
        Assert.Equal(25, vector.Sections[1].Z);

        var junction = database.GetRequiredNode(3).Junction;
        Assert.NotNull(junction);
        Assert.Equal("switchshape", junction.ShapeName);
        Assert.Equal((uint)4, junction.ShapeIndex);

        Assert.Equal(5, database.EnumerateConnections().Count());
        Assert.Empty(database.Diagnostics);
    }

    [Fact]
    public void KeepsDatabaseReadableAndReportsInvalidPinTargets()
    {
        using var store = CreateStore(("ROUTES/Test/BROKEN.tdb", """
            SIMISA@@@@@@@@@@JINX0t1t
            TrackDB (
              TrackNodes ( 1
                TrackNode ( 1
                  UiD ( 0 0 10 0 1000 1000 0 0 0 0 0 0 )
                  TrEndNode ( )
                  TrPins ( 1 0 TrPin ( 9 0 ) )
                )
              )
            )
            """));

        var database = new TrackDatabaseReader().Read(store, "ROUTES/Test/BROKEN.tdb");

        Assert.Single(database.Diagnostics);
        Assert.Contains("missing node 9", database.Diagnostics[0]);
        Assert.Empty(database.EnumerateConnections());
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

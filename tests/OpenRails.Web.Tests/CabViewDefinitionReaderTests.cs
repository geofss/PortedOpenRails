using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class CabViewDefinitionReaderTests
{
    [Fact]
    public void ReadsCabPositionsDirectionsAndDayNightLightAssets()
    {
        using var store = CreateStore(
            ("TRAINS/TRAINSET/DEMO/CABVIEW.CVF", """
                SIMISA@@@@@@@@@@JINX0t1t______
                Tr_CabViewFile (
                    Position ( 0 2.4 0.5 )
                    Direction ( 0 0 0 )
                    CabViewFile ( cabview/Front.ace )
                    CabViewControls ( )
                    ORTSCabViewControls ( )
                )
                """),
            ("TRAINS/TRAINSET/DEMO/cabview/Front1024.ace", "texture"));

        var cab = new CabViewDefinitionReader().Read(store, "TRAINS/TRAINSET/DEMO/CABVIEW.CVF");

        Assert.Single(cab.Locations);
        Assert.Equal(2.4f, cab.Locations[0].Y);
        Assert.Single(cab.Directions);
        Assert.Equal("TRAINS/TRAINSET/DEMO/cabview/Front1024.ace", cab.Views[0].DayImagePath);
        Assert.Equal("TRAINS/TRAINSET/DEMO/cabview/NIGHT/Front1024.ace", cab.Views[0].NightImagePath);
        Assert.Equal("TRAINS/TRAINSET/DEMO/cabview/CABLIGHT/Front1024.ace", cab.Views[0].CabLightImagePath);
        Assert.Equal(2, cab.ControlBlockCount);
    }

    [Fact]
    public void RejectsCabDefinitionWithUnpairedHeadPositionsAndDirections()
    {
        using var store = CreateStore(("CAB.CVF", """
            SIMISA@@@@@@@@@@JINX0t1t______
            Tr_CabViewFile (
                Position ( 0 0 0 )
            )
            """));
        Assert.Throws<InvalidDataException>(() => new CabViewDefinitionReader().Read(store, "CAB.CVF"));
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
                stream.Write(Encoding.UTF8.GetBytes(file.Content));
            }
        }
        memory.Position = 0;
        return new ZipContentStore(memory);
    }
}

using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class ConsistReaderTests
{
    [Fact]
    public void ReadsOriginalConFileNameCarsAndFlags()
    {
        using var store = CreateStore(("TRAINS/CONSISTS/Test.con", """
            SIMISA@@@@@@@@@@JINX0t0t
            Train (
              TrainCfg ( "Regional test consist"
                Wagon ( UiD ( 10 ) WagonData ( "PassengerCar" "PassengerFolder" ) LoadData ( "Coal" "CoalFolder" "Freight" "Full" ) LoadData ( "Empty" "EmptyFolder" "Passenger" ) )
                Engine ( UiD ( 20 ) EngineData ( "TestEngine" "EngineFolder" ) Flip ( ) )
                ORTSEOT ( UiD ( 30 ) EOTData ( "EndDevice" "EndFolder" ) )
              )
            )
            """));

        var consist = new ConsistReader().Read(store, "TRAINS/CONSISTS/Test.con");

        Assert.Equal("Regional test consist", consist.Name);
        Assert.Equal("TRAINS/CONSISTS/Test.con", consist.SourcePath);
        Assert.Equal(3, consist.Cars.Count);
        Assert.Equal(new ConsistCar("PassengerCar", "PassengerFolder", 10, false, false, false), consist.Cars[0] with { Loads = null });
        Assert.Equal(2, consist.Cars[0].Loads!.Count);
        Assert.Equal(new ConsistLoadData("Coal", "CoalFolder", "Freight", "Full"), consist.Cars[0].Loads![0]);
        Assert.Equal(new ConsistLoadData("Empty", "EmptyFolder", "Passenger", null), consist.Cars[0].Loads[1]);
        Assert.Equal(new ConsistCar("TestEngine", "EngineFolder", 20, true, false, true), consist.Cars[1]);
        Assert.Equal(new ConsistCar("EndDevice", "EndFolder", 30, false, true, false), consist.Cars[2]);
    }

    [Fact]
    public void UsesFileNameWhenTrainCfgNameIsMissing()
    {
        using var store = CreateStore(("TRAINS/CONSISTS/Fallback.con", """
            Train (
              TrainCfg ( "" 
                Wagon ( UiD ( 1 ) WagonData ( "Car" "Folder" ) )
              )
            )
            """));

        var consist = new ConsistReader().Read(store, "TRAINS/CONSISTS/Fallback.con");

        Assert.Equal("Fallback", consist.Name);
    }

    [Fact]
    public void RejectsConsistWithoutCars()
    {
        using var store = CreateStore(("TRAINS/CONSISTS/Empty.con", """
            Train ( TrainCfg ( "Empty consist" ) )
            """));

        Assert.Throws<InvalidDataException>(() => new ConsistReader().Read(store, "TRAINS/CONSISTS/Empty.con"));
    }

    private static ZipContentStore CreateStore(params (string Path, string Content)[] files)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in files)
            {
                var entry = archive.CreateEntry(path);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }
        stream.Position = 0;
        return new ZipContentStore(stream);
    }
}

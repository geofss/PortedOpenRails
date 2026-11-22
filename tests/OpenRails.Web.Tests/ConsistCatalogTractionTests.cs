using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class ConsistCatalogTractionTests
{
    [Fact]
    public void ResolvesLocomotiveTractionMetadataFromOwningEngineAsset()
    {
        using var store = CreateStore(
            ("TRAINS/CONSISTS/Test.con", """
                SIMISA@@@@@@@@@@JINX0t1t
                Train (
                    TrainCfg (
                        "Test Consist"
                        Engine (
                            UiD ( 1 )
                            EngineData ( "Engine" "Test" )
                        )
                    )
                )
                """),
            ("TRAINS/TRAINSET/Test/Engine.eng", """
                SIMISA@@@@@@@@@@JINX0t1t
                Wagon (
                    Engine
                    Name ( "Test Engine" )
                    Mass ( 100000 )
                    Size ( 3 4 20 )
                    WagonShape ( engine.s )
                )
                Engine (
                    MaxPower ( 2000000 )
                    MaxForce ( 200000 )
                    MaxContinuousForce ( 150000 )
                    MaxVelocity ( 120 )
                    ORTSMaxTractiveForceCurves (
                        0 ( 0 0 10 0 )
                        1 ( 0 250000 10 200000 )
                    )
                )
                """),
            ("TRAINS/TRAINSET/Test/engine.s", "placeholder"));

        var catalog = new ConsistCatalogReader().Read(store);

        var consist = Assert.Single(catalog.Consists);
        var car = Assert.Single(catalog.CarAssets);
        Assert.Equal("Test Consist", consist.Name);
        Assert.True(car.IsEngine);
        Assert.NotNull(car.TractionMetadata);
        Assert.Equal(200_000, car.TractionMetadata!.MaxForceNewtons);
        Assert.Equal(2_000_000, car.TractionMetadata.MaxPowerWatts);
        Assert.NotNull(car.TractionMetadata.TractionForceCurves);
        Assert.Empty(catalog.Issues);
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

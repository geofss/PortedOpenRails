using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class RollingStockCouplerMetadataReaderTests
{
    [Fact]
    public void ReadsSpringStiffnessDampingSlackAndBreakRatingsFromBothCouplingEnds()
    {
        using var store = CreateStore(("TRAINS/TRAINSET/TEST/car.wag", """
            SIMISA@@@@@@@@@@JINX0t1t______
            Wagon ( TestCar
                Size ( 2.8m 4m 20m )
                Mass ( 40t )
                Coupling (
                    Type ( Automatic )
                    Spring (
                        Stiffness ( 1e6N/m 5e6N/m )
                        Damping ( 1e6N/m/s 2e6N/m/s )
                        Break ( 950kN 900kN )
                        r0 ( 12cm 16cm )
                    )
                )
                Coupling (
                    Type ( Automatic )
                    Spring (
                        Stiffness ( 2e6N/m 6e6N/m )
                        Damping ( 2e6N/m/s 4e6N/m/s )
                        Break ( 850kN 800kN )
                        r0 ( 10cm 14cm )
                    )
                )
            )
            """));

        var metadata = new RollingStockMetadataReader().Read(store, "TRAINS/TRAINSET/TEST/car.wag");

        Assert.Equal(800_000f, metadata.MinCouplerStrengthN.GetValueOrDefault());
        Assert.Equal(3_500_000f, metadata.CouplerStiffnessNPerM.GetValueOrDefault());
        Assert.Equal(2_250_000f, metadata.CouplerDampingNsPerM.GetValueOrDefault());
        Assert.Equal(0.13f, metadata.CouplerSlackMeters.GetValueOrDefault(), 5);
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

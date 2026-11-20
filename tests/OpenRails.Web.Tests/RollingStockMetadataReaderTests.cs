using Xunit;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;

namespace OpenRails.Web.Tests;

public sealed class RollingStockMetadataReaderTests
{
    [Fact]
    public void ReadsEngineMetadataFromVirtualContent()
    {
        using var store = CreateStore(("TRACTION/engine.eng", """
            SIMISA@@@@@@@@@@JINX0E0t______
            Engine ( TestEngine
                Name ( "Test Locomotive" )
                Type ( Diesel )
                MaxPower ( 2000kW )
                MaxForce ( 300kN )
                MaxContinuousForce ( 250kN )
                DynamicBrakesMaximumForce ( 120kN )
                MaxVelocity ( 120km/h )
                Description ( "A test locomotive" )
            )
            """));

        var result = new RollingStockMetadataReader().ReadEngine(store, "TRACTION/engine.eng");

        Assert.Equal("Test Locomotive", result.Name);
        Assert.Equal("Diesel", result.EngineType);
        Assert.Equal(2_000_000f, result.MaximumPowerWatts, 0);
        Assert.Equal(300_000f, result.MaximumTractiveForceNewtons, 0);
        Assert.Equal(250_000f, result.MaximumContinuousForceNewtons, 0);
        Assert.Equal(120_000f, result.MaximumDynamicBrakeForceNewtons, 0);
        Assert.Equal(120f / 3.6f, result.MaximumSpeedMetersPerSecond, 2);
        Assert.Equal("A test locomotive", result.Description);
    }

    [Fact]
    public void ReadsWagonMetadataFromVirtualContent()
    {
        using var store = CreateStore(("ROLLINGSTOCK/coach.wag", """
            SIMISA@@@@@@@@@@JINX0w1t______
            Wagon ( TestCoach
                Name ( "Passenger Coach" )
                Type ( Carriage )
                Mass ( 40000kg )
                Size ( 2.8m 3.9m 20m )
                ORTSNumberAxles ( 4 )
                BrakeSystemType ( "Air_single_pipe" )
                BrakeEquipmentType ( "Triple_valve" )
                MaxBrakeForce ( 80kN )
            )
            """));

        var result = new RollingStockMetadataReader().ReadWagon(store, "ROLLINGSTOCK/coach.wag");

        Assert.Equal("Passenger Coach", result.Name);
        Assert.Equal("Carriage", result.WagonType);
        Assert.Equal(40_000f, result.MassKilograms, 0);
        Assert.Equal(2.8f, result.WidthMeters, 2);
        Assert.Equal(3.9f, result.HeightMeters, 2);
        Assert.Equal(20f, result.LengthMeters, 2);
        Assert.Equal(4, result.NumberOfAxles);
        Assert.Equal("Air_single_pipe", result.BrakeSystemType);
        Assert.Equal(80_000f, result.MaximumBrakeForceNewtons, 0);
    }

    [Fact]
    public void RejectsWrongFileExtension()
    {
        using var store = CreateStore(("engine.txt", "Engine ( Test )"));
        Assert.Throws<ArgumentException>(() =>
            new RollingStockMetadataReader().ReadEngine(store, "engine.txt"));
    }

    private static ZipContentStore CreateStore(params (string Path, string Content)[] files)
    {
        var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream,
            System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Path);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(file.Content);
            }
        }
        stream.Position = 0;
        return new ZipContentStore(stream);
    }
}

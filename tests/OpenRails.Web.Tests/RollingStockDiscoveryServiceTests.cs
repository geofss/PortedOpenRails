using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class RollingStockDiscoveryServiceTests
{
    [Fact]
    public void DiscoversEnginesAndWagonsInStablePathOrder()
    {
        using var store = CreateStore(
            ("TRAINSET/Zeta/coach.wag", Wagon("Zeta Coach", 42000)),
            ("TRAINSET/Alpha/engine.eng", Engine("Alpha Engine", 1800000)),
            ("TRAINSET/Alpha/coach.wag", Wagon("Alpha Coach", 40000)),
            ("TRAINSET/Zeta/engine.eng", Engine("Zeta Engine", 2200000)),
            ("GLOBAL/tsection.dat", "TrackSections ( )"));

        var result = new RollingStockDiscoveryService().Discover(store);

        Assert.Equal(2, result.Engines.Count);
        Assert.Equal(2, result.Wagons.Count);
        Assert.Equal(4, result.VehicleCount);
        Assert.Equal("TRAINSET/Alpha/engine.eng", result.Engines[0].DefinitionFile);
        Assert.Equal("Alpha Engine", result.Engines[0].Metadata.Name);
        Assert.Equal("TRAINSET/Alpha/coach.wag", result.Wagons[0].DefinitionFile);
        Assert.Equal("Alpha Coach", result.Wagons[0].Metadata.Name);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void ReportsBrokenDefinitionsAndContinues()
    {
        using var store = CreateStore(
            ("TRAINSET/Broken/bad.eng", "SIMISA@@@@@@@@@@JINX0E0t\nNotAnEngine ( )"),
            ("TRAINSET/Good/good.wag", Wagon("Good Coach", 40000)));

        var result = new RollingStockDiscoveryService().Discover(store);

        Assert.Empty(result.Engines);
        Assert.Single(result.Wagons);
        var failure = Assert.Single(result.Failures);
        Assert.Equal("TRAINSET/Broken/bad.eng", failure.DefinitionFile);
        Assert.False(string.IsNullOrWhiteSpace(failure.Reason));
    }

    [Fact]
    public void ReturnsEmptyCatalogWhenNoRollingStockFilesExist()
    {
        using var store = CreateStore(("GLOBAL/tsection.dat", "TrackSections ( )"));

        var result = new RollingStockDiscoveryService().Discover(store);

        Assert.Empty(result.Engines);
        Assert.Empty(result.Wagons);
        Assert.Empty(result.Failures);
        Assert.Equal(0, result.VehicleCount);
    }

    private static string Engine(string name, int powerWatts) => $$"""
        SIMISA@@@@@@@@@@JINX0E0t
        Engine ( TestEngine
            Name ( "{{name}}" )
            MaxPower ( {{powerWatts}}W )
            MaxForce ( 250kN )
            MaxVelocity ( 100km/h )
        )
        """;

    private static string Wagon(string name, int massKilograms) => $$"""
        SIMISA@@@@@@@@@@JINX0w1t
        Wagon ( TestWagon
            Name ( "{{name}}" )
            Mass ( {{massKilograms}}kg )
            Size ( 2.8m 3.9m 20m )
        )
        """;

    private static ZipContentStore CreateStore(params (string Path, string Content)[] files)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
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

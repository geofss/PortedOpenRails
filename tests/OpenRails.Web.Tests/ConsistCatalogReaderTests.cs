using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class ConsistCatalogReaderTests
{
    [Fact]
    public void DiscoversConsistsAndReportsBrokenFilesWithoutStoppingCatalog()
    {
        using var store = CreateStore(
            ("TRAINS/CONSISTS/Alpha.con", """
                Train ( TrainCfg ( "Alpha" Engine ( UiD ( 1 ) EngineData ( "EngineA" "FolderA" ) ) ) )
                """),
            ("TRAINS/CONSISTS/Broken.con", """
                Train ( TrainCfg ( "Broken" Wagon ( UiD ( 2 ) ) ) )
                """),
            ("TRAINS/CONSISTS/Beta.con", """
                Train ( TrainCfg ( "Beta" Wagon ( UiD ( 3 ) WagonData ( "CarB" "FolderB" ) ) ) )
                """),
            ("TRAINS/CONSISTS/Duplicate.con", """
                Train ( TrainCfg ( "Duplicate" Wagon ( UiD ( 4 ) WagonData ( "CarA" "FolderA" ) ) Wagon ( UiD ( 4 ) WagonData ( "CarB" "FolderB" ) ) ) )
                """),
            ("TRAINS/TRAINSET/FolderA/EngineA.eng", "engine"),
            ("TRAINS/TRAINSET/FolderB/CarB.wag", "wagon"),
            ("TRAINS/TRAINSET/FolderA/CarA.wag", "wagon A"));

        var result = new ConsistCatalogReader().Read(store);

        Assert.Equal(new[] { "Alpha", "Beta", "Duplicate" }, result.Consists.Select(consist => consist.Name));
        Assert.Equal(2, result.Issues.Count);
        Assert.Equal("TRAINS/CONSISTS/Broken.con", result.Issues[0].Path);
        Assert.Contains("missing", result.Issues[0].Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("TRAINS/CONSISTS/Duplicate.con", result.Issues[1].Path);
        Assert.Contains("Duplicate car UID 4", result.Issues[1].Message);
        Assert.Equal(4, result.CarAssets.Count);
        Assert.All(result.CarAssets, asset => Assert.True(asset.Found));
        Assert.Contains(result.CarAssets, asset => asset.IsEngine && asset.AssetPath == "TRAINS/TRAINSET/FolderA/EngineA.eng");
        Assert.Contains(result.CarAssets, asset => !asset.IsEngine && asset.AssetPath == "TRAINS/TRAINSET/FolderB/CarB.wag");
    }

    [Fact]
    public void DiscoversConsistsFromCompositePackageAndHonorsOverridePriority()
    {
        using var composite = new CompositeContentStore(new[]
        {
            new PackageLayer("Route", CreateStore(
                ("TRAINS/CONSISTS/Shared.con", """Train ( TrainCfg ( "Route override" Wagon ( UiD ( 1 ) WagonData ( "RouteCar" "RouteFolder" ) ) ) )"""),
                ("TRAINS/TRAINSET/RouteFolder/RouteCar.wag", "route car"))),
            new PackageLayer("Global", CreateStore(
                ("TRAINS/CONSISTS/shared.CON", """Train ( TrainCfg ( "Global version" Wagon ( UiD ( 2 ) WagonData ( "GlobalCar" "GlobalFolder" ) ) ) )"""),
                ("TRAINS/CONSISTS/GlobalOnly.con", """Train ( TrainCfg ( "Global only" Wagon ( UiD ( 3 ) WagonData ( "GlobalCar" "GlobalFolder" ) ) ) )"""),
                ("TRAINS/TRAINSET/RouteFolder/RouteCar.wag", "global override"),
                ("TRAINS/TRAINSET/GlobalFolder/GlobalCar.wag", "global car")))
        });

        var result = new ConsistCatalogReader().Read(composite);

        Assert.Equal(new[] { "Global only", "Route override" }, result.Consists.Select(consist => consist.Name));
        Assert.Empty(result.Issues);
        Assert.Equal("RouteCar", result.Consists.Single(consist => consist.Name == "Route override").Cars[0].Name);
        Assert.Equal("Route", result.CarAssets.Single(asset => asset.CarName == "RouteCar").SourcePackage);
        Assert.Equal("Global", result.CarAssets.Single(asset => asset.CarName == "GlobalCar").SourcePackage);
    }

    [Fact]
    public void ReportsEndOfTrainDeviceThatIsNotLast()
    {
        using var store = CreateStore(("TRAINS/CONSISTS/BadOrder.con", """
            Train ( TrainCfg ( "Bad order"
              Wagon ( UiD ( 1 ) WagonData ( "Car" "Folder" ) )
              ORTSEOT ( UiD ( 2 ) EOTData ( "EOT" "Folder" ) )
              Wagon ( UiD ( 3 ) WagonData ( "TrailingCar" "Folder" ) )
            ) )
            """));

        var result = new ConsistCatalogReader().Read(store);

        Assert.Single(result.Consists);
        Assert.Single(result.Issues);
        Assert.Contains("not the final car", result.Issues[0].Message);
    }

    [Fact]
    public void ReportsMissingEngineAndWagonAssetsAndResolvesEotAsWagon()
    {
        using var store = CreateStore(
            ("TRAINS/CONSISTS/Assets.con", """
                Train ( TrainCfg ( "Assets"
                  Engine ( UiD ( 1 ) EngineData ( "Engine" "EngineFolder" ) )
                  ORTSEOT ( UiD ( 2 ) EOTData ( "EndDevice" "EotFolder" ) )
                  Wagon ( UiD ( 3 ) WagonData ( "MissingCar" "MissingFolder" ) )
                ) )
                """),
            ("TRAINS/TRAINSET/EngineFolder/Engine.eng", "engine"),
            ("TRAINS/ORTS_EOT/EotFolder/EndDevice.eot", "eot"));

        var result = new ConsistCatalogReader().Read(store);

        Assert.Equal(3, result.CarAssets.Count);
        Assert.True(result.CarAssets.Single(asset => asset.IsEngine).Found);
        Assert.True(result.CarAssets.Single(asset => asset.IsEndOfTrain).Found);
        Assert.Equal("TRAINS/ORTS_EOT/EotFolder/EndDevice.eot", result.CarAssets.Single(asset => asset.IsEndOfTrain).AssetPath);
        Assert.False(result.CarAssets.Single(asset => asset.CarName == "MissingCar").Found);
        Assert.EndsWith("MissingCar.wag", result.CarAssets.Single(asset => asset.CarName == "MissingCar").AssetPath);
    }

    [Fact]
    public void RejectsRollingStockAssetPathsThatEscapeVirtualRoot()
    {
        using var store = CreateStore(("TRAINS/CONSISTS/Traversal.con", """
            Train ( TrainCfg ( "Traversal"
              Wagon ( UiD ( 1 ) WagonData ( "Car" "../outside" ) )
            ) )
            """));

        var result = new ConsistCatalogReader().Read(store);

        Assert.Single(result.Consists);
        Assert.Single(result.Issues);
        Assert.Contains("traversal", result.Issues[0].Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.CarAssets);
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

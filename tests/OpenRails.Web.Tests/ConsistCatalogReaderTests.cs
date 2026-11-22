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
            ("TRAINS/TRAINSET/FolderA/EngineA.eng", WagonFixture),
            ("TRAINS/TRAINSET/FolderB/CarB.wag", WagonFixture),
            ("TRAINS/TRAINSET/FolderA/CarA.wag", WagonFixture));

        var result = new ConsistCatalogReader().Read(store);

        Assert.Equal(new[] { "Alpha", "Beta", "Duplicate" }, result.Consists.Select(consist => consist.Name));
        Assert.Equal(2, result.Issues.Count);
        Assert.Equal("TRAINS/CONSISTS/Broken.con", result.Issues[0].Path);
        Assert.Contains("missing", result.Issues[0].Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("TRAINS/CONSISTS/Duplicate.con", result.Issues[1].Path);
        Assert.Contains("Duplicate car UID 4", result.Issues[1].Message);
        Assert.Equal(4, result.CarAssets.Count);
        Assert.All(result.CarAssets, asset => Assert.True(asset.Found));
        Assert.All(result.CarAssets, asset => Assert.NotNull(asset.Metadata));
        var metadata = result.CarAssets[0].Metadata!;
        Assert.Equal("FixtureCar", metadata.Name);
        Assert.Equal("Freight", metadata.Type);
        Assert.Equal(30, metadata.MassKg);
        Assert.Equal(12, metadata.LengthM);
        Assert.Equal(4, metadata.NumberAxles);
        Assert.Equal(8, metadata.NumberWheels);
        Assert.Equal("Vacuum", metadata.BrakeSystemType);
        Assert.Equal("TripleValve", metadata.BrakeEquipmentType);
        Assert.Equal(100, metadata.MaxBrakeForceN);
        Assert.Contains(result.CarAssets, asset => asset.IsEngine && asset.AssetPath == "TRAINS/TRAINSET/FolderA/EngineA.eng");
        Assert.Contains(result.CarAssets, asset => !asset.IsEngine && asset.AssetPath == "TRAINS/TRAINSET/FolderB/CarB.wag");
    }

    [Fact]
    public void DiscoversConsistsFromCompositePackageAndHonorsOverridePriority()
    {
        using var composite = new CompositeContentStore(new[]
        {
            new PackageLayer("Route", CreateStore(
                ("TRAINS/CONSISTS/Shared.con", """Train ( TrainCfg ( "Route override" ORTSTrainControlSystemParameters ( "route.tcs" ) Wagon ( UiD ( 1 ) WagonData ( "RouteCar" "RouteFolder" ) ) ) )"""),
                ("TRAINS/TRAINSET/RouteFolder/RouteCar.wag", WagonFixture),
                ("TRAINS/CONSISTS/SCRIPT/route.tcs", "route TCS"))),
            new PackageLayer("Global", CreateStore(
                ("TRAINS/CONSISTS/shared.CON", """Train ( TrainCfg ( "Global version" Wagon ( UiD ( 2 ) WagonData ( "GlobalCar" "GlobalFolder" ) ) ) )"""),
                ("TRAINS/CONSISTS/GlobalOnly.con", """Train ( TrainCfg ( "Global only" Wagon ( UiD ( 3 ) WagonData ( "GlobalCar" "GlobalFolder" ) ) ) )"""),
                ("TRAINS/TRAINSET/RouteFolder/RouteCar.wag", WagonFixture),
                ("TRAINS/TRAINSET/GlobalFolder/GlobalCar.wag", WagonFixture),
                ("TRAINS/CONSISTS/SCRIPT/route.tcs", "global TCS")))
        });

        var result = new ConsistCatalogReader().Read(composite);

        Assert.Equal(new[] { "Global only", "Route override" }, result.Consists.Select(consist => consist.Name));
        Assert.Empty(result.Issues);
        Assert.Equal("RouteCar", result.Consists.Single(consist => consist.Name == "Route override").Cars[0].Name);
        Assert.Equal("Route", result.Consists.Single(consist => consist.Name == "Route override").SourcePackage);
        Assert.Equal("Global", result.Consists.Single(consist => consist.Name == "Global only").SourcePackage);
        Assert.Equal("Route", result.CarAssets.Single(asset => asset.CarName == "RouteCar").SourcePackage);
        Assert.Equal("Global", result.CarAssets.Single(asset => asset.CarName == "GlobalCar").SourcePackage);
        Assert.Single(result.TcsAssets);
        Assert.True(result.TcsAssets[0].Found);
        Assert.Equal("TRAINS/CONSISTS/SCRIPT/route.tcs", result.TcsAssets[0].AssetPath);
        Assert.Equal("Route", result.TcsAssets[0].SourcePackage);
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
            ("TRAINS/TRAINSET/EngineFolder/Engine.eng", WagonFixture),
            ("TRAINS/ORTS_EOT/EotFolder/EndDevice.eot", "eot"));

        var result = new ConsistCatalogReader().Read(store);

        Assert.Equal(3, result.CarAssets.Count);
        Assert.True(result.CarAssets.Single(asset => asset.IsEngine).Found);
        Assert.NotNull(result.CarAssets.Single(asset => asset.IsEngine).Metadata);
        Assert.True(result.CarAssets.Single(asset => asset.IsEndOfTrain).Found);
        Assert.Null(result.CarAssets.Single(asset => asset.IsEndOfTrain).Metadata);
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

    [Fact]
    public void RejectsTcsParameterPathsThatEscapeVirtualRoot()
    {
        using var store = CreateStore(("TRAINS/CONSISTS/TcsTraversal.con", """
            Train ( TrainCfg ( "TCS traversal" ORTSTrainControlSystemParameters ( "../escape.tcs" )
              Wagon ( UiD ( 1 ) WagonData ( "Car" "Folder" ) )
            ) )
            """));

        var result = new ConsistCatalogReader().Read(store);

        Assert.Single(result.Consists);
        Assert.Single(result.Issues);
        Assert.Contains("traversal", result.Issues[0].Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.TcsAssets);
    }

    [Fact]
    public void ReportsMissingTcsScriptWithoutRejectingConsist()
    {
        using var store = CreateStore(("TRAINS/CONSISTS/MissingTcs.con", """
            Train ( TrainCfg ( "Missing TCS" ORTSTrainControlSystemParameters ( "missing.tcs" )
              Wagon ( UiD ( 1 ) WagonData ( "Car" "Folder" ) )
            ) )
            """));

        var result = new ConsistCatalogReader().Read(store);

        Assert.Single(result.Consists);
        Assert.Single(result.TcsAssets);
        Assert.False(result.TcsAssets[0].Found);
        Assert.Equal("TRAINS/CONSISTS/SCRIPT/missing.tcs", result.TcsAssets[0].AssetPath);
        Assert.Empty(result.Issues);
    }

    private const string WagonFixture = """
        Wagon (
            Name ( "FixtureCar" )
            Type ( "Freight" )
            Mass ( 30 )
            Size ( 2.5 4 12 )
            ORTSNumberAxles ( 4 )
            NumWheels ( 8 )
            BrakeSystemType ( "Vacuum" )
            BrakeEquipmentType ( "TripleValve" )
            MaxBrakeForce ( 100 )
        )
        """;

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

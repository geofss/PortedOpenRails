using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class RouteLoaderTests
{
    [Fact]
    public void LoadsRouteMetadataTrackDatabaseGeometryAndTerrainTileReferencesTogether()
    {
        using var store = CreateStore(
            ("ROUTES/Test/Test.trk", """
                SIMISA@@@@@@@@@@JINX0T0t
                Tr_RouteFile (
                    RouteID ( "TEST" )
                    Name ( "Test Route" )
                    Description ( "End-to-end route fixture" )
                    FileName ( "TESTDB" )
                    Electrified ( true )
                    MaxLineVoltage ( 25000 )
                    OverheadWireHeight ( 5.5m )
                    SpeedLimit ( 80 )
                    TempRestrictedSpeed ( 20 )
                    TrackGauge ( 1.435m )
                    MilepostUnitsKilometers
                    ORTSForceSuperElevation ( true )
                    RouteStart ( 0 0 0 0 )
                )
                """),
            ("ROUTES/Test/TDB/TESTDB.tdb", """
                SIMISA@@@@@@@@@@JINX0t1t
                TrackDB (
                    TrackNodes ( 3
                        TrackNode ( 1
                            UiD ( 0 0 1 0 0 0 0 0 0 0 0 0 )
                            TrEndNode ( )
                            TrPins ( 1 0 TrPin ( 2 0 ) )
                        )
                        TrackNode ( 2
                            TrVectorNode (
                                TrVectorSections ( 1
                                    1 0 0 0 0 0 1 "00" 0 0 0 0 0 0 0 0
                                )
                                TrItemRefs ( 1 TrItemRef ( 0 ) )
                            )
                            TrPins ( 1 1 TrPin ( 1 1 ) TrPin ( 3 0 ) )
                        )
                        TrackNode ( 3
                            UiD ( 0 0 2 0 0 0 0 0 10 0 0 0 )
                            TrEndNode ( )
                            TrPins ( 1 0 TrPin ( 2 1 ) )
                        )
                    )
                    TrItemTable ( 1 EmptyItem ( TrItemId ( 0 ) ) )
                )
                """),
            ("GLOBAL/TSECTION.DAT", """
                SIMISA@@@@@@@@@@JINX0T0t
                TrackSections (
                    10
                    TrackSection ( 1 SectionSize ( 1m 10m ) )
                )
                TrackShapes (
                    1
                    TrackShape ( 0 FileName ( "track.s" ) NumPaths ( 0 ) )
                )
                """),
            ("GLOBAL/SHAPES/track.s", "shape placeholder"),
            ("ROUTES/Test/TILES/-7FFFFFFC.t", "terrain tile placeholder"),
            ("ROUTES/Test/TILES/-7FFFFFFC_y.raw", "altitude samples"),
            ("ROUTES/Test/WORLD/W+000000+000000.w", "world tile placeholder"));

        var metadata = new RouteMetadataReader().Read(store, "ROUTES/Test/Test.trk");
        var route = new RouteLoader().Load(store, "ROUTES/Test/Test.trk", metadata);

        Assert.Equal("Test Route", route.Metadata.Name);
        Assert.True(route.Metadata.Electrified);
        Assert.Equal(25000, route.Metadata.MaxLineVoltage);
        Assert.Equal(5.5f, route.Metadata.OverheadWireHeightMeters);
        Assert.Equal(80, route.Metadata.SpeedLimitMetersPerSecond);
        Assert.Equal(20, route.Metadata.TempRestrictedSpeedMetersPerSecond);
        Assert.Equal(1.435f, route.Metadata.TrackGaugeMeters);
        Assert.True(route.Metadata.MilepostUnitsMetric);
        Assert.Equal(1, route.Metadata.SuperElevationMode);
        Assert.Equal("ROUTES/Test/TDB/TESTDB.tdb", route.Assets.TrackDatabasePath);
        Assert.Equal(3, route.TrackDatabase.NodeCount);
        Assert.Equal(1, route.TrackDatabase.TrackItemCount);
        Assert.Equal(new[] { 0 }, route.TrackDatabase.GetRequiredNode(2).TrackItemReferences);
        var trackItem = Assert.IsType<TrackItem>(Assert.Single(route.TrackDatabase.TrackItems));
        Assert.Equal("empty", trackItem.Type);
        Assert.Equal(0, trackItem.Index);
        Assert.Equal(1, route.TrackNetwork.VectorPathCount);
        Assert.Equal(10, route.TrackNetwork.TotalTrackLengthMeters);
        Assert.Equal("track.s", route.TrackNetwork.VectorPaths[0].Sections[0].Shape?.FileName);
        Assert.Equal("GLOBAL/SHAPES/track.s", Assert.Single(route.TrackShapeAssets.Found).Path);
        Assert.Empty(route.TrackShapeAssets.Missing);
        Assert.Equal("ROUTES/Test/WORLD/W+000000+000000.w", Assert.Single(route.WorldTiles.Found).Path);
        Assert.Empty(route.WorldTiles.Missing);
        Assert.Single(route.TerrainTiles.Found);
        Assert.Empty(route.TerrainTiles.Missing);
        Assert.True(route.Validation.TrackNetworkUsable);
        Assert.Equal(1, route.Validation.VectorPathCount);
        Assert.Equal(10, route.Validation.TotalTrackLengthMeters);
        Assert.True(route.Validation.AssetIssueCount > 0);
        Assert.Contains(route.Validation.Issues, issue => issue.Category == "Terrain");
        Assert.Contains(route.Validation.Issues, issue => issue.Category == "Track shape");
        Assert.Contains(route.Validation.Issues, issue => issue.Category == "WORLD");
        Assert.Equal(route.WorldScenery.Loaded.Sum(tile => tile.ShapeDiscovery.Missing.Count), route.Validation.MissingWorldShapes);
        Assert.Equal(route.WorldScenery.Loaded.Sum(tile => tile.FailedShapes.Count), route.Validation.FailedWorldShapes);
        Assert.Equal(
            route.TerrainTextures.Missing.Count +
            route.TrackShapeMeshes.Textures.Missing.Count +
            route.WorldScenery.Loaded.Sum(tile => tile.Textures.Missing.Count),
            route.Validation.MissingTextures);
        Assert.Equal(
            route.TerrainTextures.Failed.Count +
            route.TrackShapeMeshes.Textures.Failed.Count +
            route.WorldScenery.Loaded.Sum(tile => tile.Textures.Failed.Count),
            route.Validation.FailedTextures);
    }

    [Fact]
    public void MarksVectorPathWithInvalidPinTopologyAsNotUsable()
    {
        using var store = CreateStore(
            ("ROUTES/Test/Test.trk", """
                SIMISA@@@@@@@@@@JINX0T0t
                Tr_RouteFile ( RouteID ( "TEST" ) Name ( "Test Route" ) Description ( "Topology validation fixture" ) FileName ( "TESTDB" ) RouteStart ( 0 0 0 0 ) )
                """),
            ("ROUTES/Test/TDB/TESTDB.tdb", """
                SIMISA@@@@@@@@@@JINX0t1t
                TrackDB (
                    TrackNodes ( 1
                        TrackNode ( 1
                            TrVectorNode ( TrVectorSections ( 1 1 0 0 0 0 1 "00" 0 0 0 0 0 0 0 0 ) TrItemRefs ( 0 ) )
                            TrPins ( 1 0 TrPin ( 2 0 ) )
                        )
                    )
                    TrItemTable ( 0 )
                )
                """),
            ("GLOBAL/TSECTION.DAT", """
                SIMISA@@@@@@@@@@JINX0T0t
                TrackSections ( 1 TrackSection ( 1 SectionSize ( 1m 10m ) ) )
                TrackShapes ( 0 )
                """));

        var metadata = new RouteMetadataReader().Read(store, "ROUTES/Test/Test.trk");
        var route = new RouteLoader().Load(store, "ROUTES/Test/Test.trk", metadata);

        Assert.Equal(1, route.TrackNetwork.VectorPathCount);
        Assert.True(route.TrackNetwork.TotalTrackLengthMeters > 0);
        Assert.False(route.Validation.TrackNetworkUsable);
        Assert.True(route.Validation.HasCriticalIssues);
        Assert.Contains(route.Validation.Issues,
            issue => issue.Category == "Track network" &&
                issue.Message.Contains("critical topology problems", StringComparison.OrdinalIgnoreCase));
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

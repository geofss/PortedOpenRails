using OpenRails.Web.Content;
using OpenRails.Web.Simulation;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Loads the connected route-data vertical slice from an imported MSTS/Open Rails
/// content namespace: TRK metadata, TDB topology, TSECTION geometry, and terrain tiles.
/// </summary>
public sealed class RouteLoader
{
    public RouteLoadResult Load(
        IVirtualContentStore store,
        string routeFilePath,
        RouteMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(metadata);

        var assets = new RouteAssetLocator().Locate(store, routeFilePath, metadata);
        var database = new TrackDatabaseReader().Read(store, assets.TrackDatabasePath);

        var geometryReader = new TrackSectionGeometryReader();
        var globalSections = geometryReader.ReadGlobal(store, assets.GlobalTrackSectionsPath);
        var routeSections = assets.RouteTrackSectionsPath is null
            ? null
            : geometryReader.ReadRouteSections(store, assets.RouteTrackSectionsPath);

        var network = new TrackNetworkBuilder().Build(database, globalSections, routeSections);
        var terrainTiles = new RouteTerrainTileLocator().Locate(store, assets.RouteRoot, network);
        var trackShapeAssets = new TrackShapeAssetLocator().Locate(store, assets.RouteRoot, network);
        var worldTiles = new RouteWorldTileLocator().Locate(store, assets.RouteRoot, network);
        var terrainMeshes = new RouteTerrainMeshLoader().Load(store, terrainTiles);
        var terrainTextureAssets = new TerrainTextureAssetLocator().Locate(store, assets.RouteRoot, terrainMeshes.LoadedTiles);
        var terrainTextures = new TerrainTextureAssetLoader().Load(store, terrainTextureAssets);
        var worldScenery = new RouteWorldSceneryLoader().Load(store, assets.RouteRoot, worldTiles);

        return new RouteLoadResult(
            metadata,
            assets,
            database,
            globalSections,
            routeSections,
            network,
            terrainTiles,
            trackShapeAssets,
            worldTiles,
            terrainMeshes,
            terrainTextureAssets,
            terrainTextures,
            worldScenery);
    }
}

public sealed record RouteLoadResult(
    RouteMetadata Metadata,
    RouteAssetPaths Assets,
    TrackDatabase TrackDatabase,
    TrackSectionCatalog GlobalTrackSections,
    TrackSectionCatalog? RouteTrackSections,
    TrackNetwork TrackNetwork,
    RouteTerrainTileDiscovery TerrainTiles,
    TrackShapeAssetDiscovery TrackShapeAssets,
    RouteWorldTileDiscovery WorldTiles,
    RouteTerrainMeshLoadResult TerrainMeshes,
    TerrainTextureAssetDiscovery TerrainTextureAssets,
    TerrainTextureAssetLoadResult TerrainTextures,
    RouteWorldSceneryLoadResult WorldScenery);


/// <summary>Loads route scenery chunks independently so malformed tiles do not abort the route.</summary>
public sealed class RouteWorldSceneryLoader
{
    public RouteWorldSceneryLoadResult Load(IVirtualContentStore store, string routeRoot, RouteWorldTileDiscovery discovery)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(discovery);
        var loaded = new List<LoadedWorldSceneryTile>();
        var failed = new List<RouteWorldSceneryIssue>();
        foreach (var reference in discovery.Found)
        {
            try { loaded.Add(new WorldSceneryTileLoader().Load(store, routeRoot, reference)); }
            catch (Exception exception) { failed.Add(new RouteWorldSceneryIssue(reference.TileX, reference.TileZ, reference.Path, exception.Message)); }
        }
        return new RouteWorldSceneryLoadResult(loaded, discovery.Missing, failed);
    }
}

public sealed record RouteWorldSceneryIssue(int TileX, int TileZ, string Path, string Reason);
public sealed class RouteWorldSceneryLoadResult
{
    public IReadOnlyList<LoadedWorldSceneryTile> Loaded { get; }
    public IReadOnlyList<(int TileX, int TileZ)> Missing { get; }
    public IReadOnlyList<RouteWorldSceneryIssue> Failed { get; }
    public RouteWorldSceneryLoadResult(IEnumerable<LoadedWorldSceneryTile> loaded, IEnumerable<(int TileX, int TileZ)> missing, IEnumerable<RouteWorldSceneryIssue> failed)
    { Loaded = Array.AsReadOnly(loaded.ToArray()); Missing = Array.AsReadOnly(missing.ToArray()); Failed = Array.AsReadOnly(failed.ToArray()); }
}

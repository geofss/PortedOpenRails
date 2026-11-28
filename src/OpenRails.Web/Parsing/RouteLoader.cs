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
        RouteMetadata metadata,
        RouteLoadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(metadata);
        options ??= RouteLoadOptions.Eager;

        var assets = new RouteAssetLocator().Locate(store, routeFilePath, metadata);
        var database = new TrackDatabaseReader().Read(store, assets.TrackDatabasePath);

        var geometryReader = new TrackSectionGeometryReader();
        var globalSections = geometryReader.ReadGlobal(store, assets.GlobalTrackSectionsPath);
        var routeSections = assets.RouteTrackSectionsPath is null
            ? null
            : geometryReader.ReadRouteSections(store, assets.RouteTrackSectionsPath);

        var network = new TrackNetworkBuilder().Build(database, globalSections, routeSections);
        var signalConfigurationPath = SignalConfigurationLocator.FindForRoute(store.Paths, assets.RouteRoot);
        var signalConfiguration = signalConfigurationPath is null
            ? null
            : new SignalConfigurationReader().Read(store, signalConfigurationPath);
        var signalScripts = signalConfiguration is null ? null : new SignalScriptLibraryReader().Read(store, assets.RouteRoot, signalConfiguration);
        var signalling = new SignalNetworkBuilder().Build(database, network, signalConfiguration);
        var trackItemPlacements = new RouteTrackItemPlacementBuilder().Build(network);
        var terrainTiles = new RouteTerrainTileLocator().Locate(store, assets.RouteRoot, network);
        var trackShapeAssets = new TrackShapeAssetLocator().Locate(store, assets.RouteRoot, network);
        var trackShapeMeshes = new RouteTrackShapeMeshLoader().Load(store, assets.RouteRoot, trackShapeAssets, options.MaximumTrackShapeTextureBytes);
        var trackShapePlacements = new TrackShapePlacementBuilder().Build(network, trackShapeMeshes.LoadedMeshes);
        var worldTiles = new RouteWorldTileLocator().Locate(store, assets.RouteRoot, network);
        var terrainMeshes = options.LoadTerrainMeshes
            ? new RouteTerrainMeshLoader().Load(store, terrainTiles)
            : new RouteTerrainMeshLoadResult(
                Array.Empty<TerrainTileContent>(),
                Array.Empty<LoadedRouteTerrainTileMesh>(),
                terrainTiles.Missing,
                Array.Empty<RouteTerrainTileIssue>());
        var terrainTextureAssets = options.LoadTerrainTextures
            ? new TerrainTextureAssetLocator().Locate(store, assets.RouteRoot, terrainMeshes.LoadedTiles)
            : new TerrainTextureAssetDiscovery(
                Array.Empty<TerrainTextureAssetReference>(),
                Array.Empty<TerrainTextureAssetIssue>());
        var terrainTextures = options.LoadTerrainTextures
            ? new TerrainTextureAssetLoader().Load(store, terrainTextureAssets)
            : new TerrainTextureAssetLoadResult(
                Array.Empty<LoadedTerrainTextureAsset>(),
                terrainTextureAssets.Missing,
                Array.Empty<TerrainTextureLoadIssue>());
        var worldScenery = options.LoadWorldScenery
            ? new RouteWorldSceneryLoader().Load(store, assets.RouteRoot, worldTiles)
            : new RouteWorldSceneryLoadResult(
                Array.Empty<LoadedWorldSceneryTile>(),
                worldTiles.Missing,
                Array.Empty<RouteWorldSceneryIssue>());
        var validation = RouteValidationReport.Build(database, network, terrainTiles, terrainMeshes, trackShapeAssets, trackShapeMeshes, worldTiles, worldScenery, terrainTextures, trackItemPlacements);

        return new RouteLoadResult(
            metadata,
            assets,
            database,
            globalSections,
            routeSections,
            network,
            signalling,
            signalConfiguration,
            signalScripts,
            terrainTiles,
            trackShapeAssets,
            trackShapeMeshes,
            trackShapePlacements,
            worldTiles,
            terrainMeshes,
            terrainTextureAssets,
            terrainTextures,
            worldScenery,
            validation,
            trackItemPlacements);
    }
}

public sealed record RouteLoadResult(
    RouteMetadata Metadata,
    RouteAssetPaths Assets,
    TrackDatabase TrackDatabase,
    TrackSectionCatalog GlobalTrackSections,
    TrackSectionCatalog? RouteTrackSections,
    TrackNetwork TrackNetwork,
    SignalNetwork Signalling,
    SignalConfigurationDefinition? SignalConfiguration,
    SignalScriptLibrary? SignalScripts,
    RouteTerrainTileDiscovery TerrainTiles,
    TrackShapeAssetDiscovery TrackShapeAssets,
    RouteTrackShapeMeshLoadResult TrackShapeMeshes,
    IReadOnlyList<TrackShapePlacement> TrackShapePlacements,
    RouteWorldTileDiscovery WorldTiles,
    RouteTerrainMeshLoadResult TerrainMeshes,
    TerrainTextureAssetDiscovery TerrainTextureAssets,
    TerrainTextureAssetLoadResult TerrainTextures,
    RouteWorldSceneryLoadResult WorldScenery,
    RouteValidationReport Validation,
    RouteTrackItemPlacementResult? TrackItemPlacements = null);


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


/// <summary>Loads original track-shape meshes and their referenced route textures.</summary>
public sealed class RouteTrackShapeMeshLoader
{
    public RouteTrackShapeMeshLoadResult Load(
        IVirtualContentStore store,
        string routeRoot,
        TrackShapeAssetDiscovery discovery,
        long maximumTextureBytes = long.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(discovery);
        if (maximumTextureBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumTextureBytes));
        var meshes = new List<TrackShapeAssetMesh>();
        var failed = new List<RouteTrackShapeMeshIssue>();
        foreach (var reference in discovery.Found)
        {
            try { meshes.Add(new TrackShapeAssetLoader().Load(store, reference)); }
            catch (Exception exception) { failed.Add(new RouteTrackShapeMeshIssue(reference.ShapeIndex, reference.Path, exception.Message)); }
        }
        var textureDiscovery = new TrackShapeTextureAssetLocator().Locate(store, routeRoot, meshes);
        var textures = new TrackShapeTextureAssetLoader().Load(store, textureDiscovery, maximumTextureBytes);
        return new RouteTrackShapeMeshLoadResult(meshes, discovery.Missing, failed, textureDiscovery, textures);
    }
}

public sealed record RouteTrackShapeMeshIssue(uint ShapeIndex, string Path, string Reason);
public sealed class RouteTrackShapeMeshLoadResult
{
    public IReadOnlyList<TrackShapeAssetMesh> LoadedMeshes { get; }
    public IReadOnlyList<TrackShapeAssetIssue> MissingMeshes { get; }
    public IReadOnlyList<RouteTrackShapeMeshIssue> FailedMeshes { get; }
    public TrackShapeTextureAssetDiscovery TextureDiscovery { get; }
    public TrackShapeTextureAssetLoadResult Textures { get; }
    public RouteTrackShapeMeshLoadResult(IEnumerable<TrackShapeAssetMesh> loadedMeshes, IEnumerable<TrackShapeAssetIssue> missingMeshes, IEnumerable<RouteTrackShapeMeshIssue> failedMeshes, TrackShapeTextureAssetDiscovery textureDiscovery, TrackShapeTextureAssetLoadResult textures)
    { LoadedMeshes = Array.AsReadOnly(loadedMeshes.ToArray()); MissingMeshes = Array.AsReadOnly(missingMeshes.ToArray()); FailedMeshes = Array.AsReadOnly(failedMeshes.ToArray()); TextureDiscovery = textureDiscovery; Textures = textures; }
}


/// <summary>
/// Controls whether route-wide visual assets are decoded during structural route
/// loading or deferred to the viewport streaming sessions.
/// </summary>
public sealed record RouteLoadOptions(
    bool LoadTerrainMeshes = true,
    bool LoadTerrainTextures = true,
    bool LoadWorldScenery = true)
{
    /// <summary>Maximum decoded route track-shape texture bytes retained by the route load.</summary>
    public long MaximumTrackShapeTextureBytes { get; init; } = 512L * 1024 * 1024;

    public static RouteLoadOptions Eager { get; } = new();
    public static RouteLoadOptions ViewportStreaming { get; } = new(
        LoadTerrainMeshes: false,
        LoadTerrainTextures: false,
        LoadWorldScenery: false);
}

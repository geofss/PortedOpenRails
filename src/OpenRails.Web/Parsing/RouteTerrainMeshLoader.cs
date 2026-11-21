using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Loads the discovered terrain tiles as one connected terrain surface, resolves
/// shared boundary samples, and builds all original patch LOD mesh sets.
/// </summary>
public sealed class RouteTerrainMeshLoader
{
    public RouteTerrainMeshLoadResult Load(
        IVirtualContentStore store,
        RouteTerrainTileDiscovery discovery)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(discovery);

        var loaded = new List<TerrainTileContent>();
        var failed = new List<RouteTerrainTileIssue>();
        var contentLoader = new TerrainTileContentLoader();

        foreach (var reference in discovery.Found)
        {
            try
            {
                loaded.Add(contentLoader.Load(store, reference));
            }
            catch (Exception exception)
            {
                failed.Add(new RouteTerrainTileIssue(reference.TileX, reference.TileZ, reference.Path, exception.Message));
            }
        }

        var sampleResolver = new TerrainTileSampleResolver(loaded);
        var meshBuilder = new TerrainMeshBuilder();
        var meshes = new List<LoadedRouteTerrainTileMesh>();

        foreach (var tile in loaded)
        {
            try
            {
                var lodMeshes = tile.Terrain.PatchSets
                    .Select(patchSet => meshBuilder.Build(tile, patchSet, sampleResolver))
                    .ToArray();
                meshes.Add(new LoadedRouteTerrainTileMesh(tile, lodMeshes));
            }
            catch (Exception exception)
            {
                failed.Add(new RouteTerrainTileIssue(tile.Reference.TileX, tile.Reference.TileZ,
                    tile.Reference.Path, exception.Message));
            }
        }

        return new RouteTerrainMeshLoadResult(loaded, meshes, discovery.Missing, failed);
    }
}

public sealed record LoadedRouteTerrainTileMesh(
    TerrainTileContent Content,
    IReadOnlyList<TerrainTileMesh> LodMeshes);

public sealed record RouteTerrainTileIssue(int TileX, int TileZ, string Path, string Reason);

public sealed class RouteTerrainMeshLoadResult
{
    public IReadOnlyList<TerrainTileContent> LoadedTiles { get; }
    public IReadOnlyList<LoadedRouteTerrainTileMesh> Meshes { get; }
    public IReadOnlyList<(int TileX, int TileZ, string ExpectedPath)> Missing { get; }
    public IReadOnlyList<RouteTerrainTileIssue> Failed { get; }

    public RouteTerrainMeshLoadResult(
        IEnumerable<TerrainTileContent> loadedTiles,
        IEnumerable<LoadedRouteTerrainTileMesh> meshes,
        IEnumerable<(int TileX, int TileZ, string ExpectedPath)> missing,
        IEnumerable<RouteTerrainTileIssue> failed)
    {
        LoadedTiles = Array.AsReadOnly(loadedTiles.ToArray());
        Meshes = Array.AsReadOnly(meshes.ToArray());
        Missing = Array.AsReadOnly(missing.ToArray());
        Failed = Array.AsReadOnly(failed.ToArray());
    }
}

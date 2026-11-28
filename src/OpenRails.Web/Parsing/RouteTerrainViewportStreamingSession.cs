using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Owns the viewport-scoped terrain source/mesh working set. A visibility update
/// loads the selected tiles as one seam-aware batch, retains per-tile meshes in a
/// byte-bounded cache, and drops tiles outside the camera working set.
/// </summary>
public sealed class RouteTerrainViewportStreamingSession : IAsyncDisposable
{
    private readonly RouteTileStreamingCache<string, LoadedRouteTerrainTileMesh> cache;
    private readonly long estimatedBytesPerTile;
    private readonly double seamHaloMeters;

    public RouteTerrainViewportStreamingSession(
        int maximumResidentTiles = 64,
        long maximumResidentBytes = 512L * 1024 * 1024,
        int maximumConcurrentLoads = 2,
        long estimatedBytesPerTile = 32L * 1024 * 1024,
        double seamHaloMeters = RouteViewportTileSelector.TileSizeMeters)
    {
        if (estimatedBytesPerTile < 1)
            throw new ArgumentOutOfRangeException(nameof(estimatedBytesPerTile));
        if (!double.IsFinite(seamHaloMeters) || seamHaloMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(seamHaloMeters));
        this.estimatedBytesPerTile = estimatedBytesPerTile;
        this.seamHaloMeters = seamHaloMeters;
        cache = new RouteTileStreamingCache<string, LoadedRouteTerrainTileMesh>(
            maximumResidentTiles, maximumResidentBytes, maximumConcurrentLoads);
    }

    public long ResidentBytes => cache.ResidentBytes;
    public int ResidentTileCount => cache.Count;

    public async Task<RouteTerrainViewportSnapshot> UpdateCameraAsync(
        IVirtualContentStore store,
        string routeRoot,
        RouteTerrainTileDiscovery discovery,
        double cameraX,
        double cameraZ,
        double radiusMeters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(discovery);
        if (!double.IsFinite(radiusMeters) || radiusMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(radiusMeters));

        var selected = RouteViewportTileSelector.SelectTerrainTiles(
            discovery.Found, cameraX, cameraZ, checked(radiusMeters + seamHaloMeters),
            estimatedBytesPerTile);
        var selectedReferences = selected.Select(request => request.Key).ToArray();
        var referencesByPath = selectedReferences.ToDictionary(
            reference => reference.Path, StringComparer.OrdinalIgnoreCase);
        RouteTerrainMeshLoadResult? seamAwareBatch = null;

        var visible = await cache.UpdateVisibleAsync(
            selected.Select(request => new RouteTileStreamRequest<string>(
                request.Key.Path, request.Priority, request.EstimatedBytes)),
            (path, token) =>
            {
                token.ThrowIfCancellationRequested();
                if (!referencesByPath.TryGetValue(path, out var reference))
                    throw new InvalidDataException($"Viewport requested unknown terrain tile '{path}'.");
                seamAwareBatch ??= new RouteTerrainMeshLoader().Load(
                    store, new RouteTerrainTileDiscovery(selectedReferences, Array.Empty<(int, int, string)>()));
                var mesh = seamAwareBatch.Meshes.FirstOrDefault(candidate =>
                    string.Equals(candidate.Content.Reference.Path, reference.Path, StringComparison.OrdinalIgnoreCase));
                if (mesh is null)
                {
                    var issue = seamAwareBatch.Failed.FirstOrDefault(candidate =>
                        string.Equals(candidate.Path, reference.Path, StringComparison.OrdinalIgnoreCase));
                    throw new InvalidDataException(issue is null
                        ? $"Terrain tile '{reference.Path}' did not produce a mesh."
                        : $"Terrain tile '{reference.Path}' failed: {issue.Reason}");
                }

                return Task.FromResult(new RouteTileStreamAsset<LoadedRouteTerrainTileMesh>(
                    mesh, EstimateResidentBytes(mesh)));
            }, cancellationToken);

        var orderedMeshes = selectedReferences
            .Where(reference => visible.ContainsKey(reference.Path))
            .Select(reference => visible[reference.Path])
            .ToArray();
        var loadedContent = orderedMeshes.Select(mesh => mesh.Content).ToArray();
        var textureDiscovery = new TerrainTextureAssetLocator().Locate(store, routeRoot, loadedContent);
        var textures = new TerrainTextureAssetLoader().Load(store, textureDiscovery);
        var failures = seamAwareBatch?.Failed ?? Array.Empty<RouteTerrainTileIssue>();

        return new RouteTerrainViewportSnapshot(
            Array.AsReadOnly(orderedMeshes),
            textures,
            Array.AsReadOnly(failures.ToArray()),
            cache.ResidentBytes,
            cache.Count);
    }

    private static long EstimateResidentBytes(LoadedRouteTerrainTileMesh tile)
    {
        // Terrain altitude and flag grids are retained alongside decoded patch
        // vertices and indices. Count their backing arrays, not just GPU buffers.
        var sampleCount = tile.Content.Terrain.Samples.SampleCount;
        var rawGridBytes = checked((long)sampleCount * sampleCount *
            (sizeof(ushort) + (tile.Content.Flags is null ? 0 : sizeof(byte))));
        var meshBytes = tile.LodMeshes.Aggregate(0L, (total, lod) =>
            checked(total + lod.Patches.Aggregate(0L, (patchTotal, patch) =>
                checked(patchTotal +
                    (long)patch.Vertices.Count * 32 +
                    (long)patch.Indices.Count * sizeof(int)))));
        var patchMetadataBytes = checked((long)tile.Content.Terrain.PatchSets.Sum(set => set.Patches.Count) * 128);
        return checked(rawGridBytes + meshBytes + patchMetadataBytes + 64 * 1024);
    }

    public ValueTask DisposeAsync() => cache.DisposeAsync();
}

public sealed record RouteTerrainViewportSnapshot(
    IReadOnlyList<LoadedRouteTerrainTileMesh> Meshes,
    TerrainTextureAssetLoadResult Textures,
    IReadOnlyList<RouteTerrainTileIssue> Failed,
    long ResidentBytes,
    int ResidentTileCount);

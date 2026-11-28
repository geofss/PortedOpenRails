using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Owns the viewport-scoped terrain source/mesh working set. A visibility update
/// loads the selected tiles as one seam-aware batch, retains per-tile meshes in a
/// byte-bounded cache, and drops tiles outside the camera working set.
/// </summary>
public sealed class RouteTerrainViewportStreamingSession : IAsyncDisposable
{
    private readonly RouteTileStreamingCache<string, TerrainTileStreamAsset> cache;
    private readonly long estimatedBytesPerTile;
    private readonly long maximumResidentBytes;
    private readonly long maximumEstimatedGpuBytes;
    private readonly int maximumVisibleTiles;
    private readonly double seamHaloMeters;
    private long estimatedGpuBytes;

    public RouteTerrainViewportStreamingSession(
        int maximumResidentTiles = 64,
        long maximumResidentBytes = 512L * 1024 * 1024,
        int maximumConcurrentLoads = 2,
        long estimatedBytesPerTile = 32L * 1024 * 1024,
        double seamHaloMeters = RouteViewportTileSelector.TileSizeMeters,
        long maximumEstimatedGpuBytes = 384L * 1024 * 1024)
    {
        if (estimatedBytesPerTile < 1)
            throw new ArgumentOutOfRangeException(nameof(estimatedBytesPerTile));
        if (!double.IsFinite(seamHaloMeters) || seamHaloMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(seamHaloMeters));
        if (maximumResidentTiles < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumResidentTiles));
        if (maximumResidentBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumResidentBytes));
        if (maximumEstimatedGpuBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumEstimatedGpuBytes));
        this.estimatedBytesPerTile = estimatedBytesPerTile;
        this.maximumResidentBytes = maximumResidentBytes;
        this.maximumEstimatedGpuBytes = maximumEstimatedGpuBytes;
        maximumVisibleTiles = (int)Math.Min(maximumResidentTiles, maximumResidentBytes / estimatedBytesPerTile);
        this.seamHaloMeters = seamHaloMeters;
        cache = new RouteTileStreamingCache<string, TerrainTileStreamAsset>(
            maximumResidentTiles, maximumResidentBytes, maximumConcurrentLoads);
    }

    public long ResidentBytes => cache.ResidentBytes;
    public int ResidentTileCount => cache.Count;
    public long EstimatedGpuBytes => Interlocked.Read(ref estimatedGpuBytes);

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
                estimatedBytesPerTile)
            .Take(maximumVisibleTiles)
            .ToArray();
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
                        string.Equals(candidate.Path, reference.Path, StringComparison.OrdinalIgnoreCase)) ??
                        new RouteTerrainTileIssue(reference.TileX, reference.TileZ, reference.Path,
                            "The tile did not produce a terrain mesh.");
                    return Task.FromResult(new RouteTileStreamAsset<TerrainTileStreamAsset>(
                        new TerrainTileStreamAsset(null, issue), 1024));
                }

                return Task.FromResult(new RouteTileStreamAsset<TerrainTileStreamAsset>(
                    new TerrainTileStreamAsset(mesh, null), EstimateResidentBytes(mesh)));
            }, cancellationToken);

        var orderedMeshes = new List<LoadedRouteTerrainTileMesh>();
        var failures = new List<RouteTerrainTileIssue>();
        long visibleMeshBytes = 0;
        long estimatedGpuMeshBytes = 0;
        foreach (var reference in selectedReferences)
        {
            if (!visible.TryGetValue(reference.Path, out var asset))
                continue;
            if (asset.Loaded is null)
            {
                if (asset.Failure is not null)
                    failures.Add(asset.Failure);
                continue;
            }
            var bytes = EstimateResidentBytes(asset.Loaded);
            var gpuBytes = EstimateGpuBytes(asset.Loaded);
            if (bytes > maximumResidentBytes - visibleMeshBytes ||
                gpuBytes > maximumEstimatedGpuBytes - estimatedGpuMeshBytes)
                continue;
            orderedMeshes.Add(asset.Loaded);
            visibleMeshBytes += bytes;
            estimatedGpuMeshBytes += gpuBytes;
        }

        var loadedContent = orderedMeshes.Select(mesh => mesh.Content).ToArray();
        var textureDiscovery = new TerrainTextureAssetLocator().Locate(store, routeRoot, loadedContent);
        var decodedTextures = new TerrainTextureAssetLoader().Load(store, textureDiscovery);
        var visibleTextures = new List<LoadedTerrainTextureAsset>();
        long textureBytes = 0;
        foreach (var texture in decodedTextures.Loaded)
        {
            var bytes = texture.Texture.MipLevels.Sum(mip => (long)mip.Length);
            if (bytes > maximumResidentBytes - visibleMeshBytes - textureBytes ||
                bytes > maximumEstimatedGpuBytes - estimatedGpuMeshBytes - textureBytes)
                continue;
            visibleTextures.Add(texture);
            textureBytes += bytes;
        }
        var textures = new TerrainTextureAssetLoadResult(
            visibleTextures, decodedTextures.Missing, decodedTextures.Failed);
        var currentGpuBytes = checked(estimatedGpuMeshBytes + textureBytes);
        Interlocked.Exchange(ref estimatedGpuBytes, currentGpuBytes);

        return new RouteTerrainViewportSnapshot(
            Array.AsReadOnly(orderedMeshes.ToArray()),
            textures,
            Array.AsReadOnly(failures.ToArray()),
            checked(visibleMeshBytes + textureBytes),
            cache.Count,
            currentGpuBytes);
    }

    private static long EstimateGpuBytes(LoadedRouteTerrainTileMesh tile) =>
        tile.LodMeshes.Aggregate(0L, (total, lod) =>
            checked(total + lod.Patches.Aggregate(0L, (patchTotal, patch) =>
                checked(patchTotal + (long)patch.Vertices.Count * 32 +
                    (long)patch.Indices.Count * sizeof(int)))));

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

    private sealed record TerrainTileStreamAsset(
        LoadedRouteTerrainTileMesh? Loaded,
        RouteTerrainTileIssue? Failure);

    public async ValueTask ClearAsync()
    {
        Interlocked.Exchange(ref estimatedGpuBytes, 0);
        await cache.ClearAsync().ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => cache.DisposeAsync();
}

public sealed record RouteTerrainViewportSnapshot(
    IReadOnlyList<LoadedRouteTerrainTileMesh> Meshes,
    TerrainTextureAssetLoadResult Textures,
    IReadOnlyList<RouteTerrainTileIssue> Failed,
    long ResidentBytes,
    int ResidentTileCount,
    long EstimatedGpuBytes = 0);

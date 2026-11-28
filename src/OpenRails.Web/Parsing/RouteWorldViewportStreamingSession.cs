using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Camera-scoped WORLD tile loader with a bounded decoded-asset working set.
/// Leaving the viewport drops tile, shape, placement and decoded texture references.
/// </summary>
public sealed class RouteWorldViewportStreamingSession : IAsyncDisposable
{
    private readonly RouteTileStreamingCache<string, WorldTileStreamAsset> cache;
    private readonly long maximumResidentBytes;
    private readonly long estimatedBytesPerTile;
    private readonly int maximumVisibleTiles;

    public RouteWorldViewportStreamingSession(
        int maximumResidentTiles = 48,
        long maximumResidentBytes = 768L * 1024 * 1024,
        int maximumConcurrentLoads = 2,
        long estimatedBytesPerTile = 16L * 1024 * 1024)
    {
        if (maximumResidentTiles < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumResidentTiles));
        if (maximumResidentBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumResidentBytes));
        if (estimatedBytesPerTile < 1)
            throw new ArgumentOutOfRangeException(nameof(estimatedBytesPerTile));
        this.maximumResidentBytes = maximumResidentBytes;
        this.estimatedBytesPerTile = estimatedBytesPerTile;
        maximumVisibleTiles = Math.Max(1, (int)Math.Min(maximumResidentTiles, maximumResidentBytes / estimatedBytesPerTile));
        cache = new RouteTileStreamingCache<string, WorldTileStreamAsset>(
            maximumResidentTiles, maximumResidentBytes, maximumConcurrentLoads);
    }

    public long ResidentBytes => cache.ResidentBytes;
    public int ResidentTileCount => cache.Count;

    public async Task<RouteWorldViewportSnapshot> UpdateCameraAsync(
        IVirtualContentStore store,
        string routeRoot,
        RouteWorldTileDiscovery discovery,
        double cameraX,
        double cameraZ,
        double radiusMeters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(discovery);
        var selected = RouteViewportTileSelector.SelectWorldTiles(
                discovery.Found, cameraX, cameraZ, radiusMeters, estimatedBytesPerTile)
            .Take(maximumVisibleTiles)
            .ToArray();
        var references = selected.Select(request => request.Key).ToArray();
        var byPath = references.ToDictionary(reference => reference.Path, StringComparer.OrdinalIgnoreCase);

        var visible = await cache.UpdateVisibleAsync(
            selected.Select(request => new RouteTileStreamRequest<string>(
                request.Key.Path, request.Priority, request.EstimatedBytes)),
            (path, token) =>
            {
                token.ThrowIfCancellationRequested();
                if (!byPath.TryGetValue(path, out var reference))
                    throw new InvalidDataException($"Viewport requested unknown WORLD tile '{path}'.");
                try
                {
                    var tile = new WorldSceneryTileLoader().Load(store, routeRoot, reference, cancellationToken: token);
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult(new RouteTileStreamAsset<WorldTileStreamAsset>(
                        new WorldTileStreamAsset(tile, null), EstimateResidentBytes(tile)));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception exception)
                {
                    return Task.FromResult(new RouteTileStreamAsset<WorldTileStreamAsset>(
                        new WorldTileStreamAsset(null,
                            new RouteWorldSceneryIssue(reference.TileX, reference.TileZ, reference.Path, exception.Message)),
                        1024));
                }
            }, cancellationToken);

        var loaded = new List<LoadedWorldSceneryTile>();
        var failed = new List<RouteWorldSceneryIssue>();
        long visibleBytes = 0;
        foreach (var reference in references)
        {
            if (!visible.TryGetValue(reference.Path, out var asset))
                continue;
            var bytes = asset.Loaded is null ? 1024 : EstimateResidentBytes(asset.Loaded);
            if (bytes > maximumResidentBytes - visibleBytes)
                continue;
            visibleBytes += bytes;
            if (asset.Loaded is not null)
                loaded.Add(asset.Loaded);
            if (asset.Failure is not null)
                failed.Add(asset.Failure);
        }

        return new RouteWorldViewportSnapshot(
            Array.AsReadOnly(loaded.ToArray()),
            Array.AsReadOnly(failed.ToArray()),
            visibleBytes,
            cache.Count);
    }

    private static long EstimateResidentBytes(LoadedWorldSceneryTile tile)
    {
        long bytes = 64 * 1024;
        bytes = checked(bytes + (long)tile.Placements.Count * 512);
        bytes = checked(bytes + (long)tile.Tile.Diagnostics.Count * 256);
        bytes = checked(bytes + (long)tile.ShapeDiscovery.Found.Count * 256);
        bytes = checked(bytes + (long)tile.FailedShapes.Count * 256);

        foreach (var shape in tile.ShapeMeshes)
        {
            var mesh = shape.Mesh;
            bytes = checked(bytes + (long)mesh.Matrices.Count * 64);
            foreach (var lod in mesh.Lods)
            {
                bytes = checked(bytes + (long)lod.Hierarchy.Count * sizeof(int));
                foreach (var subObject in lod.SubObjects)
                {
                    bytes = checked(bytes + (long)subObject.GeometryNodeMap.Count * sizeof(int));
                    foreach (var vertex in subObject.Vertices)
                        bytes = checked(bytes + 44L +
                            (long)(vertex.AdditionalTextureCoordinates?.Count ?? 0) * 8);
                    foreach (var primitive in subObject.Primitives)
                    {
                        bytes = checked(bytes + (long)primitive.Indices.Count * sizeof(int));
                        bytes = checked(bytes + (long)primitive.Hierarchy.Count * sizeof(int));
                        bytes = checked(bytes + (long)(primitive.TextureBindings?.Count ?? 0) * 64);
                        bytes = checked(bytes + (long)(primitive.UvOperations?.Count ?? 0) * 32 + 256);
                    }
                }
            }
        }

        foreach (var texture in tile.Textures.Loaded.Concat(tile.ForestTextures.Loaded))
            foreach (var mip in texture.Texture.MipLevels)
                bytes = checked(bytes + mip.Length);

        return bytes;
    }

    public ValueTask ClearAsync() => cache.ClearAsync();
    public ValueTask DisposeAsync() => cache.DisposeAsync();

    private sealed record WorldTileStreamAsset(
        LoadedWorldSceneryTile? Loaded,
        RouteWorldSceneryIssue? Failure);
}

public sealed record RouteWorldViewportSnapshot(
    IReadOnlyList<LoadedWorldSceneryTile> Loaded,
    IReadOnlyList<RouteWorldSceneryIssue> Failed,
    long ResidentBytes,
    int ResidentTileCount);

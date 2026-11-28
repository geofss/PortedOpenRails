namespace OpenRails.Web.Parsing;

/// <summary>
/// Camera-driven bounded cache for route tiles and decoded assets. Each visibility
/// update supersedes the previous generation, cancels obsolete work, and evicts
/// obsolete entries and retains only assets that fit the configured entry and byte budgets.
/// </summary>
public sealed class RouteTileStreamingCache<TKey, TAsset> : IAsyncDisposable
    where TKey : notnull
{
    private readonly object gate = new();
    private readonly Dictionary<TKey, CacheEntry> entries = [];
    private readonly SemaphoreSlim concurrency;
    private readonly int maximumEntries;
    private readonly long maximumBytes;
    private CancellationTokenSource? generation;
    private long clock;
    private long residentBytes;
    private bool disposed;

    public RouteTileStreamingCache(int maximumEntries, long maximumBytes, int maximumConcurrentLoads = 4)
    {
        if (maximumEntries < 1) throw new ArgumentOutOfRangeException(nameof(maximumEntries));
        if (maximumBytes < 1) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        if (maximumConcurrentLoads < 1) throw new ArgumentOutOfRangeException(nameof(maximumConcurrentLoads));
        this.maximumEntries = maximumEntries;
        this.maximumBytes = maximumBytes;
        concurrency = new SemaphoreSlim(maximumConcurrentLoads, maximumConcurrentLoads);
    }

    public long ResidentBytes { get { lock (gate) return residentBytes; } }
    public int Count { get { lock (gate) return entries.Count; } }

    /// <summary>
    /// Loads the requested visible tiles in descending priority. Calling again
    /// cancels the prior generation; tiles no longer requested are unloaded.
    /// Assets that cannot fit alongside the currently visible set are returned uncached;
    /// the caller owns and must dispose those transient assets.
    /// </summary>
    public async Task<IReadOnlyDictionary<TKey, TAsset>> UpdateVisibleAsync(
        IEnumerable<RouteTileStreamRequest<TKey>> requests,
        Func<TKey, CancellationToken, Task<RouteTileStreamAsset<TAsset>>> loader,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(loader);
        var ordered = requests.OrderByDescending(request => request.Priority).ToArray();
        if (ordered.Any(request => !double.IsFinite(request.Priority) || request.EstimatedBytes < 0))
            throw new ArgumentException("Tile priorities and estimated byte counts must be finite and non-negative.", nameof(requests));
        if (ordered.Select(request => request.Key).Distinct().Count() != ordered.Length)
            throw new ArgumentException("Visible tile keys must be unique.", nameof(requests));

        CancellationTokenSource current;
        List<TAsset> unload;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            generation?.Cancel();
            generation?.Dispose();
            generation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            current = generation;
            var keep = ordered.Select(request => request.Key).ToHashSet();
            unload = [];
            foreach (var key in entries.Keys.Where(key => !keep.Contains(key)).ToArray())
                if (RemoveEntry(key, out var removedAsset))
                    unload.Add(removedAsset);
        }
        await DisposeAssetsAsync(unload).ConfigureAwait(false);

        var result = new Dictionary<TKey, TAsset>();
        foreach (var request in ordered)
        {
            current.Token.ThrowIfCancellationRequested();
            CacheEntry? cached;
            lock (gate)
            {
                if (entries.TryGetValue(request.Key, out cached))
                {
                    cached.LastUse = ++clock;
                    result[request.Key] = cached.Asset;
                    continue;
                }
            }

            await concurrency.WaitAsync(current.Token).ConfigureAwait(false);
            RouteTileStreamAsset<TAsset> loaded;
            try { loaded = await loader(request.Key, current.Token).ConfigureAwait(false); }
            finally { concurrency.Release(); }
            if (loaded.Asset is null || loaded.ResidentBytes < 0)
            {
                await DisposeAssetAsync(loaded.Asset).ConfigureAwait(false);
                throw new InvalidDataException("A streamed tile returned a null asset or negative resident size.");
            }

            var retained = false;
            var evicted = new List<TAsset>();
            var pinnedKeys = ordered.Select(item => item.Key).ToHashSet();
            lock (gate)
            {
                if (!disposed && ReferenceEquals(generation, current) && !current.IsCancellationRequested &&
                    loaded.ResidentBytes <= maximumBytes)
                {
                    while (entries.Count >= maximumEntries || residentBytes > maximumBytes - loaded.ResidentBytes)
                    {
                        var evictionCandidates = entries
                            .Where(pair => !pinnedKeys.Contains(pair.Key))
                            .ToArray();
                        if (evictionCandidates.Length == 0) break;
                        var oldest = evictionCandidates.MinBy(pair => pair.Value.LastUse);
                        if (RemoveEntry(oldest.Key, out var evictedAsset))
                            evicted.Add(evictedAsset);
                    }
                    if (entries.Count < maximumEntries && residentBytes <= maximumBytes - loaded.ResidentBytes)
                    {
                        entries[request.Key] = new CacheEntry(loaded.Asset, loaded.ResidentBytes, ++clock);
                        residentBytes += loaded.ResidentBytes;
                        retained = true;
                    }
                }
            }
            await DisposeAssetsAsync(evicted).ConfigureAwait(false);
            if (!retained && current.IsCancellationRequested)
            {
                await DisposeAssetAsync(loaded.Asset).ConfigureAwait(false);
                current.Token.ThrowIfCancellationRequested();
            }
            if (retained || !current.IsCancellationRequested)
                result[request.Key] = loaded.Asset;
            else
                await DisposeAssetAsync(loaded.Asset).ConfigureAwait(false);
        }
        current.Token.ThrowIfCancellationRequested();
        return new System.Collections.ObjectModel.ReadOnlyDictionary<TKey, TAsset>(result);
    }

    public async ValueTask ClearAsync()
    {
        List<TAsset> unload;
        lock (gate)
        {
            generation?.Cancel();
            unload = entries.Values.Select(entry => entry.Asset).ToList();
            entries.Clear();
            residentBytes = 0;
        }
        await DisposeAssetsAsync(unload).ConfigureAwait(false);
    }

    private bool RemoveEntry(TKey key, out TAsset asset)
    {
        if (!entries.Remove(key, out var entry))
        {
            asset = default!;
            return false;
        }
        residentBytes -= entry.ResidentBytes;
        asset = entry.Asset;
        return true;
    }

    private static async ValueTask DisposeAssetsAsync(IEnumerable<TAsset> assets)
    {
        foreach (var asset in assets)
            await DisposeAssetAsync(asset).ConfigureAwait(false);
    }

    private static async ValueTask DisposeAssetAsync(TAsset asset)
    {
        if (asset is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        else if (asset is IDisposable disposable)
            disposable.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
        }
        await ClearAsync().ConfigureAwait(false);
        generation?.Dispose();
        generation = null;
        concurrency.Dispose();
    }

    private sealed class CacheEntry(TAsset asset, long residentBytes, long lastUse)
    {
        public TAsset Asset { get; } = asset;
        public long ResidentBytes { get; } = residentBytes;
        public long LastUse { get; set; } = lastUse;
    }
}

public sealed record RouteTileStreamRequest<TKey>(TKey Key, double Priority, long EstimatedBytes)
    where TKey : notnull;

public sealed record RouteTileStreamAsset<TAsset>(TAsset Asset, long ResidentBytes);

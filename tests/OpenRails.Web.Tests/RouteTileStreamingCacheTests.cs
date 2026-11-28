using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class RouteTileStreamingCacheTests
{
    [Fact]
    public async Task LoadsVisibleTilesAndKeepsResidentBytesWithinBudget()
    {
        await using var cache = new RouteTileStreamingCache<int, string>(
            maximumEntries: 3, maximumBytes: 100, maximumConcurrentLoads: 2);

        var loaded = await cache.UpdateVisibleAsync(
            new[]
            {
                new RouteTileStreamRequest<int>(1, 10, 40),
                new RouteTileStreamRequest<int>(2, 5, 40)
            },
            (key, _) => Task.FromResult(new RouteTileStreamAsset<string>($"tile-{key}", 40)));

        Assert.Equal(new[] { 1, 2 }, loaded.Keys.Order().ToArray());
        Assert.Equal(80, cache.ResidentBytes);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public async Task UnloadsTilesOutsideVisibleSetAndReleasesTheirBudget()
    {
        await using var cache = new RouteTileStreamingCache<int, string>(4, 100);
        await cache.UpdateVisibleAsync(
            new[] { new RouteTileStreamRequest<int>(1, 1, 30), new RouteTileStreamRequest<int>(2, 2, 30) },
            (key, _) => Task.FromResult(new RouteTileStreamAsset<string>($"tile-{key}", 30)));

        var visible = await cache.UpdateVisibleAsync(
            new[] { new RouteTileStreamRequest<int>(2, 1, 30) },
            (key, _) => throw new InvalidOperationException("Cached tile should not reload."));

        Assert.Equal("tile-2", visible[2]);
        Assert.Equal(30, cache.ResidentBytes);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public async Task EvictsLeastRecentlyUsedTilesToRespectByteBudget()
    {
        await using var cache = new RouteTileStreamingCache<int, string>(4, 60);
        await cache.UpdateVisibleAsync(
            new[] { new RouteTileStreamRequest<int>(1, 1, 30), new RouteTileStreamRequest<int>(2, 2, 30) },
            (key, _) => Task.FromResult(new RouteTileStreamAsset<string>($"tile-{key}", 30)));

        await cache.UpdateVisibleAsync(
            new[] { new RouteTileStreamRequest<int>(1, 3, 30), new RouteTileStreamRequest<int>(3, 2, 40) },
            (key, _) => Task.FromResult(new RouteTileStreamAsset<string>($"tile-{key}", 40)));

        Assert.Equal(30, cache.ResidentBytes);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public async Task CancellationStopsTheCurrentVisibilityGeneration()
    {
        await using var cache = new RouteTileStreamingCache<int, string>(4, 100);
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var loading = cache.UpdateVisibleAsync(
            new[] { new RouteTileStreamRequest<int>(1, 1, 10) },
            async (_, token) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new RouteTileStreamAsset<string>("unreachable", 10);
            }, cancellation.Token);

        await started.Task;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loading);
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.ResidentBytes);
    }

    [Fact]
    public async Task DoesNotCacheAnAssetLargerThanTheWholeBudget()
    {
        await using var cache = new RouteTileStreamingCache<int, string>(2, 20);
        var visible = await cache.UpdateVisibleAsync(
            new[] { new RouteTileStreamRequest<int>(1, 1, 30) },
            (_, _) => Task.FromResult(new RouteTileStreamAsset<string>("oversize", 30)));

        Assert.Equal("oversize", visible[1]);
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.ResidentBytes);
    }
}

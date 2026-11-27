using Microsoft.JSInterop;

namespace OpenRails.Web.Content;

/// <summary>
/// Asynchronous, disk-backed access to individual files inside a staged ZIP.
/// Unlike IVirtualContentStore, this API never pretends OPFS reads are synchronous.
/// Each entry is decompressed and CRC-checked as it is consumed.
/// </summary>
public static class BrowserPackageArchiveContentStore
{
    public static async Task<IReadOnlyList<string>> ListPathsAsync(IJSRuntime js, string archiveId)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveId);
        var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/route-package-indexer.js");
        try
        {
            return await module.InvokeAsync<string[]>("listEntries", archiveId);
        }
        finally
        {
            await module.DisposeAsync();
        }
    }

    public static async Task<BrowserPackageEntryReadStream> OpenReadAsync(
        IJSRuntime js,
        string archiveId,
        string virtualPath,
        long maximumEntryBytes = BrowserPackageArchiveLimits.MaxArchiveBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveId);
        ArgumentException.ThrowIfNullOrWhiteSpace(virtualPath);
        if (maximumEntryBytes <= 0 || maximumEntryBytes > BrowserPackageArchiveLimits.MaxArchiveBytes)
            throw new ArgumentOutOfRangeException(nameof(maximumEntryBytes));

        var module = await js.InvokeAsync<IJSObjectReference>(
            "import", cancellationToken, "./js/route-package-indexer.js");
        try
        {
            var reference = await module.InvokeAsync<IJSStreamReference>(
                "openEntry", cancellationToken, archiveId, virtualPath);
            try
            {
                var stream = await reference.OpenReadStreamAsync(maximumEntryBytes, cancellationToken);
                return new BrowserPackageEntryReadStream(module, reference, stream);
            }
            catch
            {
                await reference.DisposeAsync();
                throw;
            }
        }
        catch
        {
            await module.DisposeAsync();
            throw;
        }
    }
}

/// <summary>Owns the JS module, JS stream reference, and .NET read stream.</summary>
public sealed class BrowserPackageEntryReadStream(
    IJSObjectReference module,
    IJSStreamReference reference,
    Stream stream) : IAsyncDisposable
{
    private bool disposed;

    public Stream Stream { get; } = stream ?? throw new ArgumentNullException(nameof(stream));

    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;
        disposed = true;

        // Dispose every owned browser/.NET resource even when an earlier
        // disposal operation fails, so JS references are not leaked.
        try
        {
            await Stream.DisposeAsync();
        }
        finally
        {
            try
            {
                await reference.DisposeAsync();
            }
            finally
            {
                await module.DisposeAsync();
            }
        }
    }
}

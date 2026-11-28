using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace OpenRails.Web.Content;

/// <summary>
/// Streams large user-selected ZIP files directly into the browser Origin Private
/// File System (OPFS), without materializing the archive in .NET or JavaScript memory.
/// </summary>
public static class BrowserPackageArchiveUploader
{
    public static async Task<IReadOnlyList<StoredBrowserPackage>> ListAsync(IJSRuntime js)
    {
        ArgumentNullException.ThrowIfNull(js);
        var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/route-package-storage.js");
        try
        {
            return await module.InvokeAsync<StoredBrowserPackage[]>("listArchives");
        }
        finally
        {
            await module.DisposeAsync();
        }
    }

    public static async Task DeleteAsync(IJSRuntime js, string id)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/route-package-storage.js");
        try
        {
            await module.InvokeVoidAsync("deleteArchive", id);
        }
        finally
        {
            await module.DisposeAsync();
        }
    }

    public static async Task<StoredBrowserPackage> StoreAsync(
        IJSRuntime js,
        IBrowserFile file,
        DotNetObjectReference<BrowserPackageUploadProgress> progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(progress);

        if (file.Size <= 0)
            throw new InvalidDataException("The selected package is empty.");
        if (file.Size > BrowserPackageArchiveLimits.MaxArchiveBytes)
            throw new InvalidDataException($"The selected package exceeds the 30 GB browser-storage limit.");

        await using var source = file.OpenReadStream(BrowserPackageArchiveLimits.MaxArchiveBytes, cancellationToken);
        using var streamReference = new DotNetStreamReference(source);
        var module = await js.InvokeAsync<IJSObjectReference>(
            "import", cancellationToken, "./js/route-package-storage.js");
        try
        {
            return await module.InvokeAsync<StoredBrowserPackage>(
                "storeArchive",
                cancellationToken,
                streamReference,
                file.Name,
                file.Size,
                BrowserPackageArchiveLimits.MaxArchiveBytes,
                BrowserPackageArchiveLimits.MaximumQuotaSafetyMarginBytes,
                progress);
        }
        finally
        {
            await module.DisposeAsync();
        }
    }
}

/// <summary>Progress callback invoked by the OPFS streaming writer.</summary>
public sealed class BrowserPackageUploadProgress(Action<long, long> report)
{
    [JSInvokable]
    public Task Report(long transferredBytes, long totalBytes)
    {
        report(transferredBytes, totalBytes);
        return Task.CompletedTask;
    }
}

/// <summary>Metadata for a successfully staged browser archive.</summary>
public sealed record StoredBrowserPackage(string Id, string FileName, long SizeBytes, bool PersistentStorageGranted);

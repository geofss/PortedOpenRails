using Microsoft.JSInterop;

namespace OpenRails.Web.Content;

/// <summary>
/// ZIP directory information read directly from an archive already stored in OPFS.
/// This indexes the archive without copying its contents into the .NET heap.
/// </summary>
public static class BrowserPackageArchiveIndexer
{
    public static async Task<BrowserPackageArchiveIndex> InspectAsync(IJSRuntime js, string id)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/route-package-indexer.js");
        try
        {
            return await module.InvokeAsync<BrowserPackageArchiveIndex>("inspectArchive", id);
        }
        finally
        {
            await module.DisposeAsync();
        }
    }
}

/// <summary>Bounded summary of an OPFS ZIP central directory.</summary>
public sealed record BrowserPackageArchiveIndex(
    string Id,
    string FileName,
    long ArchiveSizeBytes,
    int EntryCount,
    long TotalUncompressedBytes,
    IReadOnlyList<string> RouteFiles);

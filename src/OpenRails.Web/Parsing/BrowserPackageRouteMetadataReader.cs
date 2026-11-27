using OpenRails.Web.Content;
using Microsoft.JSInterop;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Reads a route's TRK metadata from an OPFS-backed archive entry using the
/// original Open Rails STF parser. Only the small metadata entry is buffered;
/// the containing ZIP and its other entries remain on disk.
/// </summary>
public static class BrowserPackageRouteMetadataReader
{
    public const long MaximumRouteMetadataBytes = 16L * 1024 * 1024;

    public static async Task<RouteMetadata> ReadAsync(
        IJSRuntime js,
        string archiveId,
        string routeFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(js);
        var path = VirtualPath.Normalize(routeFilePath);
        await using var entry = await BrowserPackageArchiveContentStore.OpenReadAsync(
            js, archiveId, path, MaximumRouteMetadataBytes, cancellationToken);
        await using var buffer = new MemoryStream();
        await entry.Stream.CopyToAsync(buffer, 81920, cancellationToken);
        if (buffer.Length == 0)
            throw new InvalidDataException($"Route metadata entry is empty: {path}");

        var store = new SingleEntryContentStore(path, buffer.ToArray());
        return new RouteMetadataReader().Read(store, path);
    }

    private sealed class SingleEntryContentStore(string path, byte[] content) : IVirtualContentStore
    {
        private readonly string normalizedPath = VirtualPath.Normalize(path);
        public IReadOnlyCollection<string> Paths { get; } = Array.AsReadOnly(new[] { VirtualPath.Normalize(path) });

        public bool Contains(string candidate) =>
            string.Equals(VirtualPath.Normalize(candidate), normalizedPath, StringComparison.OrdinalIgnoreCase);

        public Stream OpenRead(string candidate)
        {
            if (!Contains(candidate))
                throw new FileNotFoundException($"Virtual content file not found: {candidate}", candidate);
            return new MemoryStream(content, writable: false);
        }

        public bool TryOpenRead(string candidate, out Stream? stream)
        {
            if (!Contains(candidate))
            {
                stream = null;
                return false;
            }
            stream = new MemoryStream(content, writable: false);
            return true;
        }
    }
}

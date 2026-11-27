using Microsoft.JSInterop;

namespace OpenRails.Web.Content;

/// <summary>
/// Materializes a staged OPFS archive entry-by-entry into a bounded virtual store.
/// This is the compatibility bridge for the current synchronous upstream route readers.
/// It never copies the compressed ZIP as one giant buffer, and it refuses packages whose
/// expanded content would exceed the explicit desktop-memory budget.
/// </summary>
public sealed class BrowserPackageVirtualContentStore : IVirtualContentStore, IDisposable
{
    public const long MaximumExpandedPackageBytes = 2L * 1024 * 1024 * 1024;
    public const long MaximumEntryBytes = 512L * 1024 * 1024;

    private readonly Dictionary<string, byte[]> entries;
    private bool disposed;

    public IReadOnlyCollection<string> Paths { get; }

    private BrowserPackageVirtualContentStore(Dictionary<string, byte[]> entries)
    {
        this.entries = entries;
        Paths = Array.AsReadOnly(entries.Keys.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public static async Task<BrowserPackageVirtualContentStore> LoadAsync(
        IJSRuntime js,
        string archiveId,
        IProgress<(int Completed, int Total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveId);

        var module = await js.InvokeAsync<IJSObjectReference>(
            "import", cancellationToken, "./js/route-package-indexer.js");
        var loaded = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;
        try
        {
            var paths = await module.InvokeAsync<string[]>("listEntries", cancellationToken, archiveId);
            foreach (var rawPath in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = VirtualPath.Normalize(rawPath);
                var reference = await module.InvokeAsync<IJSStreamReference>(
                    "openEntry", cancellationToken, archiveId, path);
                try
                {
                    await using var source = await reference.OpenReadStreamAsync(MaximumEntryBytes, cancellationToken);
                    await using var target = new MemoryStream();
                    var buffer = new byte[81920];
                    while (true)
                    {
                        var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
                        if (read == 0)
                            break;
                        totalBytes = checked(totalBytes + read);
                        if (totalBytes > MaximumExpandedPackageBytes)
                            throw new InvalidDataException(
                                $"Expanded route package exceeds the {MaximumExpandedPackageBytes / (1024d * 1024 * 1024):N0} GiB browser compatibility budget.");
                        if (target.Length + read > MaximumEntryBytes)
                            throw new InvalidDataException($"Route package entry exceeds the supported size: {path}");
                        await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    }
                    if (!loaded.TryAdd(path, target.ToArray()))
                        throw new InvalidDataException($"Duplicate virtual content path in staged archive: {path}");
                }
                finally
                {
                    await reference.DisposeAsync();
                }
                progress?.Report((loaded.Count, paths.Length));
            }

            if (loaded.Count == 0)
                throw new InvalidDataException("The staged archive contains no files.");
            return new BrowserPackageVirtualContentStore(loaded);
        }
        catch
        {
            loaded.Clear();
            throw;
        }
        finally
        {
            await module.DisposeAsync();
        }
    }

    public bool Contains(string path)
    {
        ThrowIfDisposed();
        return entries.ContainsKey(VirtualPath.Normalize(path));
    }

    public Stream OpenRead(string path)
    {
        ThrowIfDisposed();
        var normalized = VirtualPath.Normalize(path);
        return entries.TryGetValue(normalized, out var bytes)
            ? new MemoryStream(bytes, writable: false)
            : throw new FileNotFoundException($"Virtual content file not found: {normalized}", normalized);
    }

    public bool TryOpenRead(string path, out Stream? stream)
    {
        ThrowIfDisposed();
        if (entries.TryGetValue(VirtualPath.Normalize(path), out var bytes))
        {
            stream = new MemoryStream(bytes, writable: false);
            return true;
        }
        stream = null;
        return false;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        entries.Clear();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
}

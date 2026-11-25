using System.IO.Compression;

namespace OpenRails.Web.Content;

/// <summary>
/// Read-only access to files in a logical Open Rails/MSTS content namespace.
/// Implementations must not expose host filesystem paths.
/// </summary>
public interface IVirtualContentStore
{
    IReadOnlyCollection<string> Paths { get; }
    bool Contains(string path);
    Stream OpenRead(string path);
    bool TryOpenRead(string path, out Stream? stream);
}

/// <summary>
/// A read-only virtual content store backed by a user-provided ZIP package.
/// The archive and its source stream are owned by this store.
/// </summary>
public sealed class ZipContentStore : IVirtualContentStore, IDisposable
{
    public const long DefaultMaxEntryBytes = 128L * 1024 * 1024;
    public const long DefaultMaxBrowserArchiveBytes = 300L * 1024 * 1024;
    public const long DefaultMaxTotalBytes = 512L * 1024 * 1024;
    public const int DefaultMaxEntries = 100_000;

    private readonly ZipArchive archive;
    private readonly Dictionary<string, ZipArchiveEntry> entries;
    private bool disposed;

    public IReadOnlyCollection<string> Paths { get; }

    public ZipContentStore(
        Stream zipStream,
        long maxEntryBytes = DefaultMaxEntryBytes,
        long maxTotalBytes = DefaultMaxTotalBytes,
        int maxEntries = DefaultMaxEntries)
    {
        ArgumentNullException.ThrowIfNull(zipStream);
        if (!zipStream.CanRead)
            throw new ArgumentException("The ZIP stream must be readable.", nameof(zipStream));
        if (maxEntryBytes <= 0 || maxTotalBytes <= 0 || maxEntries <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxEntryBytes), "ZIP safety limits must be positive.");

        archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: false);
        entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);

        long totalBytes = 0;
        try
        {
            if (archive.Entries.Count > maxEntries)
                throw new InvalidDataException($"The package contains more than {maxEntries} entries.");

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    continue; // Directory marker.

                string path;
                try
                {
                    path = VirtualPath.Normalize(entry.FullName);
                }
                catch (ArgumentException exception)
                {
                    throw new InvalidDataException($"Unsafe ZIP entry path: {entry.FullName}", exception);
                }

                if (entry.Length > maxEntryBytes)
                    throw new InvalidDataException($"ZIP entry exceeds the {maxEntryBytes}-byte limit: {path}");

                totalBytes = checked(totalBytes + entry.Length);
                if (totalBytes > maxTotalBytes)
                    throw new InvalidDataException($"The package exceeds the {maxTotalBytes}-byte uncompressed limit.");

                if (!entries.TryAdd(path, entry))
                    throw new InvalidDataException($"The package contains duplicate virtual paths: {path}");
            }

            Paths = Array.AsReadOnly(entries.Keys.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray());
        }
        catch
        {
            archive.Dispose();
            throw;
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
        if (!entries.TryGetValue(normalized, out var entry))
            throw new FileNotFoundException($"Virtual content file not found: {normalized}", normalized);

        return entry.Open();
    }

    public bool TryOpenRead(string path, out Stream? stream)
    {
        ThrowIfDisposed();
        var normalized = VirtualPath.Normalize(path);
        if (entries.TryGetValue(normalized, out var entry))
        {
            stream = entry.Open();
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
        archive.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}

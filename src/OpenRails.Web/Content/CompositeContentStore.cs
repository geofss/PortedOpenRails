namespace OpenRails.Web.Content;

/// <summary>
/// Combines read-only packages into one logical content namespace.
/// Packages are ordered from highest to lowest precedence.
/// </summary>
public sealed class CompositeContentStore : IVirtualContentStore, IDisposable
{
    private readonly IReadOnlyList<PackageLayer> layers;
    private readonly IReadOnlyCollection<string> paths;
    private bool disposed;

    public IReadOnlyCollection<string> Paths
    {
        get
        {
            ThrowIfDisposed();
            return paths;
        }
    }

    public CompositeContentStore(IEnumerable<PackageLayer> packages)
    {
        ArgumentNullException.ThrowIfNull(packages);

        var materialized = packages.ToArray();
        if (materialized.Length == 0)
            throw new ArgumentException("At least one package layer is required.", nameof(packages));

        if (materialized.Any(package => package is null))
            throw new ArgumentException("Package layers cannot contain null entries.", nameof(packages));

        var duplicateNames = materialized
            .GroupBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateNames is not null)
            throw new ArgumentException($"Package layer names must be unique: {duplicateNames.Key}", nameof(packages));

        layers = Array.AsReadOnly(materialized);
        var visiblePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in layers)
            visiblePaths.UnionWith(layer.Store.Paths);

        paths = Array.AsReadOnly(visiblePaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    /// <summary>Returns the first package containing this path, respecting precedence.</summary>
    public string? GetSourcePackage(string path)
    {
        ThrowIfDisposed();
        var normalized = VirtualPath.Normalize(path);
        return layers.FirstOrDefault(layer => layer.Store.Contains(normalized))?.Name;
    }

    public bool Contains(string path)
    {
        ThrowIfDisposed();
        var normalized = VirtualPath.Normalize(path);
        return layers.Any(layer => layer.Store.Contains(normalized));
    }

    public Stream OpenRead(string path)
    {
        ThrowIfDisposed();
        var normalized = VirtualPath.Normalize(path);

        foreach (var layer in layers)
        {
            if (layer.Store.TryOpenRead(normalized, out var stream))
                return stream!;
        }

        throw new FileNotFoundException($"Virtual content file not found in any package: {normalized}", normalized);
    }

    public bool TryOpenRead(string path, out Stream? stream)
    {
        ThrowIfDisposed();
        var normalized = VirtualPath.Normalize(path);

        foreach (var layer in layers)
        {
            if (layer.Store.TryOpenRead(normalized, out stream))
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
        foreach (var layer in layers)
            layer.Store.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}

/// <summary>
/// A named package layer. Earlier layers in CompositeContentStore win conflicts.
/// The composite store owns and disposes all supplied package stores.
/// </summary>
public sealed record PackageLayer(string Name, ZipContentStore Store)
{
    public string Name { get; } = string.IsNullOrWhiteSpace(Name)
        ? throw new ArgumentException("A package layer must have a name.", nameof(Name))
        : Name;

    public ZipContentStore Store { get; } = Store ?? throw new ArgumentNullException(nameof(Store));
}

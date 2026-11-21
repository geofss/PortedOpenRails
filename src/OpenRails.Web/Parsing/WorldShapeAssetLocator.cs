using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>Loads a discovered WORLD tile on demand to avoid retaining every route tile at once.</summary>
public sealed class WorldTileContentLoader
{
    public WorldTileData Load(IVirtualContentStore store, WorldTileReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return new WorldTileReader().Read(store, reference.Path);
    }
}

/// <summary>Resolves shape assets referenced by static and track objects in loaded WORLD tiles.</summary>
public sealed class WorldShapeAssetLocator
{
    public WorldShapeAssetDiscovery Locate(
        IVirtualContentStore store,
        string routeRoot,
        IEnumerable<WorldTileData> worldTiles)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(worldTiles);

        var root = VirtualPath.Normalize(routeRoot);
        var available = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in store.Paths)
            available.TryAdd(path, path);

        var found = new List<WorldShapeAssetReference>();
        var missing = new List<WorldShapeAssetIssue>();

        foreach (var tile in worldTiles)
        {
            foreach (var item in tile.StaticObjects.Select(value => (value.Uid, value.FileName, Kind: "Static"))
                .Concat(tile.TrackObjects.Select(value => (value.Uid, value.FileName, Kind: "TrackObj"))))
            {
                if (string.IsNullOrWhiteSpace(item.FileName))
                    continue;

                var fileName = item.FileName.Replace('\\', '/');
                if (fileName.StartsWith('/') || fileName.Contains(':') ||
                    fileName.Split('/').Any(segment => segment is "." or ".."))
                {
                    missing.Add(new WorldShapeAssetIssue(tile.TileX, tile.TileZ, item.Uid,
                        item.FileName, item.Kind, "Shape filename is not a safe virtual relative path."));
                    continue;
                }

                var candidates = new[]
                {
                    fileName.StartsWith("GLOBAL/", StringComparison.OrdinalIgnoreCase) ||
                    fileName.StartsWith("ROUTES/", StringComparison.OrdinalIgnoreCase)
                        ? fileName
                        : null,
                    $"{root}/SHAPES/{fileName}",
                    $"GLOBAL/SHAPES/{fileName}"
                };

                var resolved = candidates
                    .Where(candidate => candidate is not null)
                    .Select(candidate => available.TryGetValue(candidate!, out var actual) ? actual : null)
                    .FirstOrDefault(actual => actual is not null);

                if (resolved is null)
                    missing.Add(new WorldShapeAssetIssue(tile.TileX, tile.TileZ, item.Uid,
                        item.FileName, item.Kind, "Original world-object shape file was not found."));
                else
                    found.Add(new WorldShapeAssetReference(tile.TileX, tile.TileZ, item.Uid,
                        item.FileName, item.Kind, resolved));
            }
        }

        return new WorldShapeAssetDiscovery(found, missing);
    }
}

public sealed record WorldShapeAssetReference(
    int TileX,
    int TileZ,
    uint ObjectUid,
    string FileName,
    string ObjectKind,
    string Path);

public sealed record WorldShapeAssetIssue(
    int TileX,
    int TileZ,
    uint ObjectUid,
    string FileName,
    string ObjectKind,
    string Reason);

public sealed class WorldShapeAssetDiscovery
{
    public IReadOnlyList<WorldShapeAssetReference> Found { get; }
    public IReadOnlyList<WorldShapeAssetIssue> Missing { get; }

    public WorldShapeAssetDiscovery(IEnumerable<WorldShapeAssetReference> found,
        IEnumerable<WorldShapeAssetIssue> missing)
    {
        Found = Array.AsReadOnly(found.ToArray());
        Missing = Array.AsReadOnly(missing.ToArray());
    }
}

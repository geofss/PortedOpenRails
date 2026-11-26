using OpenRails.Web.Content;
using OpenRails.Web.Simulation;

namespace OpenRails.Web.Parsing;

/// <summary>Finds route WORLD tiles intersected by the built track network.</summary>
public sealed class RouteWorldTileLocator
{
    public RouteWorldTileDiscovery Locate(
        IVirtualContentStore store,
        string routeRoot,
        TrackNetwork network,
        double geometrySampleSpacingMeters = 512)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(network);

        var root = VirtualPath.Normalize(routeRoot);
        var needed = new HashSet<(int X, int Z)>();
        foreach (var node in network.Database.EnumerateNodes())
        {
            if (node.Location is { } location)
                needed.Add((location.TileX, location.TileZ));
        }

        var tessellator = new TrackGeometryTessellator();
        foreach (var path in network.VectorPaths)
        foreach (var point in tessellator.BuildPolyline(path, geometrySampleSpacingMeters))
        {
            var tileX = (int)Math.Floor((point.X + TrackNetworkBuilder.TileSizeMeters / 2) / TrackNetworkBuilder.TileSizeMeters);
            var tileZ = (int)Math.Floor((point.Z + TrackNetworkBuilder.TileSizeMeters / 2) / TrackNetworkBuilder.TileSizeMeters);
            needed.Add((tileX, tileZ));
        }

        var worldPrefix = root + "/WORLD/";
        var available = new Dictionary<(int X, int Z), string>();
        foreach (var path in store.Paths)
        {
            if (!path.StartsWith(worldPrefix, StringComparison.OrdinalIgnoreCase) ||
                !path.EndsWith(".w", StringComparison.OrdinalIgnoreCase) ||
                !WorldTileReader.TryParseTileCoordinates(path, out var x, out var z))
                continue;
            available.TryAdd((x, z), path);
        }

        // Load every WORLD tile shipped with this route, not only tiles crossed
        // by track centerlines. Towns, yards, depots and other scenery often sit
        // beside the track and would otherwise silently disappear from the route.
        var found = available
            .OrderBy(pair => pair.Key.Z)
            .ThenBy(pair => pair.Key.X)
            .Select(pair => new WorldTileReference(pair.Key.X, pair.Key.Z, pair.Value))
            .ToArray();
        var missing = needed
            .Where(tile => !available.ContainsKey(tile))
            .OrderBy(tile => tile.Z)
            .ThenBy(tile => tile.X)
            .ToArray();

        return new RouteWorldTileDiscovery(found, missing);
    }
}

public sealed record WorldTileReference(int TileX, int TileZ, string Path);

public sealed class RouteWorldTileDiscovery
{
    public IReadOnlyList<WorldTileReference> Found { get; }
    public IReadOnlyList<(int TileX, int TileZ)> Missing { get; }

    public RouteWorldTileDiscovery(IEnumerable<WorldTileReference> found,
        IEnumerable<(int TileX, int TileZ)> missing)
    {
        Found = Array.AsReadOnly(found.ToArray());
        Missing = Array.AsReadOnly(missing.ToArray());
    }
}

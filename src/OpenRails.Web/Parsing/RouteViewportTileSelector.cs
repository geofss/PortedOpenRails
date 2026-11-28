namespace OpenRails.Web.Parsing;

/// <summary>
/// Converts a camera world position into prioritized, bounded tile requests.
/// Tile coordinates follow the MSTS/Open Rails 2048-metre tile grid.
/// </summary>
public static class RouteViewportTileSelector
{
    public const double TileSizeMeters = 2048;

    public static IReadOnlyList<RouteTileStreamRequest<WorldTileReference>> SelectWorldTiles(
        IEnumerable<WorldTileReference> available,
        double cameraX,
        double cameraZ,
        double radiusMeters,
        long estimatedBytesPerTile = 8 * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(available);
        return Select(available, tile => tile.TileX, tile => tile.TileZ,
            cameraX, cameraZ, radiusMeters, estimatedBytesPerTile);
    }

    public static IReadOnlyList<RouteTileStreamRequest<TerrainTileReference>> SelectTerrainTiles(
        IEnumerable<TerrainTileReference> available,
        double cameraX,
        double cameraZ,
        double radiusMeters,
        long estimatedBytesPerTile = 4 * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(available);
        return Select(available, tile => tile.TileX, tile => tile.TileZ,
            cameraX, cameraZ, radiusMeters, estimatedBytesPerTile);
    }

    private static IReadOnlyList<RouteTileStreamRequest<TTile>> Select<TTile>(
        IEnumerable<TTile> available,
        Func<TTile, int> tileX,
        Func<TTile, int> tileZ,
        double cameraX,
        double cameraZ,
        double radiusMeters,
        long estimatedBytesPerTile)
        where TTile : notnull
    {
        if (!double.IsFinite(cameraX) || !double.IsFinite(cameraZ))
            throw new ArgumentOutOfRangeException(nameof(cameraX), "Camera coordinates must be finite.");
        if (!double.IsFinite(radiusMeters) || radiusMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(radiusMeters));
        if (estimatedBytesPerTile < 0)
            throw new ArgumentOutOfRangeException(nameof(estimatedBytesPerTile));

        var radiusSquared = radiusMeters * radiusMeters;
        var requests = new List<RouteTileStreamRequest<TTile>>();
        var seen = new HashSet<(int X, int Z)>();

        foreach (var tile in available)
        {
            var x = tileX(tile);
            var z = tileZ(tile);
            if (!seen.Add((x, z)))
                continue;

            var dx = x * TileSizeMeters - cameraX;
            var dz = z * TileSizeMeters - cameraZ;
            var distanceSquared = dx * dx + dz * dz;
            if (distanceSquared > radiusSquared)
                continue;

            // Higher priority means nearer to the camera. The cache sorts descending.
            var priority = -Math.Sqrt(distanceSquared);
            requests.Add(new RouteTileStreamRequest<TTile>(tile, priority, estimatedBytesPerTile));
        }

        return Array.AsReadOnly(requests
            .OrderByDescending(request => request.Priority)
            .ToArray());
    }
}

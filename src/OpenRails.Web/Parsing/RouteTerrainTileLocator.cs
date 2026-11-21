// COPYRIGHT 2009-2013 by the Open Rails project.
// This file is part of Open Rails and is licensed under GPL-3.0-or-later.
// Tile naming is ported from Open Rails TerrainFile.TileName.

using OpenRails.Web.Content;
using OpenRails.Web.Simulation;

namespace OpenRails.Web.Parsing;

public enum TerrainTileZoom
{
    DMLarge = 11,
    DMSmall = 12,
    Large = 14,
    Small = 15
}

public static class TerrainTileName
{
    private const string Hex = "0123456789ABCDEF";

    public static string FromTileXZ(int tileX, int tileZ, TerrainTileZoom zoom)
    {
        var level = (int)zoom;
        if (level is < 1 or > 15)
            throw new ArgumentOutOfRangeException(nameof(zoom));

        var rectX = -16384;
        var rectZ = -16384;
        var rectWidth = 16384;
        var rectHeight = 16384;
        var name = new System.Text.StringBuilder(level % 2 == 1 ? "-" : "_");
        var partial = 0;

        for (var depth = 0; depth < level; depth++)
        {
            var east = tileX >= rectX + rectWidth;
            var north = tileZ >= rectZ + rectHeight;
            partial <<= 2;
            partial += (north ? 0 : 2) + (east ^ north ? 0 : 1);

            if (depth % 2 == 1)
            {
                name.Append(Hex[partial]);
                partial = 0;
            }

            if (east) rectX += rectWidth;
            if (north) rectZ += rectHeight;
            rectWidth /= 2;
            rectHeight /= 2;
        }

        if (level % 2 == 1)
            name.Append(Hex[partial << 2]);

        return name.ToString();
    }

    public static void Snap(ref int tileX, ref int tileZ, TerrainTileZoom zoom)
    {
        var shift = 15 - (int)zoom;
        tileX >>= shift;
        tileX <<= shift;
        tileZ >>= shift;
        tileZ <<= shift;
    }
}

/// <summary>
/// Finds the terrain tiles touched by the built track network. The tile coordinate
/// conversion and filename encoding match the upstream MSTS/Open Rails convention.
/// </summary>
public sealed class RouteTerrainTileLocator
{
    public RouteTerrainTileDiscovery Locate(
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
        {
            foreach (var point in tessellator.BuildPolyline(path, geometrySampleSpacingMeters))
            {
                var tileX = (int)Math.Floor((point.X + TrackNetworkBuilder.TileSizeMeters / 2) / TrackNetworkBuilder.TileSizeMeters);
                var tileZ = (int)Math.Floor((point.Z + TrackNetworkBuilder.TileSizeMeters / 2) / TrackNetworkBuilder.TileSizeMeters);
                needed.Add((tileX, tileZ));
            }
        }

        var availablePaths = store.Paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var found = new List<TerrainTileReference>();
        var missing = new List<(int X, int Z, string ExpectedPath)>();

        foreach (var tile in needed.OrderBy(tile => tile.Z).ThenBy(tile => tile.X))
        {
            var fileName = TerrainTileName.FromTileXZ(tile.X, tile.Z, TerrainTileZoom.Small) + ".t";
            var expectedPath = $"{root}/TILES/{fileName}";
            var actualPath = availablePaths.FirstOrDefault(path =>
                path.Equals(expectedPath, StringComparison.OrdinalIgnoreCase));

            if (actualPath is null)
                missing.Add((tile.X, tile.Z, expectedPath));
            else
                found.Add(new TerrainTileReference(tile.X, tile.Z, actualPath));
        }

        return new RouteTerrainTileDiscovery(found, missing);
    }
}

public sealed record TerrainTileReference(int TileX, int TileZ, string Path);

public sealed class RouteTerrainTileDiscovery
{
    public IReadOnlyList<TerrainTileReference> Found { get; }
    public IReadOnlyList<(int TileX, int TileZ, string ExpectedPath)> Missing { get; }

    public RouteTerrainTileDiscovery(IEnumerable<TerrainTileReference> found,
        IEnumerable<(int TileX, int TileZ, string ExpectedPath)> missing)
    {
        Found = Array.AsReadOnly(found.ToArray());
        Missing = Array.AsReadOnly(missing.ToArray());
    }
}

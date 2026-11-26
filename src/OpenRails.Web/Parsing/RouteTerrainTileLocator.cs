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
    public static string FromTileXZ(int tileX, int tileZ, TerrainTileZoom zoom)
    {
        var level = (int)zoom;
        if (level is < 1 or > 15)
            throw new ArgumentOutOfRangeException(nameof(zoom));

        return Orts.Formats.Msts.TileName.FromTileXZ(
            tileX,
            tileZ,
            (Orts.Formats.Msts.TileName.Zoom)level);
    }

    public static void Snap(ref int tileX, ref int tileZ, TerrainTileZoom zoom)
    {
        var level = (int)zoom;
        if (level is < 1 or > 15)
            throw new ArgumentOutOfRangeException(nameof(zoom));

        Orts.Formats.Msts.TileName.Snap(
            ref tileX,
            ref tileZ,
            (Orts.Formats.Msts.TileName.Zoom)level);
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

        var availablePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in store.Paths)
            availablePaths.TryAdd(path, path);
        var foundByTile = new Dictionary<(TerrainTileZoom Zoom, int X, int Z), TerrainTileReference>();
        var missing = new List<(int X, int Z, string ExpectedPath)>();
        var supportedZooms = new[]
        {
            TerrainTileZoom.Small,
            TerrainTileZoom.Large,
            TerrainTileZoom.DMSmall,
            TerrainTileZoom.DMLarge
        };

        foreach (var tile in needed.OrderBy(tile => tile.Z).ThenBy(tile => tile.X))
        {
            var located = false;
            string? firstMissingPath = null;
            foreach (var zoom in supportedZooms)
            {
                var snappedX = tile.X;
                var snappedZ = tile.Z;
                TerrainTileName.Snap(ref snappedX, ref snappedZ, zoom);
                var key = (zoom, snappedX, snappedZ);
                if (foundByTile.ContainsKey(key))
                {
                    located = true;
                    break;
                }

                var tileName = TerrainTileName.FromTileXZ(snappedX, snappedZ, zoom);
                var expectedPath = $"{root}/TILES/{tileName}.t";
                var expectedAltitudePath = $"{root}/TILES/{tileName}_y.raw";
                if (!availablePaths.TryGetValue(expectedPath, out var actualPath))
                {
                    firstMissingPath ??= expectedPath;
                    continue;
                }

                if (!availablePaths.TryGetValue(expectedAltitudePath, out var actualAltitudePath))
                {
                    firstMissingPath ??= expectedAltitudePath;
                    continue;
                }

                var expectedFlagsPath = $"{root}/TILES/{tileName}_f.raw";
                availablePaths.TryGetValue(expectedFlagsPath, out var actualFlagsPath);
                foundByTile.Add(key, new TerrainTileReference(
                    snappedX, snappedZ, actualPath, actualAltitudePath, actualFlagsPath));
                located = true;
                break;
            }

            if (!located)
            {
                var fallbackName = TerrainTileName.FromTileXZ(tile.X, tile.Z, TerrainTileZoom.Small);
                missing.Add((tile.X, tile.Z, firstMissingPath ?? $"{root}/TILES/{fallbackName}.t"));
            }
        }

        var found = foundByTile.Values
            .OrderBy(tile => tile.TileZ)
            .ThenBy(tile => tile.TileX)
            .ToArray();
        return new RouteTerrainTileDiscovery(found, missing);
    }
}

public sealed record TerrainTileReference(int TileX, int TileZ, string Path, string AltitudePath, string? FlagsPath);

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

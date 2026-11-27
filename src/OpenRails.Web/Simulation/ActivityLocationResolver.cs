using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

/// <summary>
/// Converts the route profile's world-space coordinates into the tile-relative
/// coordinates used by MSTS activity location events.
/// </summary>
public static class ActivityLocationResolver
{
    public const double TileSizeMeters = TrackNetworkBuilder.TileSizeMeters;
    private const double TileHalfSizeMeters = TileSizeMeters / 2;

    public static ActivityTilePosition Resolve(TrackWorldPosition position)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (!double.IsFinite(position.X) || !double.IsFinite(position.Z))
            throw new ArgumentOutOfRangeException(nameof(position), "Activity position coordinates must be finite.");

        var tileX = checked((int)Math.Floor((position.X + TileHalfSizeMeters) / TileSizeMeters));
        var tileZ = checked((int)Math.Floor((position.Z + TileHalfSizeMeters) / TileSizeMeters));
        return new ActivityTilePosition(tileX, tileZ,
            position.X - tileX * TileSizeMeters,
            position.Z - tileZ * TileSizeMeters);
    }
}

public sealed record ActivityTilePosition(int TileX, int TileZ, double X, double Z);

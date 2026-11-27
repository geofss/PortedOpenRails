using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

/// <summary>Maps the first native service-path point onto the selected connected TDB route.</summary>
public static class ServiceRoutePositionResolver
{
    public static ServiceRouteStartPosition Resolve(
        RoutePathTrace trace,
        TrackNetworkRouteProfile profile,
        double maximumOffsetMeters = 30)
    {
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(profile);
        if (!double.IsFinite(maximumOffsetMeters) || maximumOffsetMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumOffsetMeters));
        if (!trace.IsComplete)
            throw new InvalidDataException("Service path contains broken node links and cannot be positioned on the selected route.");

        var point = trace.Points.FirstOrDefault(candidate => !candidate.IsInvalid);
        if (point is null)
            throw new InvalidDataException("Service path has no valid positioned starting point.");

        var worldPosition = new TrackWorldPosition(
            point.TileX * ActivityLocationResolver.TileSizeMeters + point.X,
            point.Y,
            point.TileZ * ActivityLocationResolver.TileSizeMeters + point.Z);
        if (!profile.TryFindNearestDistance(worldPosition, maximumOffsetMeters,
                out var distance, out var offset))
            throw new InvalidDataException(
                $"Service path start is {offset:N1} m from the selected connected route; maximum allowed offset is {maximumOffsetMeters:N1} m.");

        return new ServiceRouteStartPosition(distance, offset, worldPosition);
    }
}

public sealed record ServiceRouteStartPosition(
    double DistanceMeters,
    double OffsetMeters,
    TrackWorldPosition WorldPosition);

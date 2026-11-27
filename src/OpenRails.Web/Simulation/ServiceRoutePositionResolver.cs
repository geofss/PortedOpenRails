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

        var validPoints = trace.Points.Where(candidate => !candidate.IsInvalid).ToArray();
        if (validPoints.Length == 0)
            throw new InvalidDataException("Service path has no valid positioned starting point.");

        double startDistance = 0;
        double startOffset = 0;
        TrackWorldPosition? startPosition = null;
        for (var index = 0; index < validPoints.Length; index++)
        {
            var point = validPoints[index];
            var worldPosition = new TrackWorldPosition(
                point.TileX * ActivityLocationResolver.TileSizeMeters + point.X,
                point.Y,
                point.TileZ * ActivityLocationResolver.TileSizeMeters + point.Z);
            if (!profile.TryFindNearestDistance(worldPosition, maximumOffsetMeters,
                    out var distance, out var offset))
                throw new InvalidDataException(
                    $"Service path point {index} is {offset:N1} m from the selected connected route; maximum allowed offset is {maximumOffsetMeters:N1} m.");

            if (index == 0)
            {
                startDistance = distance;
                startOffset = offset;
                startPosition = worldPosition;
            }
        }

        return new ServiceRouteStartPosition(startDistance, startOffset, startPosition!);
    }
}

public sealed record ServiceRouteStartPosition(
    double DistanceMeters,
    double OffsetMeters,
    TrackWorldPosition WorldPosition);

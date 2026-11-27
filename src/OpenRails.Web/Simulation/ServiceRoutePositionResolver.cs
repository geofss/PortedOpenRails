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

        var routeDistances = new double[validPoints.Length];
        double startOffset = 0;
        TrackWorldPosition? startPosition = null;
        var direction = 0;
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

            routeDistances[index] = distance;
            if (index == 0)
            {
                startOffset = offset;
                startPosition = worldPosition;
                continue;
            }

            var delta = distance - routeDistances[index - 1];
            if (Math.Abs(delta) <= 0.5)
                continue;
            var step = Math.Sign(delta);
            if (direction == 0)
                direction = step;
            else if (step != direction)
                throw new InvalidDataException(
                    $"Service path reverses direction at waypoint {index}; this route profile cannot represent a reversal without a native reversal-point transition.");
        }

        return new ServiceRouteStartPosition(routeDistances[0], startOffset, startPosition!,
            Array.AsReadOnly(routeDistances), direction == 0 ? 1 : direction);
    }
}

public sealed record ServiceRouteStartPosition(
    double DistanceMeters,
    double OffsetMeters,
    TrackWorldPosition WorldPosition,
    IReadOnlyList<double>? RouteWaypointDistancesMeters = null,
    int Direction = 1);

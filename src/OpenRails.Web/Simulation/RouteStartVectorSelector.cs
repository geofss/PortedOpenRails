namespace OpenRails.Web.Simulation;

/// <summary>
/// Ranks connected TDB vector paths by proximity to the route's declared MSTS
/// RouteStart coordinate. Both vector endpoints are considered because the
/// route start may point at either end of a stored vector.
/// </summary>
public sealed class RouteStartVectorSelector
{
    public IReadOnlyList<TrackVectorPath> SelectNearest(
        TrackNetwork network,
        OpenRails.Web.Parsing.RouteStartCoordinates routeStart,
        int maximumResults = 100)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(routeStart);
        if (maximumResults <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumResults));
        ValidateRouteStart(routeStart);

        var targetX = routeStart.TileX * TrackNetworkBuilder.TileSizeMeters + routeStart.X;
        var targetZ = routeStart.TileZ * TrackNetworkBuilder.TileSizeMeters + routeStart.Z;

        return Array.AsReadOnly(network.VectorPaths
            .Where(path => path.ConnectedNodeIndices.Count == 2)
            .OrderBy(path => DistanceToRouteStart(path, targetX, targetZ))
            .ThenByDescending(path => path.LengthMeters)
            .Take(maximumResults)
            .ToArray());
    }

    public bool IsStartReversed(TrackVectorPath path, OpenRails.Web.Parsing.RouteStartCoordinates routeStart)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(routeStart);
        ValidateRouteStart(routeStart);
        var targetX = routeStart.TileX * TrackNetworkBuilder.TileSizeMeters + routeStart.X;
        var targetZ = routeStart.TileZ * TrackNetworkBuilder.TileSizeMeters + routeStart.Z;
        var first = path.Sections.FirstOrDefault()?.Start;
        var end = path.EndPosition;
        if (first is null || end is null)
            return false;

        return HorizontalDistance(end.X, end.Z, targetX, targetZ) <
            HorizontalDistance(first.X, first.Z, targetX, targetZ);
    }

    private static void ValidateRouteStart(OpenRails.Web.Parsing.RouteStartCoordinates routeStart)
    {
        var targetX = routeStart.TileX * TrackNetworkBuilder.TileSizeMeters + routeStart.X;
        var targetZ = routeStart.TileZ * TrackNetworkBuilder.TileSizeMeters + routeStart.Z;
        if (!double.IsFinite(routeStart.TileX) || !double.IsFinite(routeStart.TileZ) ||
            !double.IsFinite(routeStart.X) || !double.IsFinite(routeStart.Z) ||
            !double.IsFinite(targetX) || !double.IsFinite(targetZ))
            throw new ArgumentOutOfRangeException(nameof(routeStart),
                "RouteStart coordinates must be finite and representable in world space.");
    }

    private static double DistanceToRouteStart(TrackVectorPath path, double targetX, double targetZ)
    {
        var first = path.Sections.FirstOrDefault()?.Start;
        var end = path.EndPosition;
        var firstDistance = first is null
            ? double.PositiveInfinity
            : HorizontalDistance(first.X, first.Z, targetX, targetZ);
        var endDistance = end is null
            ? double.PositiveInfinity
            : HorizontalDistance(end.X, end.Z, targetX, targetZ);
        return Math.Min(firstDistance, endDistance);
    }

    private static double HorizontalDistance(double x1, double z1, double x2, double z2)
    {
        var dx = x1 - x2;
        var dz = z1 - z2;
        return Math.Sqrt(dx * dx + dz * dz);
    }
}

using OpenRails.Web.Simulation;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Resolves TDB track-item references onto the connected vector geometry so route
/// features can be rendered and consumed by browser simulation systems.
/// </summary>
public sealed class RouteTrackItemPlacementBuilder
{
    public RouteTrackItemPlacementResult Build(
        TrackNetwork network,
        double maximumSegmentLengthMeters = 2)
    {
        ArgumentNullException.ThrowIfNull(network);
        if (!double.IsFinite(maximumSegmentLengthMeters) || maximumSegmentLengthMeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumSegmentLengthMeters));

        var placements = new List<RouteTrackItemPlacement>();
        var diagnostics = new List<string>();
        var mappedItems = new HashSet<int>();
        var tessellator = new TrackGeometryTessellator();

        foreach (var path in network.VectorPaths)
        {
            if (!network.Database.TryGetNode((int)path.TrackNodeIndex, out var node) || node is null)
            {
                diagnostics.Add($"Vector path {path.TrackNodeIndex} has no source TrackNode.");
                continue;
            }

            var points = tessellator.BuildPolyline(path, maximumSegmentLengthMeters);
            if (points.Count < 2)
            {
                diagnostics.Add($"Vector path {path.TrackNodeIndex} has insufficient geometry for track-item placement.");
                continue;
            }

            var cumulative = BuildCumulativeDistances(points);
            var sourceLength = path.LengthMeters;
            foreach (var itemIndex in node.TrackItemReferences.Distinct())
            {
                if (itemIndex < 0 || itemIndex >= network.Database.TrackItems.Count ||
                    network.Database.TrackItems[itemIndex] is not { } item)
                {
                    diagnostics.Add($"Vector path {path.TrackNodeIndex} references missing track item {itemIndex}.");
                    continue;
                }

                mappedItems.Add(itemIndex);
                if (!float.IsFinite(item.DistanceAlongTrack) || item.DistanceAlongTrack < 0)
                {
                    diagnostics.Add($"Track item {itemIndex} has invalid distance-along-track metadata.");
                    continue;
                }

                var localDistance = Math.Clamp(item.DistanceAlongTrack, 0, sourceLength);
                if (item.DistanceAlongTrack > sourceLength + 0.5)
                    diagnostics.Add($"Track item {itemIndex} lies beyond vector path {path.TrackNodeIndex}; its position was clamped.");

                var polylineDistance = sourceLength > 0
                    ? localDistance / sourceLength * cumulative[^1]
                    : 0;
                var (position, heading) = Sample(points, cumulative, polylineDistance);
                placements.Add(new RouteTrackItemPlacement(
                    item,
                    path.TrackNodeIndex,
                    position,
                    heading,
                    localDistance,
                    item.LinkedItemId is { } linked && linked < network.Database.TrackItems.Count
                        ? (int)linked : null));
            }
        }

        foreach (var item in network.Database.TrackItems)
            if (item is not null && !mappedItems.Contains(item.Index) && item.Type != "empty")
                diagnostics.Add($"Track item {item.Index} ({item.Type}) is not hosted by a vector TrackNode.");

        var byIndex = placements.GroupBy(item => item.Item.Index)
            .ToDictionary(group => group.Key, group => group.First());
        var resolved = placements.Select(item => item with
        {
            LinkedItemResolved = item.LinkedItemIndex is { } linked && byIndex.ContainsKey(linked)
        }).ToArray();

        return new RouteTrackItemPlacementResult(
            Array.AsReadOnly(resolved),
            Array.AsReadOnly(diagnostics.Distinct(StringComparer.Ordinal).ToArray()));
    }

    private static double[] BuildCumulativeDistances(IReadOnlyList<TrackWorldPosition> points)
    {
        var distances = new double[points.Count];
        for (var i = 1; i < points.Count; i++)
        {
            var dx = points[i].X - points[i - 1].X;
            var dy = points[i].Y - points[i - 1].Y;
            var dz = points[i].Z - points[i - 1].Z;
            distances[i] = distances[i - 1] + Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
        return distances;
    }

    private static (TrackWorldPosition Position, double Heading) Sample(
        IReadOnlyList<TrackWorldPosition> points,
        IReadOnlyList<double> cumulative,
        double distance)
    {
        if (distance <= 0)
            return (points[0], Heading(points[0], points[1]));
        if (distance >= cumulative[^1])
            return (points[^1], Heading(points[^2], points[^1]));

        var upper = 1;
        while (upper < cumulative.Count && cumulative[upper] < distance)
            upper++;
        var lower = upper - 1;
        var span = cumulative[upper] - cumulative[lower];
        var amount = span <= 0 ? 0 : (distance - cumulative[lower]) / span;
        var a = points[lower];
        var b = points[upper];
        var position = new TrackWorldPosition(
            a.X + (b.X - a.X) * amount,
            a.Y + (b.Y - a.Y) * amount,
            a.Z + (b.Z - a.Z) * amount);
        return (position, Heading(a, b));
    }

    private static double Heading(TrackWorldPosition from, TrackWorldPosition to) =>
        Math.Atan2(to.X - from.X, to.Z - from.Z);
}

public sealed record RouteTrackItemPlacement(
    TrackItem Item,
    uint TrackNodeIndex,
    TrackWorldPosition Position,
    double HeadingRadians,
    double DistanceAlongTrackMeters,
    int? LinkedItemIndex,
    bool LinkedItemResolved = false);

public sealed record RouteTrackItemPlacementResult(
    IReadOnlyList<RouteTrackItemPlacement> Placements,
    IReadOnlyList<string> Diagnostics);

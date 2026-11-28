using System.Collections.Frozen;

namespace OpenRails.Web.Simulation;

/// <summary>Longitudinal occupied interval for one vehicle body, measured from the selected route origin.</summary>
public sealed record TrainCarRouteOccupancy(double CenterDistanceMeters, double LengthMeters);

/// <summary>
/// Resolves every vehicle body onto the original TDB vector-node sequence used by
/// the selected connected route. A circuit is occupied whenever any part of a
/// vehicle interval overlaps its vector segment; this includes trailing cars.
/// </summary>
public static class SignalOccupancyResolver
{
    public static IReadOnlySet<uint> Resolve(
        TrackNetworkRoute route,
        IReadOnlyList<TrainCarRouteOccupancy> cars)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(cars);
        if (route.Segments.Count == 0)
            return Array.Empty<uint>().ToFrozenSet();
        if (cars.Any(car => car is null ||
            !double.IsFinite(car.CenterDistanceMeters) ||
            !double.IsFinite(car.LengthMeters) || car.LengthMeters <= 0))
            throw new ArgumentOutOfRangeException(nameof(cars),
                "Every vehicle must have a finite route position and positive finite length.");

        var occupied = new HashSet<uint>();
        var segmentStart = 0d;
        foreach (var segment in route.Segments)
        {
            var segmentEnd = segmentStart + segment.LengthMeters;
            foreach (var car in cars)
            {
                var halfLength = car.LengthMeters / 2d;
                var carStart = Math.Max(0, car.CenterDistanceMeters - halfLength);
                var carEnd = Math.Min(route.LengthMeters, car.CenterDistanceMeters + halfLength);
                if (carEnd >= segmentStart && carStart <= segmentEnd)
                    occupied.Add(segment.Path.TrackNodeIndex);
            }
            segmentStart = segmentEnd;
        }

        return occupied.ToFrozenSet();
    }
}

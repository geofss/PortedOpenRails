using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

/// <summary>Result of aligning native timetable stop rows with .pat waiting-point waypoints.</summary>
public sealed record NativeTimetableWaitingPointResolution(
    IReadOnlyDictionary<int, double> ReleaseTimesSeconds,
    IReadOnlyList<string> Diagnostics);

/// <summary>
/// Aligns timetable station timepoints to native .pat waiting-point nodes in route
/// order. Times are converted from time-of-day to seconds since service departure,
/// including midnight rollover, for the shared AI simulation clock.
/// </summary>
public sealed class NativeTimetableWaitingPointResolver
{
    public NativeTimetableWaitingPointResolution Resolve(
        NativeTimetableTrainDefinition train,
        ServiceRouteStartPosition route,
        double serviceDepartureSeconds)
    {
        ArgumentNullException.ThrowIfNull(train);
        ArgumentNullException.ThrowIfNull(route);
        if (!double.IsFinite(serviceDepartureSeconds) || serviceDepartureSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(serviceDepartureSeconds));

        var diagnostics = new List<string>();
        var waitingIndices = route.RouteWaypoints is { Count: > 0 } waypoints
            ? waypoints.Select((waypoint, index) => (waypoint, index))
                .Where(item => item.waypoint.IsWaitingPoint)
                .Select(item => item.index).ToArray()
            : Array.Empty<int>();
        var stops = new List<(NativeTimetableStopDefinition Stop, double TimeSeconds)>();
        foreach (var stop in train.Stops)
        {
            if (stop.TryGetTimeSeconds(out var timeSeconds))
                stops.Add((stop, timeSeconds));
            else
                diagnostics.Add($"Timetable stop '{stop.Location}' has an unsupported time value '{stop.Value}'.");
        }

        if (waitingIndices.Length != stops.Count)
            diagnostics.Add($"Timetable service '{train.Train}' has {stops.Count} timed stops but its .pat path has {waitingIndices.Length} waiting points; only matching entries are bound.");

        var releases = new Dictionary<int, double>();
        var count = Math.Min(waitingIndices.Length, stops.Count);
        for (var index = 0; index < count; index++)
        {
            var releaseSeconds = stops[index].TimeSeconds - serviceDepartureSeconds;
            if (releaseSeconds < 0)
                releaseSeconds += 24 * 60 * 60;
            releases[waitingIndices[index]] = releaseSeconds;
        }

        return new NativeTimetableWaitingPointResolution(
            new System.Collections.ObjectModel.ReadOnlyDictionary<int, double>(releases),
            Array.AsReadOnly(diagnostics.ToArray()));
    }
}

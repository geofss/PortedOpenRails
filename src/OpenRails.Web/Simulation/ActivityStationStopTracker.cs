using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

public sealed record ActivityStationStopProgress(int TotalStops, int VisitedStops,
    bool StationStopReached, bool AllStopsCompleted, bool PathTraceComplete);

public sealed class ActivityStationStopTracker
{
    public const double DefaultStopRadiusMeters = 40;
    public const double StoppedSpeedMetersPerSecond = 0.1;
    private readonly (int NodeIndex, int TileX, int TileZ, double X, double Z)[] stops;
    private readonly HashSet<int> visited = [];
    private readonly bool pathTraceComplete;

    public int TotalStops => stops.Length;
    public int VisitedStops => visited.Count;
    public bool AllStopsCompleted => pathTraceComplete && stops.Length > 0 && visited.Count == stops.Length;
    public bool PathTraceComplete => pathTraceComplete;
    public double StopRadiusMeters { get; }

    public ActivityStationStopTracker(RoutePathDefinition path, double stopRadiusMeters = DefaultStopRadiusMeters)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!double.IsFinite(stopRadiusMeters) || stopRadiusMeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(stopRadiusMeters));
        StopRadiusMeters = stopRadiusMeters;
        var trace = path.TraceMainRoute();
        pathTraceComplete = trace.IsComplete;
        stops = trace.NodeIndices
            .Where(index => path.Nodes[index].IsWaitingPoint && path.Nodes[index].FromPdpIndex < path.TrackPoints.Count)
            .Select(index =>
            {
                var point = path.TrackPoints[(int)path.Nodes[index].FromPdpIndex];
                return (index, point.TileX, point.TileZ, (double)point.X, (double)point.Z);
            }).ToArray();
    }

    public ActivityStationStopProgress Advance(int tileX, int tileZ, double x, double z, double speedMetersPerSecond)
    {
        if (!double.IsFinite(x) || !double.IsFinite(z) || !double.IsFinite(speedMetersPerSecond))
            throw new ArgumentOutOfRangeException(nameof(x), "Activity station position and speed must be finite.");
        var reached = false;
        if (Math.Abs(speedMetersPerSecond) <= StoppedSpeedMetersPerSecond)
        {
            // AllStops is a path-order condition, not a set of stations visited
            // in any order. A train cannot satisfy a skipped earlier stop by
            // stopping at a later waiting point first.
            var nextStopIndex = Array.FindIndex(stops, stop => !visited.Contains(stop.NodeIndex));
            if (nextStopIndex >= 0)
            {
                var nextStop = stops[nextStopIndex];
                var dx = x - nextStop.X;
                var dz = z - nextStop.Z;
                if (nextStop.TileX != tileX || nextStop.TileZ != tileZ)
                    return new ActivityStationStopProgress(TotalStops, VisitedStops, false, AllStopsCompleted, PathTraceComplete);
                if (HorizontalDistance(dx, dz) <= StopRadiusMeters)
                {
                    visited.Add(nextStop.NodeIndex);
                    reached = true;
                }
            }
        }
        return new ActivityStationStopProgress(TotalStops, VisitedStops, reached, AllStopsCompleted, PathTraceComplete);
    }

    private static double HorizontalDistance(double x, double z)
    {
        var absoluteX = Math.Abs(x);
        var absoluteZ = Math.Abs(z);
        var maximum = Math.Max(absoluteX, absoluteZ);
        if (maximum == 0)
            return 0;
        if (!double.IsFinite(maximum))
            return double.PositiveInfinity;
        var minimum = Math.Min(absoluteX, absoluteZ);
        var ratio = minimum / maximum;
        return maximum * Math.Sqrt(1 + ratio * ratio);
    }
}

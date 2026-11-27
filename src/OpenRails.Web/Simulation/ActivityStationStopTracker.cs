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
            foreach (var stop in stops)
            {
                if (visited.Contains(stop.NodeIndex) || stop.TileX != tileX || stop.TileZ != tileZ) continue;
                var dx = x - stop.X;
                var dz = z - stop.Z;
                if (dx * dx + dz * dz <= StopRadiusMeters * StopRadiusMeters)
                {
                    visited.Add(stop.NodeIndex);
                    reached = true;
                }
            }
        }
        return new ActivityStationStopProgress(TotalStops, VisitedStops, reached, AllStopsCompleted, PathTraceComplete);
    }
}

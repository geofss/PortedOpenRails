namespace OpenRails.Web.Simulation;

/// <summary>
/// One dispatched timetable service connected to its real consist controller and
/// route position. RouteId groups trains that share a physical path for following.
/// </summary>
public sealed record AiTimetableTrainRuntimeBinding(
    string ServiceId,
    string RouteId,
    AiServiceInterlockingSession Session,
    BrowserTrainDrivingController Driver,
    ServiceRouteStartPosition Route,
    double TargetSpeedMetersPerSecond,
    Func<BrowserTrainDrivingController, IReadOnlyList<TrainCarRouteOccupancy>>? OccupancyResolver = null);

public sealed record AiTimetableSimulationTickResult(
    double ElapsedSeconds,
    IReadOnlyList<string> DispatchedServiceIds,
    IReadOnlyList<string> DecisionServiceIds,
    IReadOnlyList<AiServiceControlDecision> Decisions,
    IReadOnlyList<string> CompletedServiceIds,
    IReadOnlyList<string> OccupancyConflicts);

/// <summary>
/// Advances the native timetable dispatcher and all bound AI consist controllers
/// on one simulation clock, applying train-following constraints between services
/// that share a route and updating interlocking occupancy from consist positions.
/// </summary>
public sealed class AiTimetableSimulationRuntime
{
    private readonly AiTimetableDispatcher dispatcher;
    private readonly IReadOnlyList<AiTimetableTrainRuntimeBinding> trains;

    public AiTimetableSimulationRuntime(
        AiTimetableDispatcher dispatcher,
        IEnumerable<AiTimetableTrainRuntimeBinding> trains)
    {
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        ArgumentNullException.ThrowIfNull(trains);
        this.trains = Array.AsReadOnly(trains.ToArray());
        if (this.trains.Select(train => train.ServiceId)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != this.trains.Count)
            throw new ArgumentException("Timetable runtime service IDs must be unique.", nameof(trains));
        foreach (var train in this.trains)
        {
            if (string.IsNullOrWhiteSpace(train.ServiceId) || string.IsNullOrWhiteSpace(train.RouteId))
                throw new ArgumentException("Timetable runtime services require service and route identities.", nameof(trains));
            if (!double.IsFinite(train.TargetSpeedMetersPerSecond) || train.TargetSpeedMetersPerSecond < 0)
                throw new ArgumentOutOfRangeException(nameof(trains), "AI target speeds must be finite and non-negative.");
            ArgumentNullException.ThrowIfNull(train.Session);
            ArgumentNullException.ThrowIfNull(train.Driver);
            ArgumentNullException.ThrowIfNull(train.Route);
            if (train.Driver.State.Cars.Count == 0)
                throw new ArgumentException($"Timetable service '{train.ServiceId}' has no consist vehicles.", nameof(trains));
        }
    }

    public AiTimetableSimulationTickResult Advance(double deltaSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        var dispatched = dispatcher.Advance(deltaSeconds).ToArray();
        var active = trains.Where(train => train.Session.IsDispatched)
            .GroupBy(train => train.RouteId, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.OrderByDescending(train =>
                train.Route.Direction * train.Driver.State.Cars[0].DistanceMeters))
            .ToArray();
        var decisions = new List<AiServiceControlDecision>(active.Length);
        var decisionServiceIds = new List<string>(active.Length);
        var completed = new List<string>();
        var occupancyConflicts = new List<string>();

        foreach (var train in active)
        {
            var otherTrains = trains
                .Where(other => other.Session.IsDispatched &&
                    !ReferenceEquals(other, train) &&
                    other.RouteId.Equals(train.RouteId, StringComparison.OrdinalIgnoreCase))
                .Select(other => new AiServiceTrainState(
                    other.ServiceId,
                    other.Driver.State.Cars[0].DistanceMeters,
                    Math.Max(0, other.Driver.State.Cars[0].SpeedMetersPerSecond)))
                .ToArray();

            var decision = train.Session.Drive(
                train.Driver, train.Route, train.TargetSpeedMetersPerSecond,
                otherTrains: otherTrains);
            decisions.Add(decision);
            decisionServiceIds.Add(train.ServiceId);
            train.Driver.Advance(deltaSeconds);

            if (train.OccupancyResolver is not null &&
                !train.Session.UpdateConsistOccupancy(train.Route,
                    train.OccupancyResolver(train.Driver)))
                occupancyConflicts.Add(train.ServiceId);

            if (HasReachedRouteEnd(train) && train.Session.TryComplete())
                completed.Add(train.ServiceId);
        }

        dispatcher.Advance(0);
        return new AiTimetableSimulationTickResult(
            dispatcher.ElapsedSeconds,
            Array.AsReadOnly(dispatched),
            Array.AsReadOnly(decisionServiceIds.ToArray()),
            Array.AsReadOnly(decisions.ToArray()),
            Array.AsReadOnly(completed.ToArray()),
            Array.AsReadOnly(occupancyConflicts.ToArray()));
    }

    private static bool HasReachedRouteEnd(AiTimetableTrainRuntimeBinding train)
    {
        var waypoints = train.Route.RouteWaypointDistancesMeters;
        if (waypoints.Count == 0 || train.Driver.State.Cars.Count == 0)
            return false;
        var end = waypoints[^1];
        var position = train.Driver.State.Cars[0].DistanceMeters;
        return train.Route.Direction >= 0 ? position >= end - 1 : position <= end + 1;
    }
}

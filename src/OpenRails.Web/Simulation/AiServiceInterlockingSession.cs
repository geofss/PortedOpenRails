using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

/// <summary>
/// Couples the browser AI service path follower to the route-reservation and
/// junction-locking runtime. Dispatch is permitted only after the complete
/// service route has been reserved, following Open Rails' AI route/AILock flow.
/// </summary>
public sealed class AiServiceInterlockingSession
{
    private readonly string trainId;
    private readonly SignalInterlockingController interlocking;
    private readonly AiServiceDrivingController driver;
    private bool dispatched;

    public AiServiceInterlockingSession(
        string trainId,
        SignalInterlockingController interlocking,
        AiServiceDrivingController? driver = null)
    {
        if (string.IsNullOrWhiteSpace(trainId))
            throw new ArgumentException("A non-empty train identity is required.", nameof(trainId));
        this.trainId = trainId;
        this.interlocking = interlocking ?? throw new ArgumentNullException(nameof(interlocking));
        this.driver = driver ?? new AiServiceDrivingController();
    }

    public bool IsDispatched => dispatched;

    /// <summary>Atomically claims every route circuit and required junction branch before movement.</summary>
    public bool TryDispatch(
        IReadOnlyList<uint> routeCircuitIds,
        IReadOnlyDictionary<uint, uint> requiredJunctionBranches)
    {
        ArgumentNullException.ThrowIfNull(routeCircuitIds);
        ArgumentNullException.ThrowIfNull(requiredJunctionBranches);
        if (dispatched)
            return false;
        dispatched = interlocking.TryReserveRoute(trainId, routeCircuitIds, requiredJunctionBranches);
        return dispatched;
    }

    /// <summary>
    /// Rebuilds occupied track circuits from every car body on the selected
    /// connected TDB route, so trailing vehicles retain block and switch locks.
    /// </summary>
    public bool UpdateConsistOccupancy(
        TrackNetworkRoute route,
        IReadOnlyList<TrainCarRouteOccupancy> cars)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(cars);
        if (!dispatched)
            return false;
        var occupied = SignalOccupancyResolver.Resolve(route, cars);
        return interlocking.SetOccupiedCircuits(trainId, occupied);
    }

    /// <summary>Mirrors the train's front/rear occupancy updates into native-style track circuits.</summary>
    public bool SetCircuitOccupancy(uint circuitId, bool occupied) =>
        dispatched && interlocking.SetOccupied(trainId, circuitId, occupied);

    /// <summary>Runs the existing service-path controller only while its route remains dispatched.</summary>
    public AiServiceControlDecision Drive(
        BrowserTrainDrivingController train,
        ServiceRouteStartPosition route,
        double targetSpeedMetersPerSecond,
        IReadOnlyList<AiServiceSignalConstraint>? signalConstraints = null)
    {
        ArgumentNullException.ThrowIfNull(train);
        ArgumentNullException.ThrowIfNull(route);
        if (!dispatched)
        {
            train.SetThrottle(0);
            train.SetBrake(100);
            return new AiServiceControlDecision(0, 100, 0, -1, false, 0);
        }
        return driver.Apply(train, route, targetSpeedMetersPerSecond, signalConstraints);
    }

    /// <summary>
    /// Evaluates the route's native SIGCFG/SIGSCR signal network against live
    /// interlocking occupancy and switch state, then maps signal heads onto
    /// the active .pat route distances for the AI speed governor.
    /// </summary>
    public IReadOnlyList<AiServiceSignalConstraint> ResolveSignalConstraints(
        SignalNetwork signalNetwork,
        IReadOnlyDictionary<int, double> signalRouteDistancesMeters,
        SignalScriptLibrary? scripts = null)
    {
        ArgumentNullException.ThrowIfNull(signalNetwork);
        ArgumentNullException.ThrowIfNull(signalRouteDistancesMeters);
        var state = interlocking.Snapshot();
        return signalNetwork.Evaluate(state, scripts, trainId)
            .Where(indication => signalRouteDistancesMeters.ContainsKey(indication.Signal.TrackItemIndex))
            .Select(indication => new AiServiceSignalConstraint(
                signalRouteDistancesMeters[indication.Signal.TrackItemIndex],
                indication.Aspect,
                indication.SpeedLimitMetersPerSecond))
            .ToArray();
    }

    /// <summary>Drives an AI service using signal aspects evaluated from live route occupancy.</summary>
    public AiServiceControlDecision Drive(
        BrowserTrainDrivingController train,
        ServiceRouteStartPosition route,
        double targetSpeedMetersPerSecond,
        SignalNetwork signalNetwork,
        IReadOnlyDictionary<int, double> signalRouteDistancesMeters,
        SignalScriptLibrary? scripts = null)
    {
        var constraints = ResolveSignalConstraints(signalNetwork, signalRouteDistancesMeters, scripts);
        return Drive(train, route, targetSpeedMetersPerSecond, constraints);
    }

    /// <summary>Completes dispatch only after all occupied circuits have cleared.</summary>
    public bool TryComplete()
    {
        if (!dispatched || !interlocking.TryReleaseRoute(trainId))
            return false;
        dispatched = false;
        driver.ResetWaitingPoints();
        return true;
    }
}

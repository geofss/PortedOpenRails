namespace OpenRails.Web.Simulation;

/// <summary>
/// Deterministic timetable clock for native service definitions already bound
/// to their .pat paths and AI interlocking sessions. A blocked departure remains
/// pending and is retried on the next simulation tick rather than spawning a
/// train into an unreserved route.
/// </summary>
public sealed class AiTimetableDispatcher
{
    private readonly object gate = new();
    private readonly List<ScheduledAiService> services = [];
    private double elapsedSeconds;

    public double ElapsedSeconds
    {
        get { lock (gate) return elapsedSeconds; }
    }

    public void AddService(string serviceId, double departureSeconds,
        AiServiceInterlockingSession session,
        IReadOnlyList<uint> routeCircuitIds,
        IReadOnlyDictionary<uint, uint> requiredJunctionBranches)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
            throw new ArgumentException("Service identity is required.", nameof(serviceId));
        if (!double.IsFinite(departureSeconds) || departureSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(departureSeconds));
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(routeCircuitIds);
        ArgumentNullException.ThrowIfNull(requiredJunctionBranches);
        lock (gate)
        {
            if (services.Any(item => string.Equals(item.ServiceId, serviceId, StringComparison.Ordinal)))
                throw new ArgumentException($"Duplicate timetable service '{serviceId}'.", nameof(serviceId));
            services.Add(new ScheduledAiService(serviceId, departureSeconds, session,
                routeCircuitIds.ToArray(), new Dictionary<uint, uint>(requiredJunctionBranches)));
            services.Sort((left, right) => left.DepartureSeconds.CompareTo(right.DepartureSeconds));
        }
    }

    /// <summary>Advances simulation time and dispatches all due services whose paths can be reserved.</summary>
    public IReadOnlyList<string> Advance(double deltaSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        lock (gate)
        {
            elapsedSeconds += deltaSeconds;
            var dispatched = new List<string>();
            foreach (var service in services)
            {
                if (service.IsDispatched || service.DepartureSeconds > elapsedSeconds)
                    continue;
                if (service.Session.TryDispatch(service.RouteCircuitIds, service.RequiredJunctionBranches))
                {
                    service.IsDispatched = true;
                    dispatched.Add(service.ServiceId);
                }
            }
            return dispatched.AsReadOnly();
        }
    }

    public IReadOnlyList<string> PendingServices()
    {
        lock (gate)
            return services.Where(item => !item.IsDispatched)
                .Select(item => item.ServiceId).ToArray();
    }

    private sealed class ScheduledAiService(
        string serviceId, double departureSeconds, AiServiceInterlockingSession session,
        IReadOnlyList<uint> routeCircuitIds, IReadOnlyDictionary<uint, uint> requiredJunctionBranches)
    {
        public string ServiceId { get; } = serviceId;
        public double DepartureSeconds { get; } = departureSeconds;
        public AiServiceInterlockingSession Session { get; } = session;
        public IReadOnlyList<uint> RouteCircuitIds { get; } = routeCircuitIds;
        public IReadOnlyDictionary<uint, uint> RequiredJunctionBranches { get; } = requiredJunctionBranches;
        public bool IsDispatched { get; set; }
    }
}

using OpenRails.Web.Parsing;

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
            services.Sort((left, right) =>
            {
                var time = left.DepartureSeconds.CompareTo(right.DepartureSeconds);
                return time != 0 ? time : StringComparer.Ordinal.Compare(left.ServiceId, right.ServiceId);
            });
        }
    }

    /// <summary>
    /// Registers a service directly from native timetable header metadata.
    /// Route/path resolution remains a separate input, as in Open Rails' path preprocessing.
    /// </summary>
    public void AddService(
        NativeTimetableTrainDefinition definition,
        AiServiceInterlockingSession session,
        IReadOnlyList<uint> routeCircuitIds,
        IReadOnlyDictionary<uint, uint> requiredJunctionBranches)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!definition.TryGetDepartureSeconds(out var departureSeconds))
            throw new InvalidDataException(
                $"Native timetable service '{definition.Train}' has an invalid or missing #start time '{definition.StartTime}'.");
        AddService(definition.Train, departureSeconds, session, routeCircuitIds, requiredJunctionBranches);
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
                if (service.Status == AiTimetableServiceStatus.Dispatched && !service.Session.IsDispatched)
                    service.Status = AiTimetableServiceStatus.Completed;
                if (service.Status != AiTimetableServiceStatus.Pending || service.DepartureSeconds > elapsedSeconds)
                    continue;
                if (service.Session.TryDispatch(service.RouteCircuitIds, service.RequiredJunctionBranches))
                {
                    service.Status = AiTimetableServiceStatus.Dispatched;
                    dispatched.Add(service.ServiceId);
                }
            }
            return dispatched.AsReadOnly();
        }
    }

    public IReadOnlyList<string> PendingServices()
    {
        lock (gate)
            return services.Where(item => item.Status == AiTimetableServiceStatus.Pending)
                .Select(item => item.ServiceId).ToArray();
    }

    /// <summary>Removes a not-yet-dispatched service from the active timetable.</summary>
    public bool TryCancelService(string serviceId)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
            return false;
        lock (gate)
        {
            var service = services.FirstOrDefault(item =>
                string.Equals(item.ServiceId, serviceId, StringComparison.Ordinal));
            if (service is null || service.Status != AiTimetableServiceStatus.Pending)
                return false;
            service.Status = AiTimetableServiceStatus.Cancelled;
            return true;
        }
    }

    public AiTimetableServiceStatus? GetServiceStatus(string serviceId)
    {
        if (string.IsNullOrWhiteSpace(serviceId)) return null;
        lock (gate)
            return services.FirstOrDefault(item =>
                string.Equals(item.ServiceId, serviceId, StringComparison.Ordinal))?.Status;
    }

    public IReadOnlyDictionary<string, AiTimetableServiceStatus> SnapshotStatuses()
    {
        lock (gate)
            return new System.Collections.ObjectModel.ReadOnlyDictionary<string, AiTimetableServiceStatus>(
                services.ToDictionary(item => item.ServiceId, item => item.Status, StringComparer.Ordinal));
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
        public AiTimetableServiceStatus Status { get; set; } = AiTimetableServiceStatus.Pending;
    }
}

public enum AiTimetableServiceStatus
{
    Pending,
    Dispatched,
    Completed,
    Cancelled
}

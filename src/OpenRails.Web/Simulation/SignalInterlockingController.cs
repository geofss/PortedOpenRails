using System.Collections.ObjectModel;

namespace OpenRails.Web.Simulation;

/// <summary>
/// Browser-port interlocking state for route reservations and switch locks.
/// Behaviour is adapted from Open Rails' SignalObject.InternalBlockstate,
/// TrackCircuitSection.CircuitState, JunctionSetManual, and AILock.
/// This is an incremental port boundary, not a claim of complete native parity.
/// </summary>
public sealed class SignalInterlockingController
{
    private readonly object gate = new();
    private readonly HashSet<uint> circuits;
    private readonly IReadOnlyDictionary<uint, HashSet<uint>> junctionBranches;
    private readonly Dictionary<uint, string> reservations = [];
    private readonly Dictionary<uint, string> occupancy = [];
    private readonly Dictionary<uint, string> junctionLocks = [];
    private readonly Dictionary<uint, uint> selectedBranches = [];
    private readonly Dictionary<string, HashSet<string>> waitsFor = new(StringComparer.Ordinal);

    public SignalInterlockingController(
        IEnumerable<uint> trackCircuitIds,
        IReadOnlyDictionary<uint, IReadOnlyCollection<uint>> junctionBranchOptions)
    {
        ArgumentNullException.ThrowIfNull(trackCircuitIds);
        ArgumentNullException.ThrowIfNull(junctionBranchOptions);
        circuits = trackCircuitIds.ToHashSet();
        if (circuits.Count == 0 || circuits.Contains(0))
            throw new ArgumentException("Track circuit IDs must be non-empty and non-zero.", nameof(trackCircuitIds));

        var branchMap = new Dictionary<uint, HashSet<uint>>();
        foreach (var (junction, branches) in junctionBranchOptions)
        {
            if (junction == 0 || branches is null)
                throw new ArgumentException("Junction IDs and branch collections must be valid.", nameof(junctionBranchOptions));
            var options = branches.ToHashSet();
            if (options.Count < 2 || options.Any(branch => !circuits.Contains(branch)))
                throw new ArgumentException($"Junction {junction} must have at least two known branch circuits.", nameof(junctionBranchOptions));
            branchMap.Add(junction, options);
        }
        junctionBranches = new ReadOnlyDictionary<uint, HashSet<uint>>(branchMap);
    }

    /// <summary>
    /// Returns deterministic wait-for cycles among AI route reservations. A cycle
    /// indicates that every train in the returned chain is blocked by another
    /// member; callers can feed this into native deadlock path selection/replanning.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> SnapshotDeadlockCycles()
    {
        lock (gate)
        {
            var cycles = new List<IReadOnlyList<string>>();
            var canonical = new HashSet<string>(StringComparer.Ordinal);
            foreach (var start in waitsFor.Keys.OrderBy(id => id, StringComparer.Ordinal))
            {
                var path = new List<string>();
                var positions = new Dictionary<string, int>(StringComparer.Ordinal);
                Explore(start);
                void Explore(string current)
                {
                    if (positions.TryGetValue(current, out var cycleStart))
                    {
                        var cycle = path.Skip(cycleStart).ToArray();
                        if (cycle.Length < 2) return;
                        var normalized = Normalize(cycle);
                        if (canonical.Add(string.Join("\\0", normalized)))
                            cycles.Add(Array.AsReadOnly(normalized));
                        return;
                    }
                    if (path.Count >= waitsFor.Count) return;
                    positions[current] = path.Count;
                    path.Add(current);
                    if (waitsFor.TryGetValue(current, out var blockers))
                        foreach (var next in blockers.OrderBy(id => id, StringComparer.Ordinal))
                            if (waitsFor.ContainsKey(next))
                                Explore(next);
                    path.RemoveAt(path.Count - 1);
                    positions.Remove(current);
                }
            }
            return Array.AsReadOnly(cycles.ToArray());
        }
    }

    private static string[] Normalize(IReadOnlyList<string> cycle)
    {
        var best = 0;
        for (var index = 1; index < cycle.Count; index++)
            if (StringComparer.Ordinal.Compare(cycle[index], cycle[best]) < 0)
                best = index;
        return Enumerable.Range(0, cycle.Count).Select(offset =>
            cycle[(best + offset) % cycle.Count]).ToArray();
    }

    public bool TrySetJunctionBranch(uint junctionId, uint branchCircuitId)
    {
        lock (gate)
        {
            if (!junctionBranches.TryGetValue(junctionId, out var options) || !options.Contains(branchCircuitId))
                return false;
            if (junctionLocks.ContainsKey(junctionId))
                return false;
            selectedBranches[junctionId] = branchCircuitId;
            return true;
        }
    }

    /// <summary>
    /// Atomically reserves every circuit and switch branch for one train.
    /// A failed request changes no reservation or lock state.
    /// </summary>
    public bool TryReserveRoute(
        string trainId,
        IReadOnlyList<uint> routeCircuitIds,
        IReadOnlyDictionary<uint, uint> requiredJunctionBranches)
    {
        ValidateTrainId(trainId);
        ArgumentNullException.ThrowIfNull(routeCircuitIds);
        ArgumentNullException.ThrowIfNull(requiredJunctionBranches);
        if (routeCircuitIds.Count == 0 || routeCircuitIds.Any(id => !circuits.Contains(id)) ||
            routeCircuitIds.Distinct().Count() != routeCircuitIds.Count)
            return false;

        lock (gate)
        {
            var blockers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var circuit in routeCircuitIds)
            {
                if (occupancy.TryGetValue(circuit, out var occupant) && occupant != trainId)
                    blockers.Add(occupant);
                if (reservations.TryGetValue(circuit, out var owner) && owner != trainId)
                    blockers.Add(owner);
            }

            foreach (var (junction, branch) in requiredJunctionBranches)
            {
                if (!junctionBranches.TryGetValue(junction, out var options) || !options.Contains(branch) ||
                    !routeCircuitIds.Contains(branch))
                    return false;
                if (junctionLocks.TryGetValue(junction, out var lockOwner) && lockOwner != trainId)
                    blockers.Add(lockOwner);
                if (selectedBranches.TryGetValue(junction, out var selected) && selected != branch &&
                    junctionLocks.TryGetValue(junction, out var selectedOwner) && selectedOwner != trainId)
                    blockers.Add(selectedOwner);
            }

            if (blockers.Count > 0)
            {
                waitsFor[trainId] = blockers;
                return false;
            }

            // Commit only after the complete route has passed every conflict check.
            foreach (var circuit in routeCircuitIds)
                reservations[circuit] = trainId;
            foreach (var (junction, branch) in requiredJunctionBranches)
            {
                junctionLocks[junction] = trainId;
                selectedBranches[junction] = branch;
            }
            waitsFor.Remove(trainId);
            return true;
        }
    }

    /// <summary>
    /// Atomically replaces a train's reserved route. Existing reservations are
    /// retained if any new circuit or junction conflicts, and a replacement may
    /// never discard a circuit still occupied by any vehicle in the consist.
    /// </summary>
    public bool TryReplaceRoute(
        string trainId,
        IReadOnlyList<uint> routeCircuitIds,
        IReadOnlyDictionary<uint, uint> requiredJunctionBranches)
    {
        ValidateTrainId(trainId);
        ArgumentNullException.ThrowIfNull(routeCircuitIds);
        ArgumentNullException.ThrowIfNull(requiredJunctionBranches);
        if (routeCircuitIds.Count == 0 || routeCircuitIds.Any(id => !circuits.Contains(id)) ||
            routeCircuitIds.Distinct().Count() != routeCircuitIds.Count)
            return false;

        lock (gate)
        {
            var replacement = routeCircuitIds.ToHashSet();
            if (occupancy.Any(pair => pair.Value == trainId && !replacement.Contains(pair.Key)))
                return false;

            foreach (var circuit in replacement)
            {
                if (occupancy.TryGetValue(circuit, out var occupant) && occupant != trainId)
                    return false;
                if (reservations.TryGetValue(circuit, out var owner) && owner != trainId)
                    return false;
            }

            foreach (var (junction, branch) in requiredJunctionBranches)
            {
                if (!junctionBranches.TryGetValue(junction, out var options) || !options.Contains(branch) ||
                    !replacement.Contains(branch))
                    return false;
                if (junctionLocks.TryGetValue(junction, out var lockOwner) && lockOwner != trainId)
                    return false;
            }

            foreach (var circuit in reservations.Where(pair => pair.Value == trainId && !replacement.Contains(pair.Key))
                .Select(pair => pair.Key).ToArray())
                reservations.Remove(circuit);
            foreach (var junction in junctionLocks.Where(pair => pair.Value == trainId &&
                !requiredJunctionBranches.ContainsKey(pair.Key)).Select(pair => pair.Key).ToArray())
                junctionLocks.Remove(junction);

            foreach (var circuit in replacement)
                reservations[circuit] = trainId;
            foreach (var (junction, branch) in requiredJunctionBranches)
            {
                junctionLocks[junction] = trainId;
                selectedBranches[junction] = branch;
            }
            waitsFor.Remove(trainId);
            return true;
        }
    }

    public bool SetOccupied(string trainId, uint circuitId, bool isOccupied)
    {
        ValidateTrainId(trainId);
        if (!circuits.Contains(circuitId))
            return false;
        lock (gate)
        {
            if (isOccupied)
            {
                if (occupancy.TryGetValue(circuitId, out var owner) && owner != trainId)
                    return false;
                if (reservations.TryGetValue(circuitId, out var reservationOwner) && reservationOwner != trainId)
                    return false;
                occupancy[circuitId] = trainId;
            }
            else if (occupancy.TryGetValue(circuitId, out var currentOwner) && currentOwner == trainId)
            {
                occupancy.Remove(circuitId);
            }
            else
            {
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Releases a route only after the train has cleared every circuit it occupies.
    /// This preserves the native interlocking safety invariant: a switch must not
    /// unlock underneath a train merely because its dispatcher requested release.
    /// </summary>
    public bool TryReleaseRoute(string trainId)
    {
        ValidateTrainId(trainId);
        lock (gate)
        {
            if (occupancy.Values.Contains(trainId))
                return false;

            foreach (var circuit in reservations.Where(pair => pair.Value == trainId).Select(pair => pair.Key).ToArray())
                reservations.Remove(circuit);
            foreach (var junction in junctionLocks.Where(pair => pair.Value == trainId).Select(pair => pair.Key).ToArray())
                junctionLocks.Remove(junction);
            waitsFor.Remove(trainId);
            foreach (var blockers in waitsFor.Values)
                blockers.Remove(trainId);
            return true;
        }
    }

    /// <summary>
    /// Atomically replaces one train's occupied circuit set using the current
    /// positions of all consist vehicles, including trailing vehicles.
    /// </summary>
    public bool SetOccupiedCircuits(string trainId, IReadOnlySet<uint> occupiedCircuitIds)
    {
        ValidateTrainId(trainId);
        ArgumentNullException.ThrowIfNull(occupiedCircuitIds);
        if (occupiedCircuitIds.Any(id => !circuits.Contains(id)))
            return false;

        lock (gate)
        {
            foreach (var circuit in occupiedCircuitIds)
            {
                if (occupancy.TryGetValue(circuit, out var owner) && owner != trainId)
                    return false;
                if (reservations.TryGetValue(circuit, out var reservationOwner) && reservationOwner != trainId)
                    return false;
            }

            foreach (var circuit in occupancy.Where(pair => pair.Value == trainId)
                .Select(pair => pair.Key).ToArray())
                occupancy.Remove(circuit);
            foreach (var circuit in occupiedCircuitIds)
                occupancy[circuit] = trainId;
            return true;
        }
    }

    public void ReleaseRoute(string trainId) => TryReleaseRoute(trainId);

    /// <summary>
    /// Returns trains participating in a wait-for cycle created by blocked
    /// route reservations. This is a diagnostic, not an automatic route
    /// cancellation: native dispatch policy must choose a safe recovery.
    /// </summary>
    public IReadOnlySet<string> GetDeadlockedTrainIds()
    {
        lock (gate)
        {
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var deadlocked = new HashSet<string>(StringComparer.Ordinal);
            var stack = new List<string>();

            void Visit(string train)
            {
                if (visiting.Contains(train))
                {
                    var cycleStart = stack.IndexOf(train);
                    if (cycleStart >= 0)
                        foreach (var member in stack.Skip(cycleStart))
                            deadlocked.Add(member);
                    return;
                }
                if (!visited.Add(train))
                    return;

                visiting.Add(train);
                stack.Add(train);
                if (waitsFor.TryGetValue(train, out var blockers))
                    foreach (var blocker in blockers)
                        Visit(blocker);
                stack.RemoveAt(stack.Count - 1);
                visiting.Remove(train);
            }

            foreach (var train in waitsFor.Keys)
                Visit(train);
            return deadlocked;
        }
    }

    public SignalInterlockingSnapshot Snapshot()
    {
        lock (gate)
            return new SignalInterlockingSnapshot(
                new ReadOnlyDictionary<uint, string>(new Dictionary<uint, string>(reservations)),
                new ReadOnlyDictionary<uint, string>(new Dictionary<uint, string>(occupancy)),
                new ReadOnlyDictionary<uint, string>(new Dictionary<uint, string>(junctionLocks)),
                new ReadOnlyDictionary<uint, uint>(new Dictionary<uint, uint>(selectedBranches)));
    }

    private static void ValidateTrainId(string trainId)
    {
        if (string.IsNullOrWhiteSpace(trainId))
            throw new ArgumentException("A non-empty train identity is required.", nameof(trainId));
    }
}

public sealed record SignalInterlockingSnapshot(
    IReadOnlyDictionary<uint, string> ReservedCircuits,
    IReadOnlyDictionary<uint, string> OccupiedCircuits,
    IReadOnlyDictionary<uint, string> JunctionLocks,
    IReadOnlyDictionary<uint, uint> SelectedJunctionBranches);

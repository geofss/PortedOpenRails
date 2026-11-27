using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

/// <summary>
/// Builds the route's signal-to-track topology from the real TDB signal items,
/// vector-node item references, pins, and junction links. This is the structural
/// foundation for track circuits and aspect evaluation; it deliberately does not
/// execute MSTS/ORTS SIGSCR scripts.
/// </summary>
public sealed class SignalNetworkBuilder
{
    public SignalNetwork Build(TrackDatabase database, TrackNetwork network, SignalConfigurationDefinition? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(network);

        var vectorNodes = network.VectorPaths.ToDictionary(path => path.TrackNodeIndex);
        var adjacency = vectorNodes.Keys.ToDictionary(id => id, _ => new HashSet<uint>());
        var junctionLinks = new Dictionary<uint, HashSet<uint>>();

        foreach (var path in network.VectorPaths)
        foreach (var endpoint in path.ConnectedNodeIndices.Distinct())
        {
            if (!database.TryGetNode(endpoint, out var endpointNode) || endpointNode is null)
                continue;

            var connectedVectors = endpointNode.Kind == TrackNodeKind.Junction
                ? endpointNode.Pins.Select(pin => pin.Link)
                    .Where(link => link > 0 && link != (int)path.TrackNodeIndex && vectorNodes.ContainsKey((uint)link))
                    .Select(link => (uint)link).Distinct().ToArray()
                : endpointNode.Kind == TrackNodeKind.Vector && endpoint != (int)path.TrackNodeIndex && vectorNodes.ContainsKey((uint)endpoint)
                    ? new[] { (uint)endpoint }
                    : Array.Empty<uint>();

            if (endpointNode.Kind == TrackNodeKind.Junction)
            {
                if (!junctionLinks.TryGetValue((uint)endpoint, out var links))
                    junctionLinks[(uint)endpoint] = links = new HashSet<uint>();
                links.UnionWith(connectedVectors);
            }

            foreach (var other in connectedVectors)
            {
                adjacency[path.TrackNodeIndex].Add(other);
                adjacency[other].Add(path.TrackNodeIndex);
            }
        }

        var signals = new List<RouteSignalHead>();
        var diagnostics = new List<string>();
        foreach (var item in database.TrackItems.Where(item => item?.Type == "signal").Cast<TrackItem>())
        {
            var hostingPaths = database.EnumerateNodes()
                .Where(node => node.Kind == TrackNodeKind.Vector && node.TrackItemReferences.Contains(item.Index))
                .ToArray();

            if (hostingPaths.Length == 0)
            {
                diagnostics.Add($"Signal item {item.Index} is not referenced by any vector TrackNode.");
                continue;
            }

            foreach (var host in hostingPaths)
            {
                if (!vectorNodes.TryGetValue(host.Index, out var path))
                {
                    diagnostics.Add($"Signal item {item.Index} references vector TrackNode {host.Index}, but no usable track geometry was built.");
                    continue;
                }

                var directions = item.SignalDirections ?? Array.Empty<TrackSignalDirection>();
                foreach (var direction in directions)
                {
                    if (!database.TryGetNode((int)direction.TrackNode, out var junction) || junction?.Kind != TrackNodeKind.Junction)
                        diagnostics.Add($"Signal item {item.Index} has a direction record referencing non-junction TrackNode {direction.TrackNode}.");
                }

                signals.Add(new RouteSignalHead(
                    item.Index,
                    item.SignalType ?? string.Empty,
                    item.SignalFlags ?? string.Empty,
                    item.SignalDirection ?? 0,
                    item.SignalData ?? 0,
                    host.Index,
                    Math.Clamp(item.DistanceAlongTrack, 0, (float)path.LengthMeters),
                    Array.AsReadOnly(directions.ToArray()),
                    configuration is not null && configuration.SignalTypes.TryGetValue(item.SignalType ?? string.Empty, out var configuredType)
                        ? configuredType
                        : null));
            }
        }

        var blocks = vectorNodes.Values.Select(path =>
        {
            var junctionNodes = path.ConnectedNodeIndices
                .Where(endpoint => database.TryGetNode(endpoint, out var node) && node?.Kind == TrackNodeKind.Junction)
                .Select(endpoint => checked((uint)endpoint)).Distinct().OrderBy(id => id).ToArray();
            return new SignalTrackCircuit(
                path.TrackNodeIndex,
                path.LengthMeters,
                Array.AsReadOnly(adjacency[path.TrackNodeIndex].OrderBy(id => id).ToArray()),
                Array.AsReadOnly(signals.Where(signal => signal.TrackNodeIndex == path.TrackNodeIndex)
                    .OrderBy(signal => signal.DistanceMeters).ToArray()),
                Array.AsReadOnly(junctionNodes));
        }).ToArray();

        var immutableJunctionLinks = junctionLinks.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<uint>)Array.AsReadOnly(pair.Value.OrderBy(id => id).ToArray()));
        return new SignalNetwork(blocks, immutableJunctionLinks, diagnostics);
    }
}

/// <summary>A single vector-node track-circuit candidate, retaining the original TDB node ID.</summary>
public sealed record SignalTrackCircuit(
    uint TrackNodeIndex,
    double LengthMeters,
    IReadOnlyList<uint> ConnectedTrackNodeIndices,
    IReadOnlyList<RouteSignalHead> Signals,
    IReadOnlyList<uint>? JunctionNodeIndices = null);

/// <summary>Signal head placement and its original TDB direction records.</summary>
public sealed record RouteSignalHead(
    int TrackItemIndex,
    string SignalType,
    string Flags,
    uint Direction,
    float SignalData,
    uint TrackNodeIndex,
    double DistanceMeters,
    IReadOnlyList<TrackSignalDirection> Directions,
    SignalTypeDefinition? Definition = null);

public enum RouteSignalAspect
{
    Stop,
    Approach,
    Clear1,
    Clear2
}

public sealed record RouteSignalIndication(
    RouteSignalHead Signal,
    RouteSignalAspect Aspect,
    uint? NextTrackNodeIndex,
    string Reason,
    string? NativeAspect = null,
    string? DrawState = null,
    float? SpeedLimitMetersPerSecond = null);

/// <summary>
/// Route-level occupancy and conservative fixed-block aspect evaluation. Ambiguous
/// junction routes fail restrictive; no signal is cleared through an unresolved switch.
/// </summary>
public sealed class SignalNetwork
{
    private readonly IReadOnlyDictionary<uint, SignalTrackCircuit> blocks;
    private readonly IReadOnlyDictionary<uint, IReadOnlyList<uint>> junctionLinks;

    public IReadOnlyList<SignalTrackCircuit> TrackCircuits { get; }
    public IReadOnlyList<string> Diagnostics { get; }

    internal SignalNetwork(IEnumerable<SignalTrackCircuit> circuits,
        IReadOnlyDictionary<uint, IReadOnlyList<uint>> junctionLinks, IEnumerable<string> diagnostics)
    {
        TrackCircuits = Array.AsReadOnly(circuits.ToArray());
        blocks = TrackCircuits.ToDictionary(block => block.TrackNodeIndex);
        this.junctionLinks = junctionLinks;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public IReadOnlyList<RouteSignalIndication> Evaluate(
        IReadOnlySet<uint> occupiedTrackNodeIndices,
        IReadOnlyDictionary<uint, uint>? selectedJunctionBranches = null)
    {
        ArgumentNullException.ThrowIfNull(occupiedTrackNodeIndices);
        var result = new List<RouteSignalIndication>();

        foreach (var block in TrackCircuits)
        foreach (var signal in block.Signals)
        {
            if (occupiedTrackNodeIndices.Contains(block.TrackNodeIndex))
            {
                result.Add(CreateIndication(signal, RouteSignalAspect.Stop, null, "The signal's own track circuit is occupied."));
                continue;
            }

            var requiredClearAhead = signal.Definition?.NumberClearAhead is >= 0 and <= 32
                ? Math.Max(1, signal.Definition.NumberClearAhead)
                : 2;
            var current = block.TrackNodeIndex;
            uint? previous = null;
            uint? firstNext = null;
            var clearBlocks = 0;
            var blocked = false;
            var unresolved = false;
            for (var clearIndex = 0; clearIndex < requiredClearAhead; clearIndex++)
            {
                var next = ResolveNextBlock(current, clearIndex == 0 ? signal : null,
                    selectedJunctionBranches, previous);
                if (next is null)
                {
                    unresolved = true;
                    break;
                }
                firstNext ??= next;
                if (occupiedTrackNodeIndices.Contains(next.Value))
                {
                    blocked = true;
                    break;
                }
                clearBlocks++;
                previous = current;
                current = next.Value;
            }

            if (clearBlocks == 0 && (blocked || unresolved))
                result.Add(CreateIndication(signal, RouteSignalAspect.Stop, firstNext,
                    blocked ? "The next track circuit is occupied." : "No unique, selected route is available beyond this signal."));
            else if (blocked || unresolved)
                result.Add(CreateIndication(signal, RouteSignalAspect.Approach, firstNext,
                    blocked ? "A required track circuit ahead is occupied." : "The required clear-ahead route is not uniquely established."));
            else
                result.Add(CreateIndication(signal,
                    requiredClearAhead == 1 ? RouteSignalAspect.Clear1 : RouteSignalAspect.Clear2,
                    firstNext, $"The configured {requiredClearAhead}-block clear-ahead requirement is satisfied."));
        }

        return Array.AsReadOnly(result.ToArray());
    }

    private static RouteSignalIndication CreateIndication(
        RouteSignalHead signal, RouteSignalAspect aspect, uint? next, string reason)
    {
        var candidates = aspect switch
        {
            RouteSignalAspect.Stop => new[] { "STOP", "STOP_AND_PROCEED", "RESTRICTING" },
            RouteSignalAspect.Approach => new[] { "APPROACH_1", "APPROACH_2", "APPROACH_3" },
            RouteSignalAspect.Clear1 => new[] { "CLEAR_1", "CLEAR_2" },
            RouteSignalAspect.Clear2 => new[] { "CLEAR_2", "CLEAR_1" },
            _ => Array.Empty<string>()
        };
        var configured = signal.Definition?.Aspects
            .Where(candidate => candidates.Contains(candidate.Aspect, StringComparer.OrdinalIgnoreCase))
            .OrderBy(candidate => Array.IndexOf(candidates, candidate.Aspect.ToUpperInvariant()))
            .FirstOrDefault();
        if (signal.Definition is not null && configured is null)
            reason += $" Configured signal type '{signal.Definition.Name}' has no matching {aspect} aspect.";
        return new RouteSignalIndication(signal, aspect, next, reason,
            configured?.Aspect, configured?.DrawState,
            configured is { SpeedMetersPerSecond: >= 0 } ? configured.SpeedMetersPerSecond : null);
    }

    private uint? ResolveNextBlock(
        uint current,
        RouteSignalHead? signal,
        IReadOnlyDictionary<uint, uint>? selectedBranches,
        uint? previous = null)
    {
        if (!blocks.TryGetValue(current, out var block))
            return null;

        var candidates = block.ConnectedTrackNodeIndices
            .Where(id => id != previous && blocks.ContainsKey(id))
            .Distinct().ToArray();

        // Direction records identify the junction controlling this signal. If present,
        // only the branch selected for that junction is considered valid.
        var controlledJunctions = (block.JunctionNodeIndices ?? Array.Empty<uint>())
            .Concat(signal?.Directions.Select(direction => direction.TrackNode) ?? Array.Empty<uint>())
            .Distinct().ToArray();
        var viableCandidates = candidates.ToHashSet();
        foreach (var junction in controlledJunctions)
        {
            if (!junctionLinks.TryGetValue(junction, out var links) ||
                selectedBranches is null ||
                !selectedBranches.TryGetValue(junction, out var selected) ||
                !links.Contains(selected))
                return null;

            // Every connected junction must have a selected branch, not only
            // the junction named by the signal's own direction record.
            viableCandidates.IntersectWith(new[] { selected });
            if (viableCandidates.Count == 0)
                return null;
        }

        return viableCandidates.Count == 1 ? viableCandidates.Single() : null;
    }
}

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
    public SignalNetwork Build(TrackDatabase database, TrackNetwork network)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(network);

        var vectorNodes = network.VectorPaths.ToDictionary(path => path.TrackNodeIndex);
        var adjacency = vectorNodes.Keys.ToDictionary(id => id, _ => new HashSet<uint>());
        var junctionLinks = new Dictionary<uint, IReadOnlyList<uint>>();

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
                junctionLinks[(uint)endpoint] = Array.AsReadOnly(connectedVectors);

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
                    Array.AsReadOnly(directions.ToArray())));
            }
        }

        var blocks = vectorNodes.Values.Select(path => new SignalTrackCircuit(
            path.TrackNodeIndex,
            path.LengthMeters,
            Array.AsReadOnly(adjacency[path.TrackNodeIndex].OrderBy(id => id).ToArray()),
            Array.AsReadOnly(signals.Where(signal => signal.TrackNodeIndex == path.TrackNodeIndex)
                .OrderBy(signal => signal.DistanceMeters).ToArray()))).ToArray();

        return new SignalNetwork(blocks, junctionLinks, diagnostics);
    }
}

/// <summary>A single vector-node track-circuit candidate, retaining the original TDB node ID.</summary>
public sealed record SignalTrackCircuit(
    uint TrackNodeIndex,
    double LengthMeters,
    IReadOnlyList<uint> ConnectedTrackNodeIndices,
    IReadOnlyList<RouteSignalHead> Signals);

/// <summary>Signal head placement and its original TDB direction records.</summary>
public sealed record RouteSignalHead(
    int TrackItemIndex,
    string SignalType,
    string Flags,
    uint Direction,
    float SignalData,
    uint TrackNodeIndex,
    double DistanceMeters,
    IReadOnlyList<TrackSignalDirection> Directions);

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
    string Reason);

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
            var next = ResolveNextBlock(block.TrackNodeIndex, signal, selectedJunctionBranches);
            if (occupiedTrackNodeIndices.Contains(block.TrackNodeIndex))
            {
                result.Add(new(signal, RouteSignalAspect.Stop, next, "The signal's own track circuit is occupied."));
                continue;
            }
            if (next is null)
            {
                result.Add(new(signal, RouteSignalAspect.Stop, null, "No unique, selected route is available beyond this signal."));
                continue;
            }
            if (occupiedTrackNodeIndices.Contains(next.Value))
            {
                result.Add(new(signal, RouteSignalAspect.Stop, next, "The next track circuit is occupied."));
                continue;
            }

            var beyond = ResolveNextBlock(next.Value, null, selectedJunctionBranches, block.TrackNodeIndex);
            if (beyond is null)
                result.Add(new(signal, RouteSignalAspect.Approach, next, "The route beyond the next block is not uniquely established."));
            else if (occupiedTrackNodeIndices.Contains(beyond.Value))
                result.Add(new(signal, RouteSignalAspect.Approach, next, "The second track circuit ahead is occupied."));
            else
                result.Add(new(signal, RouteSignalAspect.Clear2, next, "Two consecutive track circuits ahead are clear."));
        }

        return Array.AsReadOnly(result.ToArray());
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
        var controlledJunctions = signal?.Directions.Select(direction => direction.TrackNode).Distinct().ToArray()
            ?? Array.Empty<uint>();
        foreach (var junction in controlledJunctions)
        {
            if (!junctionLinks.TryGetValue(junction, out var links))
                continue;
            if (selectedBranches is null || !selectedBranches.TryGetValue(junction, out var selected))
                return null;
            if (!links.Contains(selected))
                return null;
            if (candidates.Contains(selected))
                return selected;
        }

        if (candidates.Length == 1)
            return candidates[0];
        if (candidates.Length != 0 || controlledJunctions.Length != 0)
            return null;

        return null;
    }
}

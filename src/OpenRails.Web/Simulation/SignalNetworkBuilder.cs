using System.Collections.Frozen;
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

                SignalTypeDefinition? configuredType = null;
                if (configuration is not null &&
                    !configuration.SignalTypes.TryGetValue(item.SignalType ?? string.Empty, out configuredType))
                    diagnostics.Add($"Signal item {item.Index} references undefined SIGCFG type '{item.SignalType}'.");

                signals.Add(new RouteSignalHead(
                    item.Index,
                    item.SignalType ?? string.Empty,
                    item.SignalFlags ?? string.Empty,
                    item.SignalDirection ?? 0,
                    item.SignalData ?? 0,
                    host.Index,
                    Math.Clamp(item.DistanceAlongTrack, 0, (float)path.LengthMeters),
                    Array.AsReadOnly(directions.ToArray()),
                    configuredType));
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
        return new SignalNetwork(blocks, immutableJunctionLinks, diagnostics, configuration);
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
    private readonly SignalConfigurationDefinition? configuration;

    public IReadOnlyList<SignalTrackCircuit> TrackCircuits { get; }
    public IReadOnlyList<string> Diagnostics { get; }

    /// <summary>
    /// Creates the reservation/locking runtime from this network's actual TDB
    /// track-circuit and junction topology.
    /// </summary>
    public SignalInterlockingController CreateInterlockingController()
    {
        var branchOptions = junctionLinks
            .Where(pair => pair.Value.Count >= 2)
            .ToDictionary(pair => pair.Key,
                pair => (IReadOnlyCollection<uint>)pair.Value);
        return new SignalInterlockingController(blocks.Keys, branchOptions);
    }

    internal SignalNetwork(IEnumerable<SignalTrackCircuit> circuits,
        IReadOnlyDictionary<uint, IReadOnlyList<uint>> junctionLinks, IEnumerable<string> diagnostics,
        SignalConfigurationDefinition? configuration = null)
    {
        TrackCircuits = Array.AsReadOnly(circuits.ToArray());
        blocks = TrackCircuits.ToDictionary(block => block.TrackNodeIndex);
        this.junctionLinks = junctionLinks;
        this.configuration = configuration;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    /// <summary>Evaluates signals directly from the current interlocking occupancy and switch state.</summary>
    public IReadOnlyList<RouteSignalIndication> Evaluate(
        SignalInterlockingSnapshot interlockingState,
        SignalScriptLibrary? scripts = null,
        string? enabledTrainId = null)
    {
        ArgumentNullException.ThrowIfNull(interlockingState);
        IReadOnlySet<uint>? enabledTrackNodes = null;
        if (!string.IsNullOrWhiteSpace(enabledTrainId))
            enabledTrackNodes = interlockingState.ReservedCircuits
                .Where(pair => string.Equals(pair.Value, enabledTrainId, StringComparison.Ordinal))
                .Select(pair => pair.Key).ToFrozenSet();
        // A route reserved by another train is not available to this signal,
        // even before that train physically occupies the circuit. Keep the
        // evaluating train's own reservations available for native ENABLED /
        // ROUTE_SET script semantics.
        var unavailableCircuits = interlockingState.OccupiedCircuits.Keys
            .Concat(interlockingState.ReservedCircuits
                .Where(pair => string.IsNullOrWhiteSpace(enabledTrainId) ||
                    !string.Equals(pair.Value, enabledTrainId, StringComparison.Ordinal))
                .Select(pair => pair.Key))
            .ToFrozenSet();
        return Evaluate(unavailableCircuits,
            interlockingState.SelectedJunctionBranches, scripts, enabledTrackNodes);
    }

    public IReadOnlyList<RouteSignalIndication> Evaluate(
        IReadOnlySet<uint> occupiedTrackNodeIndices,
        IReadOnlyDictionary<uint, uint>? selectedJunctionBranches = null,
        SignalScriptLibrary? scripts = null,
        IReadOnlySet<uint>? enabledTrackNodeIndices = null)
    {
        ArgumentNullException.ThrowIfNull(occupiedTrackNodeIndices);
        var baselineBySignal = new Dictionary<RouteSignalHead, (RouteSignalIndication Baseline, int BlockState)>(
            ReferenceEqualityComparer.Instance);
        foreach (var block in TrackCircuits)
        foreach (var signal in block.Signals)
            baselineBySignal[signal] = EvaluateBaseline(signal, occupiedTrackNodeIndices, selectedJunctionBranches);

        var result = new List<RouteSignalIndication>();
        foreach (var block in TrackCircuits)
        foreach (var signal in block.Signals)
        {
            var (baseline, blockState) = baselineBySignal[signal];
            var enabled = enabledTrackNodeIndices?.Contains(block.TrackNodeIndex) == true;
            result.Add(ApplyNativeScript(signal, baseline, blockState, scripts,
                selectedJunctionBranches, enabled, enabledTrackNodeIndices,
                baselineBySignal));
        }

        return Array.AsReadOnly(result.ToArray());
    }

    private (RouteSignalIndication Baseline, int BlockState) EvaluateBaseline(
        RouteSignalHead signal,
        IReadOnlySet<uint> occupiedTrackNodeIndices,
        IReadOnlyDictionary<uint, uint>? selectedJunctionBranches)
    {
        if (!blocks.TryGetValue(signal.TrackNodeIndex, out var block))
            return (CreateIndication(signal, RouteSignalAspect.Stop, null,
                "The signal's track circuit is missing."), 2);

        var blockState = 0;
        if (occupiedTrackNodeIndices.Contains(block.TrackNodeIndex))
            return (CreateIndication(signal, RouteSignalAspect.Stop, null,
                "The signal's own track circuit is occupied."), 1);

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

        if (blocked) blockState = 1;
        else if (unresolved) blockState = 2;
        if (clearBlocks == 0 && (blocked || unresolved))
            return (CreateIndication(signal, RouteSignalAspect.Stop, firstNext,
                blocked ? "The next track circuit is occupied or reserved by another train." :
                    "No unique, selected route is available beyond this signal."), blockState);
        if (blocked || unresolved)
            return (CreateIndication(signal, RouteSignalAspect.Approach, firstNext,
                blocked ? "A required track circuit ahead is occupied or reserved by another train." :
                    "The required clear-ahead route is not uniquely established."), blockState);
        return (CreateIndication(signal,
            requiredClearAhead == 1 ? RouteSignalAspect.Clear1 : RouteSignalAspect.Clear2,
            firstNext, $"The configured {requiredClearAhead}-block clear-ahead requirement is satisfied."),
            blockState);
    }

    private RouteSignalIndication ApplyNativeScript(
        RouteSignalHead signal,
        RouteSignalIndication baseline,
        int blockState,
        SignalScriptLibrary? scripts,
        IReadOnlyDictionary<uint, uint>? selectedBranches,
        bool enabled,
        IReadOnlySet<uint>? enabledRouteCircuits,
        IReadOnlyDictionary<RouteSignalHead, (RouteSignalIndication Baseline, int BlockState)> baselineBySignal)
    {
        var scriptName = signal.Definition?.Script;
        if (string.IsNullOrWhiteSpace(scriptName) || scripts is null ||
            !scripts.Scripts.TryGetValue(scriptName, out var script))
            return baseline;

        var initialState = NativeAspectValue(baseline.NativeAspect) ??
            baseline.Aspect switch
            {
                RouteSignalAspect.Stop => 0,
                RouteSignalAspect.Approach => 3,
                RouteSignalAspect.Clear1 => 6,
                RouteSignalAspect.Clear2 => 7,
                _ => 0
            };
        int? ResolveDrawState(int nativeAspect)
        {
            var aspectName = NativeAspectName(nativeAspect);
            return aspectName is null ? null : signal.Definition?.Aspects
                .FirstOrDefault(aspect => aspect.Aspect.Equals(aspectName, StringComparison.OrdinalIgnoreCase))
                ?.DrawState is { Length: > 0 } drawName &&
                signal.Definition.DrawStates.TryGetValue(drawName, out var draw)
                    ? draw.Index : null;
        }

        int? ResolveSymbol(string name)
        {
            if (NativeAspectValue(name) is { } aspect)
                return aspect;
            if (name.Equals("CLEAR", StringComparison.OrdinalIgnoreCase)) return 0;
            if (name.Equals("OCCUPIED", StringComparison.OrdinalIgnoreCase)) return 1;
            if (name.Equals("OBSTRUCTED", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("JN_OBSTRUCTED", StringComparison.OrdinalIgnoreCase)) return 2;
            if (configuration is null)
                return null;
            if (configuration.SignalFunctions.TryGetValue(name, out var baseFunction))
                return NativeSignalFunctionValue(baseFunction);
            for (var index = 0; index < configuration.NormalSubtypes.Count; index++)
                if (configuration.NormalSubtypes[index].Equals(name, StringComparison.OrdinalIgnoreCase))
                    return index;
            return null;
        }

        int? ResolveNativeFunction(string name, IReadOnlyList<int> arguments)
        {
            if (name.Equals("ROUTE_SET", StringComparison.OrdinalIgnoreCase) &&
                arguments.Count == 2 && arguments[0] >= 0 && arguments[1] >= 0)
            {
                var requiredMainNode = (uint)arguments[0];
                var requiredJunctionNode = (uint)arguments[1];
                if (enabled && enabledRouteCircuits?.Contains(requiredMainNode) == true)
                    return 1;
                return selectedBranches is not null &&
                    selectedBranches.TryGetValue(requiredJunctionNode, out var selectedBranch) &&
                    selectedBranch == requiredMainNode ? 1 : 0;
            }

            if (arguments.Count == 1 &&
                (name.Equals("THIS_SIG_LR", StringComparison.OrdinalIgnoreCase) ||
                 name.Equals("THIS_SIG_MR", StringComparison.OrdinalIgnoreCase)))
                return ResolveThisSignalAspect(signal, arguments[0], name.EndsWith("_LR", StringComparison.OrdinalIgnoreCase),
                    baselineBySignal);

            if (arguments.Count == 1 &&
                (name.Equals("NEXT_SIG_LR", StringComparison.OrdinalIgnoreCase) ||
                 name.Equals("NEXT_SIG_MR", StringComparison.OrdinalIgnoreCase)))
            {
                var next = FindNextSignal(signal, arguments[0], selectedBranches);
                return next is null ? 0 : ResolveThisSignalAspect(next, arguments[0],
                    name.EndsWith("_LR", StringComparison.OrdinalIgnoreCase), baselineBySignal);
            }

            if (name.Equals("NEXT_NSIG_LR", StringComparison.OrdinalIgnoreCase) &&
                arguments.Count == 2)
            {
                if (arguments[1] <= 0) return 0;
                RouteSignalHead? next = signal;
                for (var index = 0; index < arguments[1]; index++)
                {
                    if (next is null) return 0;
                    next = FindNextSignal(next, arguments[0], selectedBranches);
                }
                return next is null ? 0 :
                    ResolveThisSignalAspect(next, arguments[0], true, baselineBySignal);
            }

            if ((name.Equals("DIST_MULTI_SIG_MR", StringComparison.OrdinalIgnoreCase) ||
                 name.Equals("DIST_MULTI_SIG_MR_OF_LR", StringComparison.OrdinalIgnoreCase)) &&
                arguments.Count == 2)
                return ResolveDistanceMultiSignalAspect(signal, arguments[0], arguments[1],
                    name.EndsWith("_OF_LR", StringComparison.OrdinalIgnoreCase),
                    selectedBranches, baselineBySignal);

            return null;
        }

        var execution = new SignalScriptInterpreter().Execute(script,
            new SignalScriptExecutionContext(
                initialState,
                signal.Definition?.DrawStates.Values.FirstOrDefault(draw =>
                    draw.Name.Equals(baseline.DrawState, StringComparison.OrdinalIgnoreCase))?.Index ?? 0,
                Enabled: enabled,
                BlockState: blockState,
                RouteSet: baseline.NextTrackNodeIndex.HasValue,
                DrawStateResolver: ResolveDrawState,
                FunctionResolver: ResolveNativeFunction,
                SymbolResolver: ResolveSymbol));
        if (!execution.Success)
            return CreateIndication(signal, RouteSignalAspect.Stop, baseline.NextTrackNodeIndex,
                $"Native SIGSCR '{scriptName}' failed safely: {execution.Diagnostic} Defaulting to Stop.");

        var nativeName = NativeAspectName(execution.State);
        var configured = nativeName is null ? null : signal.Definition?.Aspects
            .FirstOrDefault(aspect => aspect.Aspect.Equals(nativeName, StringComparison.OrdinalIgnoreCase));
        if (configured is null)
            return CreateIndication(signal, RouteSignalAspect.Stop, baseline.NextTrackNodeIndex,
                $"Native SIGSCR '{scriptName}' requested unsupported aspect value {execution.State}; defaulting to Stop.");

        var routeAspect = execution.State switch
        {
            0 or 1 or 2 => RouteSignalAspect.Stop,
            3 or 4 or 5 => RouteSignalAspect.Approach,
            6 => RouteSignalAspect.Clear1,
            7 => RouteSignalAspect.Clear2,
            _ => RouteSignalAspect.Stop
        };
        return new RouteSignalIndication(signal, routeAspect, baseline.NextTrackNodeIndex,
            $"{baseline.Reason} Native SIGSCR '{scriptName}' executed.",
            configured.Aspect,
            execution.DrawStateWasAssigned
                ? signal.Definition!.DrawStates.Values.FirstOrDefault(draw => draw.Index == execution.DrawState)?.Name ?? configured.DrawState
                : configured.DrawState,
            configured.SpeedMetersPerSecond >= 0 ? configured.SpeedMetersPerSecond : null);
    }

    private int ResolveThisSignalAspect(
        RouteSignalHead signal,
        int functionId,
        bool leastRestrictive,
        IReadOnlyDictionary<RouteSignalHead, (RouteSignalIndication Baseline, int BlockState)> baselineBySignal)
    {
        if (!blocks.TryGetValue(signal.TrackNodeIndex, out var block))
            return -1;
        var aspects = block.Signals
            .Where(candidate => candidate.TrackItemIndex == signal.TrackItemIndex &&
                candidate.Direction == signal.Direction &&
                GetSignalFunctionId(candidate) == functionId &&
                baselineBySignal.ContainsKey(candidate))
            .Select(candidate => NativeAspectValue(baselineBySignal[candidate].Baseline.NativeAspect) ?? 0)
            .ToArray();
        if (aspects.Length == 0)
            return -1;
        return leastRestrictive ? aspects.Max() : aspects.Min();
    }

    private RouteSignalHead? FindNextSignal(
        RouteSignalHead currentSignal,
        int functionId,
        IReadOnlyDictionary<uint, uint>? selectedBranches)
    {
        if (!blocks.TryGetValue(currentSignal.TrackNodeIndex, out var currentBlock))
            return null;

        var directionSign = currentSignal.Direction == 0 ? 1d : -1d;
        var sameBlock = currentBlock.Signals
            .Where(candidate => candidate.TrackItemIndex != currentSignal.TrackItemIndex &&
                candidate.Direction == currentSignal.Direction &&
                GetSignalFunctionId(candidate) == functionId &&
                (candidate.DistanceMeters - currentSignal.DistanceMeters) * directionSign > 0)
            .OrderBy(candidate => (candidate.DistanceMeters - currentSignal.DistanceMeters) * directionSign)
            .FirstOrDefault();
        if (sameBlock is not null)
            return sameBlock;

        var current = currentSignal.TrackNodeIndex;
        uint? previous = null;
        for (var visited = 0; visited < blocks.Count; visited++)
        {
            var next = ResolveNextBlock(current, visited == 0 ? currentSignal : null,
                selectedBranches, previous);
            if (next is null || !blocks.TryGetValue(next.Value, out var nextBlock))
                return null;
            var candidate = nextBlock.Signals
                .Where(item => item.Direction == currentSignal.Direction &&
                    GetSignalFunctionId(item) == functionId)
                .OrderBy(item => currentSignal.Direction == 0 ? item.DistanceMeters : -item.DistanceMeters)
                .FirstOrDefault();
            if (candidate is not null)
                return candidate;
            previous = current;
            current = next.Value;
        }
        return null;
    }

    private int ResolveDistanceMultiSignalAspect(
        RouteSignalHead currentSignal,
        int firstFunctionId,
        int endFunctionId,
        bool leastRestrictiveHead,
        IReadOnlyDictionary<uint, uint>? selectedBranches,
        IReadOnlyDictionary<RouteSignalHead, (RouteSignalIndication Baseline, int BlockState)> baselineBySignal)
    {
        var current = currentSignal;
        var foundFirst = false;
        var foundState = 7; // Native CLEAR_2 is the least restrictive starting bound.
        for (var visited = 0; visited < blocks.Count * 2; visited++)
        {
            var next = FindNextSignalOfEither(current, firstFunctionId, endFunctionId, selectedBranches);
            if (next is null)
                return 0; // Native function returns STOP if the end signal is not found.
            if (next.Value.FunctionId == endFunctionId)
                return foundFirst ? foundState : 0;
            if (next.Value.FunctionId == firstFunctionId)
            {
                var aspect = ResolveThisSignalAspect(next.Value.Signal, firstFunctionId,
                    leastRestrictiveHead, baselineBySignal);
                if (aspect >= 0)
                {
                    foundFirst = true;
                    foundState = Math.Min(foundState, aspect);
                }
                current = next.Value.Signal;
            }
        }
        return 0;
    }

    private (RouteSignalHead Signal, int FunctionId)? FindNextSignalOfEither(
        RouteSignalHead currentSignal,
        int firstFunctionId,
        int endFunctionId,
        IReadOnlyDictionary<uint, uint>? selectedBranches)
    {
        if (!blocks.TryGetValue(currentSignal.TrackNodeIndex, out var currentBlock))
            return null;

        var directionSign = currentSignal.Direction == 0 ? 1d : -1d;
        var sameBlock = currentBlock.Signals
            .Where(candidate => candidate.TrackItemIndex != currentSignal.TrackItemIndex &&
                candidate.Direction == currentSignal.Direction &&
                (GetSignalFunctionId(candidate) is int functionId) &&
                (functionId == firstFunctionId || functionId == endFunctionId) &&
                (candidate.DistanceMeters - currentSignal.DistanceMeters) * directionSign > 0)
            .OrderBy(candidate => (candidate.DistanceMeters - currentSignal.DistanceMeters) * directionSign)
            .FirstOrDefault();
        if (sameBlock is not null)
            return (sameBlock, GetSignalFunctionId(sameBlock)!.Value);

        var current = currentSignal.TrackNodeIndex;
        uint? previous = null;
        for (var visited = 0; visited < blocks.Count; visited++)
        {
            var next = ResolveNextBlock(current, visited == 0 ? currentSignal : null,
                selectedBranches, previous);
            if (next is null || !blocks.TryGetValue(next.Value, out var nextBlock))
                return null;
            var candidate = nextBlock.Signals
                .Where(item => item.Direction == currentSignal.Direction &&
                    (GetSignalFunctionId(item) is int functionId) &&
                    (functionId == firstFunctionId || functionId == endFunctionId))
                .OrderBy(item => currentSignal.Direction == 0 ? item.DistanceMeters : -item.DistanceMeters)
                .FirstOrDefault();
            if (candidate is not null)
                return (candidate, GetSignalFunctionId(candidate)!.Value);
            previous = current;
            current = next.Value;
        }
        return null;
    }

    private int? GetSignalFunctionId(RouteSignalHead signal)
    {
        var function = signal.Definition?.Function;
        if (string.IsNullOrWhiteSpace(function))
            return null;
        if (configuration is not null &&
            configuration.SignalFunctions.TryGetValue(function, out var baseFunction))
            function = baseFunction;
        return NativeSignalFunctionValue(function);
    }

    private static int? NativeSignalFunctionValue(string? name) => name?.ToUpperInvariant() switch
    {
        "NORMAL" => 0,
        "DISTANCE" => 1,
        "REPEATER" => 2,
        "SHUNTING" => 3,
        "INFO" => 4,
        "SPEED" => 5,
        "ALERT" => 6,
        "UNKNOWN" => 7,
        _ => null
    };

    private static int? NativeAspectValue(string? name) => name?.ToUpperInvariant() switch
    {
        "STOP" => 0, "STOP_AND_PROCEED" => 1, "RESTRICTING" => 2,
        "APPROACH_1" => 3, "APPROACH_2" => 4, "APPROACH_3" => 5,
        "CLEAR_1" => 6, "CLEAR_2" => 7, _ => null
    };

    private static string? NativeAspectName(int value) => value switch
    {
        0 => "STOP", 1 => "STOP_AND_PROCEED", 2 => "RESTRICTING",
        3 => "APPROACH_1", 4 => "APPROACH_2", 5 => "APPROACH_3",
        6 => "CLEAR_1", 7 => "CLEAR_2", _ => null
    };

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
        if (signal.Definition is not null && configured is null && aspect != RouteSignalAspect.Stop)
        {
            // Never display a permissive aspect that the route's actual SIGCFG
            // does not define. A missing native aspect is a restrictive failure.
            reason += $" Configured signal type '{signal.Definition.Name}' has no matching {aspect} aspect; defaulting to Stop.";
            aspect = RouteSignalAspect.Stop;
            var stopCandidates = new[] { "STOP", "STOP_AND_PROCEED", "RESTRICTING" };
            configured = signal.Definition.Aspects
                .Where(candidate => stopCandidates.Contains(candidate.Aspect, StringComparer.OrdinalIgnoreCase))
                .OrderBy(candidate => Array.IndexOf(stopCandidates, candidate.Aspect.ToUpperInvariant()))
                .FirstOrDefault();
        }
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

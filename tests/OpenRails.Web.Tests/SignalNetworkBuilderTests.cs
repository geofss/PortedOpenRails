using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class SignalNetworkBuilderTests
{
    [Fact]
    public void BuildsSignalHeadsAndJunctionAdjacencyFromTdbReferences()
    {
        var (database, network) = CreateFixture();
        var signals = new SignalNetworkBuilder().Build(database, network);

        Assert.Equal(3, signals.TrackCircuits.Count);
        var host = Assert.Single(signals.TrackCircuits.Where(block => block.TrackNodeIndex == 2));
        var signal = Assert.Single(host.Signals);
        Assert.Equal("main", signal.SignalType);
        Assert.Equal((uint)2, signal.TrackNodeIndex);
        Assert.Contains((uint)3, host.ConnectedTrackNodeIndices);
        Assert.Contains((uint)4, host.ConnectedTrackNodeIndices);
    }

    [Fact]
    public void InterlockingRuntimeUsesTheBuiltTdbJunctionTopology()
    {
        var (database, network) = CreateFixture();
        var signalNetwork = new SignalNetworkBuilder().Build(database, network);
        var interlocking = signalNetwork.CreateInterlockingController();

        Assert.True(interlocking.TryReserveRoute("AI-1", new uint[] { 2, 3 },
            new Dictionary<uint, uint> { [5] = 3 }));
        Assert.False(interlocking.TryReserveRoute("AI-2", new uint[] { 2, 4 },
            new Dictionary<uint, uint> { [5] = 4 }));
        Assert.Equal("AI-1", interlocking.Snapshot().JunctionLocks[5]);
    }

    [Fact]
    public void EvaluatesSignalAspectsFromLiveInterlockingSnapshot()
    {
        var (database, network) = CreateFixture();
        var signalNetwork = new SignalNetworkBuilder().Build(database, network);
        var interlocking = signalNetwork.CreateInterlockingController();

        Assert.True(interlocking.TryReserveRoute("PLAYER", new uint[] { 2, 3 },
            new Dictionary<uint, uint> { [5] = 3 }));
        Assert.True(interlocking.SetOccupied("PLAYER", 3, true));

        var indication = Assert.Single(signalNetwork.Evaluate(interlocking.Snapshot()));

        Assert.Equal(RouteSignalAspect.Stop, indication.Aspect);
        Assert.Contains("next track circuit is occupied", indication.Reason);
    }

    [Fact]
    public void OccupancyProducesStopAndApproachIndications()
    {
        var (database, network) = CreateFixture();
        var signals = new SignalNetworkBuilder().Build(database, network);
        var selected = new Dictionary<uint, uint> { [5] = 3 };

        var stop = Assert.Single(signals.Evaluate(new HashSet<uint> { 3 }, selected));
        Assert.Equal(RouteSignalAspect.Stop, stop.Aspect);

        var approach = Assert.Single(signals.Evaluate(new HashSet<uint> { 4 }, selected));
        Assert.Equal(RouteSignalAspect.Approach, approach.Aspect);
    }

    [Fact]
    public void UnselectedJunctionRouteDefaultsToStop()
    {
        var (database, network) = CreateFixture();
        var signals = new SignalNetworkBuilder().Build(database, network);

        var indication = Assert.Single(signals.Evaluate(new HashSet<uint>()));

        Assert.Equal(RouteSignalAspect.Stop, indication.Aspect);
        Assert.Contains("No unique", indication.Reason);
    }

    [Fact]
    public void ConflictingSelectionsAcrossSignalControlledJunctionsFailRestrictive()
    {
        var signal = new RouteSignalHead(
            7, "main", string.Empty, 0, 0, 2, 10,
            new[]
            {
                new TrackSignalDirection(5, 1, 3, 1),
                new TrackSignalDirection(6, 1, 3, 1)
            });
        var circuits = new[]
        {
            new SignalTrackCircuit(2, 100, new uint[] { 3, 4 }, new[] { signal }),
            new SignalTrackCircuit(3, 100, new uint[] { 2 }, Array.Empty<RouteSignalHead>()),
            new SignalTrackCircuit(4, 100, new uint[] { 2 }, Array.Empty<RouteSignalHead>())
        };
        var junctions = new Dictionary<uint, IReadOnlyList<uint>>
        {
            [5] = new uint[] { 3, 4 },
            [6] = new uint[] { 3, 4 }
        };
        var network = new SignalNetwork(circuits, junctions, Array.Empty<string>());

        var indication = Assert.Single(network.Evaluate(
            new HashSet<uint>(),
            new Dictionary<uint, uint> { [5] = 3, [6] = 4 }));

        Assert.Equal(RouteSignalAspect.Stop, indication.Aspect);
        Assert.Contains("No unique", indication.Reason);
    }

    [Fact]
    public void MapsEvaluatedBlockStateToConfiguredNativeAspectAndDrawState()
    {
        var definition = new SignalTypeDefinition("main", "", "NORMAL", "", "", 2,
            new HashSet<string>(),
            Array.Empty<SignalLightDefinition>(),
            new Dictionary<string, SignalDrawStateDefinition>
            {
                ["clear"] = new(0, "clear", Array.Empty<SignalDrawLightDefinition>(), 0)
            },
            new[] { new SignalAspectDefinition("CLEAR_2", "clear", 22.2f, false, false, false) });
        var signal = new RouteSignalHead(7, "main", "", 0, 0, 2, 10,
            Array.Empty<TrackSignalDirection>(), definition);
        var network = new SignalNetwork(
            new[]
            {
                new SignalTrackCircuit(2, 100, new uint[] { 3 }, new[] { signal }),
                new SignalTrackCircuit(3, 100, new uint[] { 2, 4 }, Array.Empty<RouteSignalHead>()),
                new SignalTrackCircuit(4, 100, new uint[] { 3 }, Array.Empty<RouteSignalHead>())
            },
            new Dictionary<uint, IReadOnlyList<uint>>(),
            Array.Empty<string>());

        var indication = Assert.Single(network.Evaluate(new HashSet<uint>()));

        Assert.Equal(RouteSignalAspect.Clear2, indication.Aspect);
        Assert.Equal("CLEAR_2", indication.NativeAspect);
        Assert.Equal("clear", indication.DrawState);
        Assert.Equal(22.2f, indication.SpeedLimitMetersPerSecond);
    }

    [Fact]
    public void UsesConfiguredSingleBlockClearAheadAndClearOneAspect()
    {
        var definition = new SignalTypeDefinition("main", "", "NORMAL", "", "", 1,
            new HashSet<string>(),
            Array.Empty<SignalLightDefinition>(),
            new Dictionary<string, SignalDrawStateDefinition>
            {
                ["clearone"] = new(0, "clearone", Array.Empty<SignalDrawLightDefinition>(), 0)
            },
            new[] { new SignalAspectDefinition("CLEAR_1", "clearone", 13.9f, false, false, false) });
        var signal = new RouteSignalHead(8, "main", "", 0, 0, 2, 0,
            Array.Empty<TrackSignalDirection>(), definition);
        var network = new SignalNetwork(
            new[]
            {
                new SignalTrackCircuit(2, 100, new uint[] { 3 }, new[] { signal }),
                new SignalTrackCircuit(3, 100, new uint[] { 2 }, Array.Empty<RouteSignalHead>())
            },
            new Dictionary<uint, IReadOnlyList<uint>>(),
            Array.Empty<string>());

        var indication = Assert.Single(network.Evaluate(new HashSet<uint>()));

        Assert.Equal(RouteSignalAspect.Clear1, indication.Aspect);
        Assert.Equal("CLEAR_1", indication.NativeAspect);
        Assert.Equal("clearone", indication.DrawState);
        Assert.Equal(13.9f, indication.SpeedLimitMetersPerSecond);
    }

    [Fact]
    public void MissingConfiguredClearAspectFailsRestrictiveInsteadOfShowingGreen()
    {
        var definition = new SignalTypeDefinition("main", "", "NORMAL", "", "", 1,
            new HashSet<string>(), Array.Empty<SignalLightDefinition>(),
            new Dictionary<string, SignalDrawStateDefinition>(),
            new[] { new SignalAspectDefinition("STOP", "", -1, false, false, false) });
        var signal = new RouteSignalHead(10, "main", "", 0, 0, 2, 0,
            Array.Empty<TrackSignalDirection>(), definition);
        var network = new SignalNetwork(
            new[]
            {
                new SignalTrackCircuit(2, 100, new uint[] { 3 }, new[] { signal }),
                new SignalTrackCircuit(3, 100, new uint[] { 2 }, Array.Empty<RouteSignalHead>())
            },
            new Dictionary<uint, IReadOnlyList<uint>>(),
            Array.Empty<string>());

        var indication = Assert.Single(network.Evaluate(new HashSet<uint>()));

        Assert.Equal(RouteSignalAspect.Stop, indication.Aspect);
        Assert.Contains("defaulting to Stop", indication.Reason);
        Assert.Equal("STOP", indication.NativeAspect);
    }

    [Fact]
    public void RequiresSelectedBranchAtEveryJunctionEvenWithoutSignalDirectionOverride()
    {
        var definition = new SignalTypeDefinition("main", "", "NORMAL", "", "", 1,
            new HashSet<string>(), Array.Empty<SignalLightDefinition>(),
            new Dictionary<string, SignalDrawStateDefinition>(),
            new[] { new SignalAspectDefinition("CLEAR_1", "", 10, false, false, false) });
        var signal = new RouteSignalHead(9, "main", "", 0, 0, 2, 0,
            Array.Empty<TrackSignalDirection>(), definition);
        var network = new SignalNetwork(
            new[]
            {
                new SignalTrackCircuit(2, 100, new uint[] { 3, 4 }, new[] { signal }, new uint[] { 5 }),
                new SignalTrackCircuit(3, 100, new uint[] { 2 }, Array.Empty<RouteSignalHead>()),
                new SignalTrackCircuit(4, 100, new uint[] { 2 }, Array.Empty<RouteSignalHead>())
            },
            new Dictionary<uint, IReadOnlyList<uint>> { [5] = new uint[] { 2, 3, 4 } },
            Array.Empty<string>());

        var noSelection = Assert.Single(network.Evaluate(new HashSet<uint>()));
        var selected = Assert.Single(network.Evaluate(new HashSet<uint>(),
            new Dictionary<uint, uint> { [5] = 3 }));

        Assert.Equal(RouteSignalAspect.Stop, noSelection.Aspect);
        Assert.Equal(RouteSignalAspect.Clear1, selected.Aspect);
        Assert.Equal((uint)3, selected.NextTrackNodeIndex);
    }

    [Fact]
    public void ExecutesConfiguredNativeScriptAndAppliesItsAspectAndDrawState()
    {
        var definition = new SignalTypeDefinition("main", "native-main", "NORMAL", "", "", 1,
            new HashSet<string>(), Array.Empty<SignalLightDefinition>(),
            new Dictionary<string, SignalDrawStateDefinition>
            {
                ["script-clear"] = new(0, "script-clear", Array.Empty<SignalDrawLightDefinition>(), 0),
                ["baseline-clear"] = new(1, "baseline-clear", Array.Empty<SignalDrawLightDefinition>(), 0)
            },
            new[]
            {
                new SignalAspectDefinition("STOP", "", -1, false, false, false),
                new SignalAspectDefinition("CLEAR_1", "baseline-clear", 12, false, false, false)
            });
        var signal = new RouteSignalHead(11, "main", "", 0, 0, 2, 0,
            Array.Empty<TrackSignalDirection>(), definition);
        var network = new SignalNetwork(
            new[]
            {
                new SignalTrackCircuit(2, 100, new uint[] { 3 }, new[] { signal }),
                new SignalTrackCircuit(3, 100, new uint[] { 2 }, Array.Empty<RouteSignalHead>())
            },
            new Dictionary<uint, IReadOnlyList<uint>>(),
            Array.Empty<string>());
        var script = new SignalScriptDefinition("native-main",
            new[] { "EXTERN FLOAT STATE;", "EXTERN FLOAT DRAW_STATE;", "STATE = 6;", "DRAW_STATE = 0;" },
            Array.Empty<string>(), new[] { "STATE", "DRAW_STATE" }, 2, Array.Empty<string>());
        var library = new SignalScriptLibrary(
            new Dictionary<string, SignalScriptDefinition> { ["native-main"] = script },
            Array.Empty<string>());

        var indication = Assert.Single(network.Evaluate(new HashSet<uint>(), scripts: library));

        Assert.Equal(RouteSignalAspect.Clear1, indication.Aspect);
        Assert.Equal("CLEAR_1", indication.NativeAspect);
        Assert.Equal("script-clear", indication.DrawState);
        Assert.Contains("Native SIGSCR 'native-main' executed", indication.Reason);
    }

    [Fact]
    public void NativeScriptFailureForcesStopInsteadOfKeepingPermissiveBaseline()
    {
        var definition = new SignalTypeDefinition("main", "unsafe-script", "NORMAL", "", "", 1,
            new HashSet<string>(), Array.Empty<SignalLightDefinition>(),
            new Dictionary<string, SignalDrawStateDefinition>(),
            new[]
            {
                new SignalAspectDefinition("STOP", "", -1, false, false, false),
                new SignalAspectDefinition("CLEAR_1", "", 12, false, false, false)
            });
        var signal = new RouteSignalHead(12, "main", "", 0, 0, 2, 0,
            Array.Empty<TrackSignalDirection>(), definition);
        var network = new SignalNetwork(
            new[]
            {
                new SignalTrackCircuit(2, 100, new uint[] { 3 }, new[] { signal }),
                new SignalTrackCircuit(3, 100, new uint[] { 2 }, Array.Empty<RouteSignalHead>())
            },
            new Dictionary<uint, IReadOnlyList<uint>>(),
            Array.Empty<string>());
        var script = new SignalScriptDefinition("unsafe-script",
            new[] { "EXTERN FLOAT STATE;", "STATE = NEXT_SIG_LR();" },
            Array.Empty<string>(), new[] { "STATE" }, 1, Array.Empty<string>());
        var library = new SignalScriptLibrary(
            new Dictionary<string, SignalScriptDefinition> { ["unsafe-script"] = script },
            Array.Empty<string>());

        var indication = Assert.Single(network.Evaluate(new HashSet<uint>(), scripts: library));

        Assert.Equal(RouteSignalAspect.Stop, indication.Aspect);
        Assert.Contains("failed safely", indication.Reason);
    }

    private static (TrackDatabase Database, TrackNetwork Network) CreateFixture()
    {
        var nodes = new TrackNode?[]
        {
            null,
            new TrackNode(1, TrackNodeKind.End, null, null,
                Array.Empty<TrackVectorSection>(), Array.Empty<TrackPin>()),
            new TrackNode(2, TrackNodeKind.Vector, null, null,
                Array.Empty<TrackVectorSection>(), new[] { new TrackPin(5, 0) }, new[] { 0 }),
            new TrackNode(3, TrackNodeKind.Vector, null, null,
                Array.Empty<TrackVectorSection>(), new[] { new TrackPin(5, 0) }),
            new TrackNode(4, TrackNodeKind.Vector, null, null,
                Array.Empty<TrackVectorSection>(), new[] { new TrackPin(5, 0) }),
            new TrackNode(5, TrackNodeKind.Junction, null,
                new TrackJunctionData("switch", 1), Array.Empty<TrackVectorSection>(),
                new[] { new TrackPin(2, 0), new TrackPin(3, 1), new TrackPin(4, 1) })
        };

        var item = new TrackItem(
            0, "signal", 0, 0, 0, 0, 0, 0, 0, 0, 0, 15,
            string.Empty, string.Empty, string.Empty, string.Empty,
            null, null, null,
            SignalType: "main",
            SignalDirections: new[] { new TrackSignalDirection(5, 1, 3, 1) });

        var database = new TrackDatabase(nodes, new TrackItem?[] { item }, Array.Empty<string>());
        var paths = new[]
        {
            new TrackVectorPath(2, new[] { 5 }, Array.Empty<TrackSectionPlacement>(), 100),
            new TrackVectorPath(3, new[] { 5 }, Array.Empty<TrackSectionPlacement>(), 100),
            new TrackVectorPath(4, new[] { 5 }, Array.Empty<TrackSectionPlacement>(), 100)
        };
        return (database, new TrackNetwork(database, paths, Array.Empty<string>()));
    }
}

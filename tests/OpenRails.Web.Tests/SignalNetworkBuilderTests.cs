using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class SignalNetworkBuilderTests
{
    [Fact]
    public void BuildsSignalHeadsAndTrackCircuitAdjacencyFromTdbReferences()
    {
        var database = CreateDatabase();
        var network = CreateNetwork(database);
        var signals = new SignalNetworkBuilder().Build(database, network);

        Assert.Equal(3, signals.TrackCircuits.Count);
        var signal = Assert.Single(signals.TrackCircuits.Single(block => block.TrackNodeIndex == 2).Signals);
        Assert.Equal("main", signal.SignalType);
        Assert.Equal(2, signal.TrackNodeIndex);
        Assert.Contains((uint)3, signals.TrackCircuits.Single(block => block.TrackNodeIndex == 2).ConnectedTrackNodeIndices);
    }

    [Fact]
    public void OccupiedNextBlockForcesStopAndOccupiedSecondBlockGivesApproach()
    {
        var signals = new SignalNetworkBuilder().Build(CreateDatabase(), CreateNetwork(CreateDatabase()));
        var signal = signals.TrackCircuits.SelectMany(block => block.Signals).Single();

        var stop = Assert.Single(signals.Evaluate(new HashSet<uint> { 3 }));
        Assert.Equal(RouteSignalAspect.Stop, stop.Aspect);
        Assert.Equal("The next track circuit is occupied.", stop.Reason);

        var approach = Assert.Single(signals.Evaluate(new HashSet<uint> { 4 }, selected));
        Assert.Equal(RouteSignalAspect.Approach, approach.Aspect);
    }

    [Fact]
    public void AmbiguousJunctionWithoutSelectedBranchRemainsAtStop()
    {
        var database = CreateDatabase();
        var network = CreateNetwork(database, branch: true);
        var signals = new SignalNetworkBuilder().Build(database, network);

        var indication = Assert.Single(signals.Evaluate(new HashSet<uint>()));
        Assert.Equal(RouteSignalAspect.Stop, indication.Aspect);
    }

    private static TrackDatabase CreateDatabase()
    {
        var nodes = new TrackNode?[7];
        nodes[1] = new TrackNode(1, TrackNodeKind.End, null, null, Array.Empty<TrackVectorSection>(),
            new[] { new TrackPin(2, 0) });
        nodes[2] = new TrackNode(2, TrackNodeKind.Vector, null, null, Array.Empty<TrackVectorSection>(),
            new[] { new TrackPin(1, 0), new TrackPin(5, 0) }, new[] { 0 });
        nodes[3] = new TrackNode(3, TrackNodeKind.Vector, null, null, Array.Empty<TrackVectorSection>(),
            new[] { new TrackPin(5, 0), new TrackPin(6, 0) });
        nodes[4] = new TrackNode(4, TrackNodeKind.Vector, null, null, Array.Empty<TrackVectorSection>(),
            new[] { new TrackPin(5, 0), new TrackPin(6, 0) });
        nodes[5] = new TrackNode(5, TrackNodeKind.Junction, null, new TrackJunctionData("switch", 1),
            Array.Empty<TrackVectorSection>(), new[] { new TrackPin(2, 0), new TrackPin(3, 0), new TrackPin(4, 0) });
        nodes[6] = new TrackNode(6, TrackNodeKind.End, null, null, Array.Empty<TrackVectorSection>(),
            new[] { new TrackPin(3, 0), new TrackPin(4, 0) });
        var item = new TrackItem(0, "signal", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "", "", "", "",
            null, null, null, SignalType: "main", SignalDirections: new[] { new TrackSignalDirection(5, 1, 3, 1) });
        return new TrackDatabase(nodes, new TrackItem?[] { item }, Array.Empty<string>());
    }

    private static TrackNetwork CreateNetwork(TrackDatabase database, bool branch = false)
    {
        var paths = new[]
        {
            new TrackVectorPath(2, new[] { 1, 5 }, Array.Empty<TrackSectionPlacement>(), 100),
            new TrackVectorPath(3, new[] { 5, 6 }, Array.Empty<TrackSectionPlacement>(), 100),
            new TrackVectorPath(4, new[] { 5, 6 }, Array.Empty<TrackSectionPlacement>(), 100)
        };
        return new TrackNetwork(database, paths, Array.Empty<string>());
    }
}

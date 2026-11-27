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

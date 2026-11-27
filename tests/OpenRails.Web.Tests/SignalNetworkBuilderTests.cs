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
        var database = CreateDatabase();
        var signals = new SignalNetworkBuilder().Build(database, CreateNetwork(database));
        var selected = new Dictionary<uint, uint> { [5] = 3 };

        var stop = Assert.Single(signals.Evaluate(new HashSet<uint> { 3 }, selected));
        Assert.Equal(RouteSignalAspect.Stop, stop.Aspect);
        Assert.Equal("The next track circuit is occupied.", stop.Reason);

        var approach = Assert.Single(signals.Evaluate(new HashSet<uint> { 4 }, selected));
        Assert.Equal(RouteSignalAspect.Approach, approach.Aspect);
    }


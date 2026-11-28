using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class AiServiceInterlockingSessionTests
{
    private static SignalInterlockingController CreateInterlocking() => new(
        new uint[] { 10, 11, 12 },
        new Dictionary<uint, IReadOnlyCollection<uint>> { [100] = new uint[] { 11, 12 } });

    private static BrowserTrainDrivingController CreateTrain()
    {
        var consist = new ConsistMetadata("AI-Interlocked", new[]
        {
            new ConsistCar("Engine", "Diesel", 1, true, false, false)
        }, "ai-interlocked.con");
        var traction = new LocomotiveTractionMetadata(
            "AI-Interlocked-Loco", 1500, 1500, 100000, 100, 0, null, "ai-interlocked.eng");
        var asset = new ConsistCarAssetReference("AI-Interlocked", 1, "Engine", "Diesel", true,
            false, "ai-interlocked.eng", true, null,
            new RollingStockMetadata("Engine", "Diesel", 40000, 3, 4, 20, 4, 8,
                "air_single_pipe", "distributor", 100000, "ai-interlocked.eng"),
            "ai-interlocked.s", true, null, traction);
        return new BrowserTrainDrivingController(
            new TrainConsistSimulationSetupBuilder().Build(consist, new[] { asset }, 200));
    }

    [Fact]
    public void AITrainCannotMoveBeforeItsWholeRouteIsReserved()
    {
        var session = new AiServiceInterlockingSession("AI-1", CreateInterlocking());
        var train = CreateTrain();
        var route = new ServiceRouteStartPosition(0, 0,
            new TrackWorldPosition(0, 0, 0), new[] { 0d, 100d });

        var decision = session.Drive(train, route, 15);

        Assert.False(session.IsDispatched);
        Assert.Equal(0, decision.ThrottlePercent);
        Assert.Equal(100, decision.BrakePercent);
        Assert.Equal(0, train.ThrottlePercent);
        Assert.Equal(100, train.BrakePercent);
    }

    [Fact]
    public void LiveAIServiceAppliesNearestTrainFollowingConstraint()
    {
        var interlocking = CreateInterlocking();
        var session = new AiServiceInterlockingSession("AI-1", interlocking);
        Assert.True(session.TryDispatch(new uint[] { 10, 11 }, new Dictionary<uint, uint> { [100] = 11 }));
        var train = CreateTrain();
        var route = new ServiceRouteStartPosition(0, 0,
            new TrackWorldPosition(0, 0, 0), new[] { 0d, 500d }, 1);

        var free = session.Drive(train, route, 25);
        var following = session.Drive(train, route, 25, otherTrains: new[]
        {
            new AiServiceTrainState("PLAYER", 25, 0),
            new AiServiceTrainState("AI-2", 100, 10)
        });

        Assert.True(following.TargetSpeedMetersPerSecond < free.TargetSpeedMetersPerSecond);
        Assert.Equal(0, following.ThrottlePercent);
        Assert.Equal(100, following.BrakePercent);
    }

    [Fact]
    public void AITrainOccupancyKeepsSwitchLockedUntilRouteIsClear()
    {
        var interlocking = CreateInterlocking();
        var session = new AiServiceInterlockingSession("AI-1", interlocking);
        Assert.True(session.TryDispatch(new uint[] { 10, 11 }, new Dictionary<uint, uint> { [100] = 11 }));
        Assert.True(session.SetCircuitOccupancy(10, true));

        Assert.False(session.TryComplete());
        Assert.False(interlocking.TrySetJunctionBranch(100, 12));

        Assert.True(session.SetCircuitOccupancy(10, false));
        Assert.True(session.TryComplete());
        Assert.False(session.IsDispatched);
        Assert.True(interlocking.TrySetJunctionBranch(100, 12));
    }

    [Fact]
    public void ActiveAIServiceCanAtomicallyReplanWithoutDroppingOccupiedCircuit()
    {
        var interlocking = CreateInterlocking();
        var session = new AiServiceInterlockingSession("AI-1", interlocking);
        Assert.True(session.TryDispatch(new uint[] { 10, 11 }, new Dictionary<uint, uint> { [100] = 11 }));
        Assert.True(session.SetCircuitOccupancy(10, true));

        Assert.False(session.TryChangeRoute(new uint[] { 11, 12 },
            new Dictionary<uint, uint> { [100] = 12 }));
        Assert.True(session.TryChangeRoute(new uint[] { 10, 12 },
            new Dictionary<uint, uint> { [100] = 12 }));

        var state = interlocking.Snapshot();
        Assert.Equal("AI-1", state.OccupiedCircuits[10]);
        Assert.Equal("AI-1", state.ReservedCircuits[12]);
        Assert.DoesNotContain(11u, state.ReservedCircuits.Keys);
        Assert.True(session.IsDispatched);
    }

    [Fact]
    public void ConflictingAIServiceCannotAcquireSameJunctionRoute()
    {
        var interlocking = CreateInterlocking();
        var first = new AiServiceInterlockingSession("AI-1", interlocking);
        var second = new AiServiceInterlockingSession("AI-2", interlocking);

        Assert.True(first.TryDispatch(new uint[] { 10, 11 }, new Dictionary<uint, uint> { [100] = 11 }));
        Assert.False(second.TryDispatch(new uint[] { 12 }, new Dictionary<uint, uint> { [100] = 12 }));
        Assert.False(second.IsDispatched);
    }
}

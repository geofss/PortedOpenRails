using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class SignalInterlockingControllerTests
{
    private static SignalInterlockingController Create() => new(
        new uint[] { 10, 11, 12, 13 },
        new Dictionary<uint, IReadOnlyCollection<uint>>
        {
            [100] = new uint[] { 11, 12 }
        });

    [Fact]
    public void ReservesWholeRouteAndLocksSelectedJunctionAtomically()
    {
        var interlocking = Create();
        Assert.True(interlocking.TrySetJunctionBranch(100, 11));
        Assert.True(interlocking.TryReserveRoute("AI-1", new uint[] { 10, 11, 13 },
            new Dictionary<uint, uint> { [100] = 11 }));

        var state = interlocking.Snapshot();
        Assert.Equal("AI-1", state.ReservedCircuits[10]);
        Assert.Equal("AI-1", state.ReservedCircuits[11]);
        Assert.Equal("AI-1", state.JunctionLocks[100]);
        Assert.Equal((uint)11, state.SelectedJunctionBranches[100]);
    }

    [Fact]
    public void ConflictingReservationDoesNotPartiallyReserveRoute()
    {
        var interlocking = Create();
        Assert.True(interlocking.TryReserveRoute("AI-1", new uint[] { 10, 11 },
            new Dictionary<uint, uint> { [100] = 11 }));

        Assert.False(interlocking.TryReserveRoute("AI-2", new uint[] { 12, 13 },
            new Dictionary<uint, uint> { [100] = 12 }));

        var state = interlocking.Snapshot();
        Assert.DoesNotContain(12u, state.ReservedCircuits.Keys);
        Assert.DoesNotContain(13u, state.ReservedCircuits.Keys);
        Assert.Equal("AI-1", state.JunctionLocks[100]);
    }

    [Fact]
    public void OccupancyBlocksOtherTrainButAllowsOwningTrain()
    {
        var interlocking = Create();
        Assert.True(interlocking.SetOccupied("PLAYER", 12, true));
        Assert.False(interlocking.TryReserveRoute("AI-1", new uint[] { 12 },
            new Dictionary<uint, uint>()));
        Assert.True(interlocking.TryReserveRoute("PLAYER", new uint[] { 12 },
            new Dictionary<uint, uint>()));
    }

    [Fact]
    public void LockedJunctionCannotBeThrownByAnotherTrain()
    {
        var interlocking = Create();
        Assert.True(interlocking.TryReserveRoute("AI-1", new uint[] { 11 },
            new Dictionary<uint, uint> { [100] = 11 }));

        Assert.False(interlocking.TrySetJunctionBranch(100, 12));
        Assert.False(interlocking.TryReserveRoute("AI-2", new uint[] { 12 },
            new Dictionary<uint, uint> { [100] = 12 }));
    }

    [Fact]
    public void ReleasingRouteUnlocksSwitchAndAllowsNewBranch()
    {
        var interlocking = Create();
        Assert.True(interlocking.TryReserveRoute("AI-1", new uint[] { 11 },
            new Dictionary<uint, uint> { [100] = 11 }));

        interlocking.ReleaseRoute("AI-1");

        Assert.True(interlocking.TrySetJunctionBranch(100, 12));
        Assert.True(interlocking.TryReserveRoute("AI-2", new uint[] { 12 },
            new Dictionary<uint, uint> { [100] = 12 }));
    }

    [Fact]
    public void CannotClearAnotherTrainsOccupancy()
    {
        var interlocking = Create();
        Assert.True(interlocking.SetOccupied("PLAYER", 10, true));
        Assert.False(interlocking.SetOccupied("AI-1", 10, false));
        Assert.Equal("PLAYER", interlocking.Snapshot().OccupiedCircuits[10]);
    }

    [Fact]
    public void ReplacesWholeTrainOccupancyAtomicallyIncludingTrailingCircuit()
    {
        var interlocking = Create();
        Assert.True(interlocking.SetOccupiedCircuits("PLAYER", new HashSet<uint> { 10, 11 }));
        Assert.False(interlocking.SetOccupiedCircuits("AI-1", new HashSet<uint> { 12, 11 }));

        var afterConflict = interlocking.Snapshot();
        Assert.Equal("PLAYER", afterConflict.OccupiedCircuits[10]);
        Assert.Equal("PLAYER", afterConflict.OccupiedCircuits[11]);
        Assert.DoesNotContain(12u, afterConflict.OccupiedCircuits.Keys);

        Assert.True(interlocking.SetOccupiedCircuits("PLAYER", new HashSet<uint> { 11, 12 }));
        var updated = interlocking.Snapshot();
        Assert.DoesNotContain(10u, updated.OccupiedCircuits.Keys);
        Assert.Equal("PLAYER", updated.OccupiedCircuits[11]);
        Assert.Equal("PLAYER", updated.OccupiedCircuits[12]);
    }

    [Fact]
    public void DetectsTwoTrainWaitForCycleWithoutReleasingEitherRoute()
    {
        var interlocking = Create();
        Assert.True(interlocking.SetOccupied("AI-1", 10, true));
        Assert.True(interlocking.SetOccupied("AI-2", 11, true));

        Assert.False(interlocking.TryReserveRoute("AI-1", new uint[] { 11 },
            new Dictionary<uint, uint>()));
        Assert.Empty(interlocking.GetDeadlockedTrainIds());

        Assert.False(interlocking.TryReserveRoute("AI-2", new uint[] { 10 },
            new Dictionary<uint, uint>()));
        Assert.Equal(new HashSet<string> { "AI-1", "AI-2" }, interlocking.GetDeadlockedTrainIds());
        Assert.Equal("AI-1", interlocking.Snapshot().OccupiedCircuits[10]);
        Assert.Equal("AI-2", interlocking.Snapshot().OccupiedCircuits[11]);
    }

    [Fact]
    public void RejectsInvalidCircuitTopology()
    {
        Assert.Throws<ArgumentException>(() => new SignalInterlockingController(
            new uint[] { 10 },
            new Dictionary<uint, IReadOnlyCollection<uint>> { [100] = new uint[] { 10 } }));
    }
    [Fact]
    public void RouteReleaseIsDeferredWhileTrainStillOccupiesItsReservedRoute()
    {
        var interlocking = Create();
        Assert.True(interlocking.TryReserveRoute("AI-1", new uint[] { 10, 11 },
            new Dictionary<uint, uint> { [100] = 11 }));
        Assert.True(interlocking.SetOccupied("AI-1", 10, true));

        Assert.False(interlocking.TryReleaseRoute("AI-1"));
        var held = interlocking.Snapshot();
        Assert.Equal("AI-1", held.ReservedCircuits[10]);
        Assert.Equal("AI-1", held.JunctionLocks[100]);
        Assert.False(interlocking.TrySetJunctionBranch(100, 12));

        Assert.True(interlocking.SetOccupied("AI-1", 10, false));
        Assert.True(interlocking.TryReleaseRoute("AI-1"));
        Assert.Empty(interlocking.Snapshot().ReservedCircuits);
        Assert.Empty(interlocking.Snapshot().JunctionLocks);
}
}

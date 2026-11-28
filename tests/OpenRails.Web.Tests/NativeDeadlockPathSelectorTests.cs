using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class NativeDeadlockPathSelectorTests
{
    private readonly NativeDeadlockPathSelector selector = new();

    [Fact]
    public void PrefersTrainOwnPathWhenItFits()
    {
        var own = new NativeDeadlockPathCandidate(2, "PASS01", true, 220);
        var selected = selector.SelectPath(new[]
        {
            new NativeDeadlockPathCandidate(1, "PASS02", true, 230),
            own,
            new NativeDeadlockPathCandidate(0, "MAIN", true, 200)
        }, own, deadlockActive: false);

        Assert.Equal(2, selected.PathIndex);
        Assert.Equal("PASS01", selected.PathName);
        Assert.True(selected.TrainFits);
        Assert.Equal(220, selected.EndSectionIndex);
    }

    [Fact]
    public void PrefersMainPathWhenNoDeadlockIsActiveAndNoPathFits()
    {
        var own = new NativeDeadlockPathCandidate(1, "PASS01", false, 220);
        var selected = selector.SelectPath(new[]
        {
            own,
            new NativeDeadlockPathCandidate(0, "MAIN", false, 200),
            new NativeDeadlockPathCandidate(2, "PASS02", false, 240)
        }, own, deadlockActive: false);

        Assert.Equal(0, selected.PathIndex);
        Assert.Equal("MAIN", selected.PathName);
        Assert.False(selected.TrainFits);
    }

    [Fact]
    public void AvoidsMainPathPreferenceWhenDeadlockIsAlreadyActive()
    {
        var own = new NativeDeadlockPathCandidate(1, "PASS01", false, 220);
        var selected = selector.SelectPath(new[]
        {
            own,
            new NativeDeadlockPathCandidate(0, "MAIN", false, 200),
            new NativeDeadlockPathCandidate(2, "PASS02", false, 240)
        }, own, deadlockActive: true);

        Assert.Equal(2, selected.PathIndex);
        Assert.Equal("PASS02", selected.PathName);
    }

    [Fact]
    public void FallsBackToOwnPathWhenNoPassingRouteIsAvailable()
    {
        var own = new NativeDeadlockPathCandidate(7, "OWN", false, 700);

        var selected = selector.SelectPath(Array.Empty<NativeDeadlockPathCandidate>(),
            own, deadlockActive: false);

        Assert.Equal(7, selected.PathIndex);
        Assert.Equal(700, selected.EndSectionIndex);
    }

    [Fact]
    public void AIInterlockingDispatchUsesTheNativeSelectedPassingPath()
    {
        var interlocking = new SignalInterlockingController(new uint[] { 10, 11, 12 },
            new Dictionary<uint, IReadOnlyCollection<uint>>());
        var session = new AiServiceInterlockingSession("AI-DEADLOCK", interlocking);
        var own = new AiDeadlockRouteOption(
            new NativeDeadlockPathCandidate(1, "PASS01", false, 101),
            new uint[] { 10 }, new Dictionary<uint, uint>());
        var main = new AiDeadlockRouteOption(
            new NativeDeadlockPathCandidate(0, "MAIN", false, 100),
            new uint[] { 11 }, new Dictionary<uint, uint>());
        var alternative = new AiDeadlockRouteOption(
            new NativeDeadlockPathCandidate(2, "PASS02", false, 102),
            new uint[] { 12 }, new Dictionary<uint, uint>());

        Assert.True(session.TryDispatchWithDeadlockPath(
            new[] { own, main, alternative }, own, deadlockActive: false));

        Assert.Equal(0, session.SelectedDeadlockPath?.PathIndex);
        Assert.Equal("AI-DEADLOCK", interlocking.Snapshot().ReservedCircuits[11]);
        Assert.DoesNotContain(10u, interlocking.Snapshot().ReservedCircuits.Keys);
        Assert.True(session.TryComplete());
        Assert.Null(session.SelectedDeadlockPath);
    }

    [Fact]
    public void RejectsDuplicateAvailablePathIndexes()
    {
        var own = new NativeDeadlockPathCandidate(1, "OWN", true, 10);

        Assert.Throws<ArgumentException>(() => selector.SelectPath(new[]
        {
            own,
            new NativeDeadlockPathCandidate(1, "MAIN", true, 20)
        }, own, deadlockActive: false));
    }
}

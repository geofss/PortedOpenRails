using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class ActivityStationStopTrackerTests
{
    [Fact]
    public void CreditsWaitingPointOnlyAfterTrainStopsWithinTileAndRadius()
    {
        var path = new RoutePathDefinition("route/path.pat", "Main", "Main", "A", "B", 0,
            new[] { new RoutePathPoint(1, 2, 10, 0, 20, 1, 1), new RoutePathPoint(1, 2, 100, 0, 200, 1, 1) },
            new[] { new RoutePathNode(0, 1, uint.MaxValue, 0), new RoutePathNode(2, uint.MaxValue, uint.MaxValue, 1) },
            Array.Empty<string>());
        var tracker = new ActivityStationStopTracker(path, 25);
        Assert.False(tracker.Advance(1, 2, 100, 200, 1.5).StationStopReached);
        var stopped = tracker.Advance(1, 2, 110, 205, 0);
        Assert.True(stopped.StationStopReached);
        Assert.True(stopped.AllStopsCompleted);
        Assert.False(tracker.Advance(1, 2, 100, 200, 0).StationStopReached);
    }

    [Fact]
    public void AllStopsRequiresEveryWaitingPointToBeVisited()
    {
        var path = new RoutePathDefinition("route/path.pat", "Main", "Main", "A", "C", 0,
            new[]
            {
                new RoutePathPoint(1, 2, 100, 0, 200, 1, 1),
                new RoutePathPoint(1, 2, 300, 0, 400, 1, 1),
                new RoutePathPoint(1, 2, 500, 0, 600, 1, 1)
            },
            new[]
            {
                new RoutePathNode(0, 1, uint.MaxValue, 0),
                new RoutePathNode(2, 2, uint.MaxValue, 1),
                new RoutePathNode(2, uint.MaxValue, uint.MaxValue, 2)
            },
            Array.Empty<string>());
        var tracker = new ActivityStationStopTracker(path, 15);

        var first = tracker.Advance(1, 2, 300, 400, 0);
        Assert.True(first.StationStopReached);
        Assert.Equal(1, first.VisitedStops);
        Assert.False(first.AllStopsCompleted);

        var second = tracker.Advance(1, 2, 500, 600, 0);
        Assert.True(second.StationStopReached);
        Assert.Equal(2, second.VisitedStops);
        Assert.True(second.AllStopsCompleted);
    }

    [Fact]
    public void BrokenPathTraceCannotCompleteAllStops()
    {
        var path = new RoutePathDefinition("route/path.pat", "Broken", "Broken", "A", "B", 0,
            new[] { new RoutePathPoint(1, 2, 100, 0, 200, 1, 1) },
            new[] { new RoutePathNode(2, 9, uint.MaxValue, 0) },
            Array.Empty<string>());
        var tracker = new ActivityStationStopTracker(path);

        var progress = tracker.Advance(1, 2, 100, 200, 0);

        Assert.False(progress.PathTraceComplete);
        Assert.False(progress.AllStopsCompleted);
    }

    [Fact]
    public void PathWithoutWaitingPointsDoesNotCompleteAllStopsImmediately()
    {
        var path = new RoutePathDefinition("route/path.pat", "NoStops", "NoStops", "A", "B", 0,
            new[] { new RoutePathPoint(1, 2, 100, 0, 200, 1, 1) },
            new[] { new RoutePathNode(0, uint.MaxValue, uint.MaxValue, 0) },
            Array.Empty<string>());
        var progress = new ActivityStationStopTracker(path).Advance(1, 2, 100, 200, 0);

        Assert.Equal(0, progress.TotalStops);
        Assert.False(progress.AllStopsCompleted);
    }

    [Fact]
    public void DoesNotCreditAnotherTileOrPointOutsideRadius()
    {
        var path = new RoutePathDefinition("route/path.pat", "Main", "Main", "A", "B", 0,
            new[] { new RoutePathPoint(1, 2, 100, 0, 200, 1, 1) },
            new[] { new RoutePathNode(2, uint.MaxValue, uint.MaxValue, 0) }, Array.Empty<string>());
        var tracker = new ActivityStationStopTracker(path, 10);
        Assert.False(tracker.Advance(2, 2, 100, 200, 0).StationStopReached);
        Assert.False(tracker.Advance(1, 2, 150, 200, 0).StationStopReached);
        Assert.Equal(0, tracker.VisitedStops);
    }
}

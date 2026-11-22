using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class FixedStepSimulationSchedulerTests
{
    [Fact]
    public void AccumulatesFrameTimeIntoDeterministicFixedSteps()
    {
        var scheduler = new FixedStepSimulationScheduler(0.1, 0.5, 8);
        var steps = new List<double>();

        Assert.Equal(0, scheduler.Advance(0.04, steps.Add));
        Assert.Equal(2, scheduler.Advance(0.16, steps.Add));

        Assert.Equal(new[] { 0.1, 0.1 }, steps);
        Assert.Equal(0.2, scheduler.SimulationTimeSeconds, 8);
        Assert.InRange(scheduler.InterpolationAlpha, 0, 1);
    }

    [Fact]
    public void BoundsCatchUpWorkAndAccountsForDroppedTime()
    {
        var scheduler = new FixedStepSimulationScheduler(0.1, 0.5, 2);
        var steps = 0;

        var executed = scheduler.Advance(2.0, _ => steps++);

        Assert.Equal(2, executed);
        Assert.Equal(2, steps);
        Assert.Equal(0.2, scheduler.SimulationTimeSeconds, 8);
        Assert.True(scheduler.DroppedTimeSeconds >= 1.7);
        Assert.InRange(scheduler.InterpolationAlpha, 0, 1);
    }

    [Fact]
    public void PauseStopsAdvancementAndResetClearsClockState()
    {
        var scheduler = new FixedStepSimulationScheduler(0.1, 0.5, 4);
        scheduler.Advance(0.2, _ => { });
        scheduler.Pause();

        Assert.Equal(0, scheduler.Advance(1, _ => throw new InvalidOperationException()));
        Assert.Equal(0.2, scheduler.SimulationTimeSeconds, 8);

        scheduler.Resume();
        scheduler.Advance(0.1, _ => { });
        scheduler.Reset();

        Assert.Equal(0, scheduler.SimulationTimeSeconds);
        Assert.Equal(0, scheduler.DroppedTimeSeconds);
        Assert.Equal(0, scheduler.InterpolationAlpha);
        Assert.False(scheduler.IsPaused);
    }
}

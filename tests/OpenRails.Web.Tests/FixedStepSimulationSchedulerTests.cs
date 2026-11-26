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
    public void ProducesSameMotionForDifferentBrowserFrameCadences()
    {
        var integrator = new TrainCarMotionIntegrator();
        var coarseScheduler = new FixedStepSimulationScheduler(0.01, 0.5, 32);
        var fineScheduler = new FixedStepSimulationScheduler(0.01, 0.5, 32);
        var coarseState = new TrainCarMotionState(10_000, 0, 0);
        var fineState = coarseState;

        coarseScheduler.Advance(0.2, step =>
            coarseState = integrator.Step(coarseState, 15_000, step).State);
        for (var frame = 0; frame < 20; frame++)
            fineScheduler.Advance(0.01, step =>
                fineState = integrator.Step(fineState, 15_000, step).State);

        Assert.Equal(coarseState.SpeedMetersPerSecond, fineState.SpeedMetersPerSecond, 8);
        Assert.Equal(coarseState.DistanceMeters, fineState.DistanceMeters, 8);
        Assert.Equal(coarseScheduler.SimulationTimeSeconds, fineScheduler.SimulationTimeSeconds, 8);
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
    public void CapturedClockRestoresTimeAccumulatorDroppedTimeAndPause()
    {
        var scheduler = new FixedStepSimulationScheduler(0.1, 0.5, 4);
        scheduler.Advance(0.15, _ => { });
        scheduler.Advance(1.0, _ => { });
        scheduler.Pause();
        var snapshot = scheduler.CaptureState();

        var restored = new FixedStepSimulationScheduler(0.1, 0.5, 4);
        restored.RestoreState(snapshot);

        Assert.Equal(scheduler.SimulationTimeSeconds, restored.SimulationTimeSeconds, 8);
        Assert.Equal(scheduler.DroppedTimeSeconds, restored.DroppedTimeSeconds, 8);
        Assert.Equal(scheduler.InterpolationAlpha, restored.InterpolationAlpha, 8);
        Assert.True(restored.IsPaused);
        Assert.Equal(0, restored.LastStepsExecuted);
    }

    [Fact]
    public void RejectsInvalidClockSnapshot()
    {
        var scheduler = new FixedStepSimulationScheduler(0.1, 0.5, 4);
        Assert.Throws<ArgumentException>(() => scheduler.RestoreState(
            new FixedStepSchedulerSnapshot(1, 0, 0.1, false)));
        Assert.Throws<ArgumentException>(() => scheduler.RestoreState(
            new FixedStepSchedulerSnapshot(double.NaN, 0, 0, false)));
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

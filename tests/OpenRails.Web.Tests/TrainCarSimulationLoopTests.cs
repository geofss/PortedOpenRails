using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrainCarSimulationLoopTests
{
    [Fact]
    public void RunsForceModelAndMotionIntegratorOnFixedSteps()
    {
        var loop = new TrainCarSimulationLoop(
            new TrainCarMotionState(10_000, 0, 0),
            fixedStepSeconds: 0.1,
            maximumFrameSeconds: 0.5,
            maximumStepsPerFrame: 8);
        var forces = new TrainCarForceBreakdown(10_000, 0, 0, 0, 0, 0, 0);

        var steps = loop.Advance(0.5, _ => forces);

        Assert.Equal(5, steps);
        Assert.Equal(0.5, loop.State.SpeedMetersPerSecond, 8);
        Assert.Equal(0.15, loop.State.DistanceMeters, 8);
        Assert.Equal(0.5, loop.SimulationTimeSeconds, 8);
    }

    [Fact]
    public void SupportsPauseResumeAndReset()
    {
        var initial = new TrainCarMotionState(10_000, 0, 0);
        var loop = new TrainCarSimulationLoop(initial, 0.1, 0.5, 8);
        var forces = new TrainCarForceBreakdown(10_000, 0, 0, 0, 0, 0, 0);

        loop.Advance(0.2, _ => forces);
        loop.Pause();
        Assert.Equal(0, loop.Advance(0.2, _ => forces));
        Assert.Equal(0.2, loop.SimulationTimeSeconds, 8);

        loop.Resume();
        loop.Advance(0.1, _ => forces);
        loop.Reset(initial);

        Assert.Equal(initial, loop.State);
        Assert.Equal(0, loop.SimulationTimeSeconds);
        Assert.False(loop.IsPaused);
    }
}

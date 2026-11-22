using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrainConsistSimulationLoopTests
{
    [Fact]
    public void AdvancesAllCarsOnTheSameFixedSimulationClock()
    {
        var initial = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(10_000, 0, 0),
            new TrainCarMotionState(20_000, 0, 0)
        });
        var loop = new TrainConsistSimulationLoop(initial, 0.1, 0.5, 8);
        var frame = new TrainConsistForceFrame(
            new[]
            {
                new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0),
                new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0)
            },
            new[] { -5_000d, 5_000d });

        Assert.Equal(2, loop.Advance(0.2, _ => frame));
        Assert.Equal(-0.1, loop.State.Cars[0].SpeedMetersPerSecond, 8);
        Assert.Equal(0.05, loop.State.Cars[1].SpeedMetersPerSecond, 8);
        Assert.Equal(0.2, loop.SimulationTimeSeconds, 8);
    }

    [Fact]
    public void RejectsEmptyOrInvalidInitialConsistStates()
    {
        Assert.Throws<ArgumentException>(() => new TrainConsistSimulationLoop(
            new TrainConsistMotionState(Array.Empty<TrainCarMotionState>())));
        Assert.Throws<ArgumentException>(() => new TrainConsistSimulationLoop(
            new TrainConsistMotionState(new[] { new TrainCarMotionState(0, 0, 0) })));
        Assert.Throws<ArgumentException>(() => new TrainConsistSimulationLoop(
            new TrainConsistMotionState(new[] { new TrainCarMotionState(1_000, double.NaN, 0) })));

        var loop = new TrainConsistSimulationLoop(new TrainConsistMotionState(
            new[] { new TrainCarMotionState(1_000, 0, 0) }));
        Assert.Throws<ArgumentException>(() => loop.Reset(
            new TrainConsistMotionState(Array.Empty<TrainCarMotionState>())));
        Assert.Equal(1, loop.State.Cars.Count);
    }

    [Fact]
    public void PausesAndResetsTheWholeConsistTogether()
    {
        var initial = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(10_000, 1, 5)
        });
        var loop = new TrainConsistSimulationLoop(initial, 0.1, 0.5, 8);
        var frame = new TrainConsistForceFrame(
            new[] { new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0) },
            new[] { 0d });

        loop.Advance(0.1, _ => frame);
        loop.Pause();
        Assert.Equal(0, loop.Advance(0.5, _ => frame));
        loop.Resume();
        loop.Reset(initial);

        Assert.Equal(initial, loop.State);
        Assert.Equal(0, loop.SimulationTimeSeconds);
        Assert.False(loop.IsPaused);
    }
}

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
    public void ConstrainsCarsAtRouteStartAndEndBoundaries()
    {
        var initial = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(1_000, 0, 9.95),
            new TrainCarMotionState(1_000, -2, 0.1)
        });
        var loop = new TrainConsistSimulationLoop(initial, 0.1, 0.5, 8, 10);
        var frame = new TrainConsistForceFrame(
            new[]
            {
                new TrainCarForceBreakdown(10_000, 0, 0, 0, 0, 0, 0),
                new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0)
            },
            new[] { 0d, 0d });

        loop.Advance(0.1, _ => frame);

        Assert.Equal(10, loop.State.Cars[0].DistanceMeters, 8);
        Assert.Equal(0, loop.State.Cars[0].SpeedMetersPerSecond, 8);
        Assert.Equal(0, loop.State.Cars[1].DistanceMeters, 8);
        Assert.Equal(0, loop.State.Cars[1].SpeedMetersPerSecond, 8);
    }

    [Fact]
    public void RejectsInvalidRouteDistanceConstraint()
    {
        var state = new TrainConsistMotionState(new[] { new TrainCarMotionState(1_000, 0, 0) });
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrainConsistSimulationLoop(state, routeLengthMeters: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrainConsistSimulationLoop(state, routeLengthMeters: double.NaN));
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
    public void CapturesAndRestoresMotionAndClockStateForConsist()
    {
        var initial = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(10_000, 2, 25),
            new TrainCarMotionState(20_000, 2, 10)
        });
        var loop = new TrainConsistSimulationLoop(initial, 0.1, 0.5, 8, 100);
        var zeroFrame = new TrainConsistForceFrame(
            new[]
            {
                new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0),
                new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0)
            },
            new[] { 0d, 0d });
        loop.Advance(0.2, _ => zeroFrame);
        loop.Pause();
        var snapshot = loop.CaptureState();
        loop.Resume();
        loop.Advance(0.1, _ => zeroFrame);
        loop.Pause();

        var restored = new TrainConsistSimulationLoop(initial, 0.1, 0.5, 8, 100);
        restored.RestoreState(snapshot);

        Assert.Equal(snapshot.MotionState.Cars, restored.State.Cars);
        Assert.Equal(snapshot.ClockState.SimulationTimeSeconds, restored.SimulationTimeSeconds, 8);
        Assert.Equal(loop.InterpolationAlpha, restored.InterpolationAlpha, 8);
        Assert.True(restored.IsPaused);
        Assert.Equal(0, restored.Advance(1, _ => throw new InvalidOperationException()));
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

using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrainConsistSimulationSessionTests
{
    [Fact]
    public void AppliesLocomotiveForceToPoweredCarAndCouplerForceToTrailer()
    {
        var locomotive = new LocomotiveTractionMetadata(
            "TestLoco", 1_000, 1_000, 100_000, 100, 0, null, "test.eng");
        var initial = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(1_000, 0, 0),
            new TrainCarMotionState(2_000, 0, 0)
        });
        var session = new TrainConsistSimulationSession(
            initial, new LocomotiveTractionMetadata?[] { locomotive, null },
            fixedStepSeconds: 0.1, maximumFrameSeconds: 0.5, maximumStepsPerFrame: 8);

        session.Advance(0.1, new[] { 100f, 0f },
            (_, _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0),
            new[] { -500d, 500d });

        Assert.Equal(0.05, session.State.Cars[0].SpeedMetersPerSecond, 8);
        Assert.Equal(0.005, session.State.Cars[0].DistanceMeters, 8);
        Assert.Equal(0.025, session.State.Cars[1].SpeedMetersPerSecond, 8);
        Assert.Equal(0.0025, session.State.Cars[1].DistanceMeters, 8);
    }

    [Fact]
    public void RejectsNonFiniteInputsBeforeAdvancingSimulation()
    {
        var initial = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(1_000, 0, 0)
        });
        var session = new TrainConsistSimulationSession(initial, new LocomotiveTractionMetadata?[] { null },
            fixedStepSeconds: 0.1, maximumFrameSeconds: 0.5, maximumStepsPerFrame: 8);

        Assert.Throws<ArgumentOutOfRangeException>(() => session.Advance(
            0.1, new[] { float.NaN },
            (_, _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0),
            new[] { 0d }));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Advance(
            0.1, new[] { 0f },
            (_, _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0),
            new[] { double.PositiveInfinity }));
        Assert.Equal(0, session.SimulationTimeSeconds);
    }

    [Fact]
    public void RejectsInputArraySizesThatDoNotMatchTheConsist()
    {
        var initial = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(1_000, 0, 0),
            new TrainCarMotionState(2_000, 0, 0)
        });
        var session = new TrainConsistSimulationSession(initial, new LocomotiveTractionMetadata?[] { null, null });

        Assert.Throws<ArgumentException>(() => session.Advance(
            0.1, new[] { 0f },
            (_, _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0),
            new[] { 0d, 0d }));
        Assert.Equal(0, session.SimulationTimeSeconds);
    }

    [Fact]
    public void PausingAndResettingMaintainConsistClockState()
    {
        var initial = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(1_000, 1, 2)
        });
        var session = new TrainConsistSimulationSession(initial, new LocomotiveTractionMetadata?[] { null },
            fixedStepSeconds: 0.1, maximumFrameSeconds: 0.5, maximumStepsPerFrame: 8);

        session.Pause();
        Assert.Equal(0, session.Advance(0.1, new[] { 0f },
            (_, _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0), new[] { 0d }));
        Assert.Equal(2, session.State.Cars[0].DistanceMeters);
        session.Resume();
        session.Reset(initial);
        Assert.Equal(0, session.SimulationTimeSeconds);
        Assert.Equal(2, session.State.Cars[0].DistanceMeters);
    }
}

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
        Assert.Equal(0.0025, session.State.Cars[0].DistanceMeters, 8);
        Assert.Equal(0.025, session.State.Cars[1].SpeedMetersPerSecond, 8);
        Assert.Equal(0.00125, session.State.Cars[1].DistanceMeters, 8);
    }

    [Fact]
    public void DynamicCouplerProviderRunsForEveryFixedSimulationStep()
    {
        var initial = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(1_000, 0, 0),
            new TrainCarMotionState(2_000, 0, 0)
        });
        var session = new TrainConsistSimulationSession(
            initial, new LocomotiveTractionMetadata?[] { null, null },
            fixedStepSeconds: 0.1, maximumFrameSeconds: 0.5, maximumStepsPerFrame: 8);
        var sampledLeadDistances = new List<double>();

        var steps = session.AdvanceWithBrakeCommands(
            0.3,
            new[] { 0f, 0f },
            new[] { 0d, 0d },
            new[] { 0d, 0d },
            (_, _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0),
            new[] { 0d, 0d },
            couplerForceProvider: state =>
            {
                sampledLeadDistances.Add(state.Cars[0].DistanceMeters);
                return new[] { 0d, 0d };
            });

        Assert.Equal(3, steps);
        Assert.Equal(3, sampledLeadDistances.Count);
        Assert.Equal(new[] { 0d, 0d, 0d }, sampledLeadDistances);
    }

    [Fact]
    public void AppliesPerCarBrakeCommandsToConsistMotion()
    {
        var initial = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(1_000, 2, 0),
            new TrainCarMotionState(2_000, 2, 0)
        });
        var session = new TrainConsistSimulationSession(
            initial, new LocomotiveTractionMetadata?[] { null, null },
            fixedStepSeconds: 0.1, maximumFrameSeconds: 0.5, maximumStepsPerFrame: 8);

        session.AdvanceWithBrakeCommands(0.1, new[] { 0f, 0f }, new[] { 50d, 50d },
            new[] { 4_000d, 4_000d },
            (_, _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0),
            new[] { 0d, 0d });

        Assert.Equal(1.8, session.State.Cars[0].SpeedMetersPerSecond, 8);
        Assert.Equal(1.9, session.State.Cars[1].SpeedMetersPerSecond, 8);
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
        Assert.Throws<ArgumentException>(() => session.AdvanceWithBrakeCommands(
            0.1, new[] { 0f }, new[] { 0d, 0d }, new[] { 1_000d, 1_000d },
            (_, _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0), new[] { 0d }));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.AdvanceWithBrakeCommands(
            0.1, new[] { 0f, 0f }, new[] { 0d, 0d }, new[] { -1d, 1_000d },
            (_, _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0), new[] { 0d, 0d }));
        Assert.Equal(0, session.SimulationTimeSeconds);
    }


    [Fact]
    public void AutomaticallyDistributesCouplerForcesForCommonAcceleration()
    {
        var locomotive = new LocomotiveTractionMetadata(
            "TestLoco", 1_500, 1_500, 100_000, 100, 0, null, "test.eng");
        var initial = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(1_000, 0, 0),
            new TrainCarMotionState(2_000, 0, 0)
        });
        var session = new TrainConsistSimulationSession(
            initial, new LocomotiveTractionMetadata?[] { locomotive, null },
            fixedStepSeconds: 0.1, maximumFrameSeconds: 0.5, maximumStepsPerFrame: 8);

        var steps = session.AdvanceWithCommonAcceleration(0.1, new[] { 100f, 0f },
            (_, _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0),
            targetAccelerationMetersPerSecondSquared: 0.5);

        Assert.Equal(1, steps);
        Assert.Equal(0.05, session.State.Cars[0].SpeedMetersPerSecond, 8);
        Assert.Equal(0.05, session.State.Cars[1].SpeedMetersPerSecond, 8);
        Assert.Equal(0.0025, session.State.Cars[0].DistanceMeters, 8);
        Assert.Equal(0.0025, session.State.Cars[1].DistanceMeters, 8);
    }

    [Fact]
    public void AutomaticCouplersDeriveAccelerationFromAvailableLocomotiveForce()
    {
        var locomotive = new LocomotiveTractionMetadata(
            "TestLoco", 1_500, 1_500, 100_000, 100, 0, null, "test.eng");
        var initial = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(1_000, 0, 0),
            new TrainCarMotionState(2_000, 0, 0)
        });
        var session = new TrainConsistSimulationSession(
            initial, new LocomotiveTractionMetadata?[] { locomotive, null },
            fixedStepSeconds: 0.1, maximumFrameSeconds: 0.5, maximumStepsPerFrame: 8);

        var steps = session.AdvanceWithAutomaticCouplers(
            0.1,
            new[] { 100f, 0f },
            new[] { 0d, 0d },
            new[] { 0d, 0d },
            (_, _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0));

        Assert.Equal(1, steps);
        Assert.Equal(0.05, session.State.Cars[0].SpeedMetersPerSecond, 8);
        Assert.Equal(0.05, session.State.Cars[1].SpeedMetersPerSecond, 8);
        Assert.Equal(0.0025, session.State.Cars[0].DistanceMeters, 8);
        Assert.Equal(0.0025, session.State.Cars[1].DistanceMeters, 8);
    }

    [Fact]
    public void CommonAccelerationModeIncludesPerCarBrakeCommands()
    {
        var initial = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(1_000, 2, 0),
            new TrainCarMotionState(2_000, 2, 0)
        });
        var session = new TrainConsistSimulationSession(
            initial, new LocomotiveTractionMetadata?[] { null, null },
            fixedStepSeconds: 0.1, maximumFrameSeconds: 0.5, maximumStepsPerFrame: 8);

        var steps = session.AdvanceWithCommonAcceleration(0.1, new[] { 0f, 0f },
            (_, _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0),
            targetAccelerationMetersPerSecondSquared: -1,
            brakePercentByCar: new[] { 50d, 50d },
            maximumBrakeForceNewtonsByCar: new[] { 2_000d, 4_000d });

        Assert.Equal(1, steps);
        Assert.Equal(1.9, session.State.Cars[0].SpeedMetersPerSecond, 8);
        Assert.Equal(1.9, session.State.Cars[1].SpeedMetersPerSecond, 8);
    }

    [Fact]
    public void CommonAccelerationSessionHonorsCouplerRatings()
    {
        var locomotive = new LocomotiveTractionMetadata(
            "TestLoco", 1_500, 1_500, 100_000, 100, 0, null, "test.eng");
        var initial = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(1_000, 0, 0),
            new TrainCarMotionState(2_000, 0, 0)
        });
        var session = new TrainConsistSimulationSession(
            initial, new LocomotiveTractionMetadata?[] { locomotive, null },
            fixedStepSeconds: 0.1, maximumFrameSeconds: 0.5, maximumStepsPerFrame: 8);

        Assert.Throws<InvalidOperationException>(() => session.AdvanceWithCommonAcceleration(
            0.1, new[] { 100f, 0f },
            (_, _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0),
            targetAccelerationMetersPerSecondSquared: 0.5,
            couplerForceLimits: new[] { new TrainCouplerForceLimit(100, 500) }));
        Assert.Equal(0, session.SimulationTimeSeconds);
    }

    [Fact]
    public void CommonAccelerationSessionRejectsInconsistentInputSizesAndNonFiniteTarget()
    {
        var initial = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(1_000, 0, 0)
        });
        var session = new TrainConsistSimulationSession(initial, new LocomotiveTractionMetadata?[] { null });

        Assert.Throws<ArgumentException>(() => session.AdvanceWithCommonAcceleration(
            0.1, Array.Empty<float>(),
            (_, _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.AdvanceWithCommonAcceleration(
            0.1, new[] { 0f },
            (_, _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0), double.NaN));
        Assert.Throws<ArgumentException>(() => session.AdvanceWithCommonAcceleration(
            0.1, new[] { 0f }, (_, _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0), 0,
            brakePercentByCar: new[] { 0d }));
        Assert.Throws<ArgumentException>(() => session.AdvanceWithCommonAcceleration(
            0.1, new[] { 0f }, (_, _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0), 0,
            brakePercentByCar: new[] { 0d, 0d }, maximumBrakeForceNewtonsByCar: new[] { 1_000d }));
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

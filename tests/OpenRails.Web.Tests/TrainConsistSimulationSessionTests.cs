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
    public void CapturesAndRestoresConsistSessionMotionAndClock()
    {
        var initial = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(1_000, 1, 5),
            new TrainCarMotionState(2_000, 1, 10)
        });
        var session = new TrainConsistSimulationSession(initial,
            new LocomotiveTractionMetadata?[] { null, null },
            fixedStepSeconds: 0.1, maximumFrameSeconds: 0.5, maximumStepsPerFrame: 8);
        Func<int, TrainCarMotionState, TrainCarResistanceForces> resistance = (int _, TrainCarMotionState _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0);
        session.Advance(0.2, new[] { 0f, 0f }, resistance, new[] { 0d, 0d });
        session.Pause();
        var snapshot = session.CaptureState();
        session.Resume();
        session.Advance(0.1, new[] { 0f, 0f }, resistance, new[] { 0d, 0d });
        session.RestoreState(snapshot);

        Assert.Equal(snapshot.MotionState.Cars, session.State.Cars);
        Assert.Equal(snapshot.ClockState.SimulationTimeSeconds, session.SimulationTimeSeconds, 8);
        Assert.True(session.IsPaused);
    }

    [Fact]
    public void FailedSnapshotRestoreLeavesMotionAndClockUnchanged()
    {
        var initial = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(1_000, 1, 5)
        });
        var session = new TrainConsistSimulationSession(initial, new LocomotiveTractionMetadata?[] { null },
            fixedStepSeconds: 0.1, maximumFrameSeconds: 0.5, maximumStepsPerFrame: 8);
        Func<int, TrainCarMotionState, TrainCarResistanceForces> resistance =
            (int _, TrainCarMotionState _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0);
        session.Advance(0.2, new[] { 0f }, resistance, new[] { 0d });
        var beforeMotion = session.State.Cars.ToArray();
        var beforeTime = session.SimulationTimeSeconds;
        var beforeDropped = session.DroppedTimeSeconds;
        var beforePaused = session.IsPaused;
        var nullMotionSnapshot = new TrainConsistSimulationSnapshot(
            null!, new FixedStepSchedulerSnapshot(0, 0, 0, false));
        Assert.Throws<ArgumentNullException>(() => session.RestoreState(nullMotionSnapshot));
        var nullClockSnapshot = new TrainConsistSimulationSnapshot(
            new TrainConsistMotionState(beforeMotion), null!);
        Assert.Throws<ArgumentNullException>(() => session.RestoreState(nullClockSnapshot));

        var invalidSnapshot = new TrainConsistSimulationSnapshot(
            new TrainConsistMotionState(new[] { new TrainCarMotionState(1_000, 0, 99) }),
            new FixedStepSchedulerSnapshot(-1, 0, 0, false));

        Assert.Throws<ArgumentException>(() => session.RestoreState(invalidSnapshot));
        Assert.Equal(beforeMotion, session.State.Cars);
        Assert.Equal(beforeTime, session.SimulationTimeSeconds, 8);
        Assert.Equal(beforeDropped, session.DroppedTimeSeconds, 8);
        Assert.Equal(beforePaused, session.IsPaused);
    }

    [Fact]
    public void BrakeCommandSimulationCapsLocomotiveTractionByPerCarAdhesion()
    {
        var locomotive = new LocomotiveTractionMetadata(
            "TestLoco", 100_000, 100_000, 1_000_000, 100, 0, null, "test.eng");
        var initial = new TrainConsistMotionState(new[] { new TrainCarMotionState(1_000, 0, 0) });
        var session = new TrainConsistSimulationSession(initial,
            new LocomotiveTractionMetadata?[] { locomotive },
            fixedStepSeconds: 0.1, maximumFrameSeconds: 0.5, maximumStepsPerFrame: 8);

        session.AdvanceWithBrakeCommands(0.1, new[] { 100f }, new[] { 0d }, new[] { 0d },
            (_, _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0), new[] { 0d },
            adhesionCoefficientByCar: new[] { 0.1d });

        var expectedAcceleration = 0.1 * TrainCarAdhesionModel.StandardGravityMetersPerSecondSquared;
        Assert.Equal(expectedAcceleration * 0.1, session.State.Cars[0].SpeedMetersPerSecond, 8);
    }

    [Fact]
    public void AllConsistAdvanceModesApplyPerCarAdhesionLimits()
    {
        var locomotive = new LocomotiveTractionMetadata(
            "TestLoco", 100_000, 100_000, 1_000_000, 100, 0, null, "test.eng");
        var resistance = new Func<int, TrainCarMotionState, TrainCarResistanceForces>(
            (_, _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0));
        var adhesionLimitedForce = (float)(0.1 * 1_000 * TrainCarAdhesionModel.StandardGravityMetersPerSecondSquared);
        var expectedAcceleration = adhesionLimitedForce / 1_000d;
        var expectedSpeed = expectedAcceleration * 0.1;
        var coefficients = new[] { 0.1d };

        var direct = new TrainConsistSimulationSession(
            new TrainConsistMotionState(new[] { new TrainCarMotionState(1_000, 0, 0) }),
            new LocomotiveTractionMetadata?[] { locomotive },
            fixedStepSeconds: 0.1, maximumFrameSeconds: 0.5, maximumStepsPerFrame: 8);
        direct.Advance(0.1, new[] { 100f }, resistance, new[] { 0d }, adhesionCoefficientByCar: coefficients);

        var common = new TrainConsistSimulationSession(
            new TrainConsistMotionState(new[] { new TrainCarMotionState(1_000, 0, 0) }),
            new LocomotiveTractionMetadata?[] { locomotive },
            fixedStepSeconds: 0.1, maximumFrameSeconds: 0.5, maximumStepsPerFrame: 8);
        common.AdvanceWithCommonAcceleration(0.1, new[] { 100f }, resistance,
            expectedAcceleration, adhesionCoefficientByCar: coefficients);

        var automatic = new TrainConsistSimulationSession(
            new TrainConsistMotionState(new[] { new TrainCarMotionState(1_000, 0, 0) }),
            new LocomotiveTractionMetadata?[] { locomotive },
            fixedStepSeconds: 0.1, maximumFrameSeconds: 0.5, maximumStepsPerFrame: 8);
        automatic.AdvanceWithAutomaticCouplers(0.1, new[] { 100f }, new[] { 0d }, new[] { 0d },
            resistance, adhesionCoefficientByCar: coefficients);

        Assert.Equal(expectedSpeed, direct.State.Cars[0].SpeedMetersPerSecond, 8);
        Assert.Equal(expectedSpeed, common.State.Cars[0].SpeedMetersPerSecond, 8);
        Assert.Equal(expectedSpeed, automatic.State.Cars[0].SpeedMetersPerSecond, 8);
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
            (_, _) => new TrainCarResistanceForces(1_000, 0, 0, 0, 0, 0),
            new[] { 0d, 0d },
            couplerForceProvider: state =>
            {
                sampledLeadDistances.Add(state.Cars[0].DistanceMeters);
                return new[] { 0d, 0d };
            });

        Assert.Equal(3, steps);
        Assert.Equal(3, sampledLeadDistances.Count);
        Assert.Equal(0, sampledLeadDistances[0]);
        Assert.True(sampledLeadDistances[1] > sampledLeadDistances[0]);
        Assert.True(sampledLeadDistances[2] > sampledLeadDistances[1]);
    }

    [Fact]
    public void DynamicCouplerProviderRejectsMismatchedPerCarForces()
    {
        var initial = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(1_000, 0, 0),
            new TrainCarMotionState(2_000, 0, 0)
        });
        var session = new TrainConsistSimulationSession(
            initial, new LocomotiveTractionMetadata?[] { null, null },
            fixedStepSeconds: 0.1, maximumFrameSeconds: 0.5, maximumStepsPerFrame: 8);

        Assert.Throws<InvalidOperationException>(() => session.AdvanceWithBrakeCommands(
            0.1,
            new[] { 0f, 0f },
            new[] { 0d, 0d },
            new[] { 0d, 0d },
            (_, _) => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0),
            new[] { 0d, 0d },
            couplerForceProvider: _ => new[] { 0d }));

        Assert.Equal(0, session.SimulationTimeSeconds);
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

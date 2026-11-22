using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class LocomotiveSimulationSessionTests
{
    [Fact]
    public void UsesParsedLocomotiveTractionToAdvanceTheSimulation()
    {
        var metadata = new LocomotiveTractionMetadata(
            "TestLoco", 1_000, 1_000, 100_000, 100, 0, null, "test.eng");
        var session = new LocomotiveSimulationSession(
            metadata, new TrainCarMotionState(1_000, 0, 0),
            fixedStepSeconds: 0.1, maximumFrameSeconds: 0.5, maximumStepsPerFrame: 8);

        var steps = session.Advance(0.1, 100, _ => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0));

        Assert.Equal(1, steps);
        Assert.Equal(0.1, session.State.SpeedMetersPerSecond, 8);
        Assert.Equal(0.005, session.State.DistanceMeters, 8);
    }

    [Fact]
    public void BrakeCommandSessionRejectsInvalidRatingBeforeStepping()
    {
        var metadata = new LocomotiveTractionMetadata(
            "TestLoco", 1_000, 1_000, 100_000, 100, 0, null, "test.eng");
        var session = new LocomotiveSimulationSession(
            metadata, new TrainCarMotionState(1_000, 0, 0),
            fixedStepSeconds: 0.1, maximumFrameSeconds: 0.5, maximumStepsPerFrame: 8);

        Assert.Throws<ArgumentOutOfRangeException>(() => session.AdvanceWithBrakeCommand(
            0.1, 0, 50, -1, _ => new TrainCarResistanceForces(0, 0, 0, 0, 0, 0)));
        Assert.Equal(0, session.SimulationTimeSeconds);
    }

    [Fact]
    public void AppliesDriverBrakeCommandAlongsideOtherResistanceForces()
    {
        var metadata = new LocomotiveTractionMetadata(
            "TestLoco", 10_000, 10_000, 1_000_000, 100, 0, null, "test.eng");
        var session = new LocomotiveSimulationSession(
            metadata, new TrainCarMotionState(10_000, 2, 0),
            fixedStepSeconds: 0.1, maximumFrameSeconds: 0.5, maximumStepsPerFrame: 8);

        session.AdvanceWithBrakeCommand(0.1, 0, 50, 10_000, _ =>
            new TrainCarResistanceForces(2_000, 0, 1_000, 0, 0, 0));

        Assert.Equal(1.96, session.State.SpeedMetersPerSecond, 8);
    }

    [Fact]
    public void AppliesBrakeAndGradeForcesThroughTheSameSimulationStep()
    {
        var metadata = new LocomotiveTractionMetadata(
            "TestLoco", 10_000, 10_000, 1_000_000, 100, 0, null, "test.eng");
        var session = new LocomotiveSimulationSession(
            metadata, new TrainCarMotionState(10_000, 2, 0),
            fixedStepSeconds: 0.1, maximumFrameSeconds: 0.5, maximumStepsPerFrame: 8);

        session.Advance(0.1, 0, _ =>
            new TrainCarResistanceForces(2_000, 0, 10_000, 0, 0, 0));

        Assert.Equal(1.92, session.State.SpeedMetersPerSecond, 8);
    }
}

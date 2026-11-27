using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrainSimulationSessionTests
{
    [Fact]
    public void AcceptedTicksAdvancePipeCylinderAndVehicleMotionTogether()
    {
        var profile = new RollingStockBrakeProfile(
            "air_single_pipe", "triple_valve",
            RollingStockBrakeSupport.PrototypeAirDistributor);
        var session = new TrainSimulationSession(
            new TrainConsistMotionState(new[]
            {
                new TrainCarMotionState(80_000, 10, 0)
            }),
            new[] { 100_000d },
            new[] { profile },
            fixedStepSeconds: 1d / 60d,
            maximumFrameSeconds: 0.25d,
            maximumStepsPerFrame: 16);

        var steps = session.Advance(0.25, 50, _ => new TrainConsistForceFrame(
            new[] { new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0) },
            new[] { 0d }));

        Assert.True(steps > 0);
        Assert.True(session.BrakePipeState.PressuresKPa[0] < TrainBrakePipeModel.NormalPressureKPa);
        Assert.True(session.BrakeCylinderState.PressuresKPa[0] > 0);
        Assert.True(session.BrakePercentByCar[0] > 0);
        Assert.True(session.State.Cars[0].SpeedMetersPerSecond < 10);
    }

    [Fact]
    public void UnsupportedBrakeProfileDoesNotGeneratePrototypeAirBrakeForce()
    {
        var profile = new RollingStockBrakeProfile(
            "vacuum", "unknown", RollingStockBrakeSupport.UnsupportedOrUnverified);
        var session = new TrainSimulationSession(
            new TrainConsistMotionState(new[]
            {
                new TrainCarMotionState(80_000, 10, 0)
            }),
            new[] { 100_000d },
            new[] { profile },
            fixedStepSeconds: 1d / 60d,
            maximumFrameSeconds: 0.25d,
            maximumStepsPerFrame: 16);

        session.Advance(0.25, 100, _ => new TrainConsistForceFrame(
            new[] { new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0) },
            new[] { 0d }));

        Assert.Equal(0, session.BrakeCylinderState.PressuresKPa[0]);
        Assert.Equal(0, session.BrakePercentByCar[0]);
        Assert.Equal(10, session.State.Cars[0].SpeedMetersPerSecond, 6);
    }

    [Fact]
    public void SnapshotRestoresMotionAndAllPneumaticSubsystemStates()
    {
        var profile = new RollingStockBrakeProfile(
            "air_single_pipe", "distributor",
            RollingStockBrakeSupport.PrototypeAirDistributor);
        var session = new TrainSimulationSession(
            new TrainConsistMotionState(new[]
            {
                new TrainCarMotionState(80_000, 10, 0)
            }),
            new[] { 100_000d },
            new[] { profile },
            fixedStepSeconds: 1d / 60d,
            maximumFrameSeconds: 0.25d,
            maximumStepsPerFrame: 16);
        session.Advance(0.25, 40, _ => new TrainConsistForceFrame(
            new[] { new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0) },
            new[] { 0d }));
        var snapshot = session.CaptureState();
        var expectedSpeed = session.State.Cars[0].SpeedMetersPerSecond;
        var expectedPipe = session.BrakePipeState.PressuresKPa[0];
        var expectedCylinder = session.BrakeCylinderState.PressuresKPa[0];

        session.Advance(0.25, 0, _ => new TrainConsistForceFrame(
            new[] { new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0) },
            new[] { 0d }));
        session.RestoreState(snapshot);

        Assert.Equal(expectedSpeed, session.State.Cars[0].SpeedMetersPerSecond, 9);
        Assert.Equal(expectedPipe, session.BrakePipeState.PressuresKPa[0], 9);
        Assert.Equal(expectedCylinder, session.BrakeCylinderState.PressuresKPa[0], 9);
    }

    [Fact]
    public void ConstructorRejectsBrakeRatingsThatDoNotMatchConsist()
    {
        var state = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(80_000, 0, 0),
            new TrainCarMotionState(80_000, 0, 20)
        });

        Assert.Throws<ArgumentException>(() =>
            new TrainSimulationSession(state, new[] { 100_000d }));
    }
}

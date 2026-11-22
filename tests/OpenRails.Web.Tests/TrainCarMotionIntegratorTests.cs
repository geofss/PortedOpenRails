using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrainCarMotionIntegratorTests
{
    [Fact]
    public void AppliesSignedNewtonianForceToLongitudinalMotion()
    {
        var initial = new TrainCarMotionState(20_000, 0, 0);

        var result = new TrainCarMotionIntegrator().Step(initial, 40_000, 0.5);

        Assert.Equal(2, result.AccelerationMetersPerSecondSquared, 8);
        Assert.Equal(1, result.State.SpeedMetersPerSecond, 8);
        Assert.Equal(0.5, result.State.DistanceMeters, 8);
    }

    [Fact]
    public void ResistingForceStopsCarWithoutReversingIt()
    {
        var initial = new TrainCarMotionState(10_000, 1, 12);

        var result = new TrainCarMotionIntegrator().Step(initial, -20_000, 1);

        Assert.Equal(0, result.State.SpeedMetersPerSecond);
        Assert.Equal(12, result.State.DistanceMeters);
    }

    [Fact]
    public void SupportsReverseMotionAndRejectsInvalidMass()
    {
        var reverse = new TrainCarMotionIntegrator().Step(
            new TrainCarMotionState(5_000, -2, 10), -5_000, 1);

        Assert.Equal(-3, reverse.State.SpeedMetersPerSecond, 8);
        Assert.Equal(7, reverse.State.DistanceMeters, 8);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TrainCarMotionIntegrator().Step(new TrainCarMotionState(0, 0, 0), 1, 1));
    }
}

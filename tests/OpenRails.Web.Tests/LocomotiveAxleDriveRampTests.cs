using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class LocomotiveAxleDriveRampTests
{
    [Fact]
    public void ScalesForceRampByAxleTractiveForceFraction()
    {
        var result = LocomotiveAxleDriveRamp.Advance(
            0, 0, 0, 100_000, 100_000, 0.25f, 1,
            forceRampUpNewtonsPerSecond: 40_000);

        Assert.Equal(10_000, result);
    }

    [Fact]
    public void ReversesRampDirectionForNegativeForce()
    {
        var result = LocomotiveAxleDriveRamp.Advance(
            -50_000, 0, 0, -100_000, 100_000, 0.5f, 1,
            forceRampUpNewtonsPerSecond: 20_000,
            forceRampDownNewtonsPerSecond: 10_000);

        Assert.Equal(-60_000, result);
    }

    [Fact]
    public void UsesDedicatedToZeroRateWhenTargetChangesSign()
    {
        var result = LocomotiveAxleDriveRamp.Advance(
            50_000, 0, 0, -50_000, 100_000, 0.5f, 1,
            forceRampUpNewtonsPerSecond: 40_000,
            forceRampDownNewtonsPerSecond: 20_000,
            forceRampDownToZeroNewtonsPerSecond: 10_000);

        Assert.Equal(45_000, result);
    }

    [Fact]
    public void PowerRampUsesPreviousAndCurrentAxleSpeed()
    {
        var result = LocomotiveAxleDriveRamp.Advance(
            10_000, 10, 20, 30_000, 50_000, 0.5f, 1,
            powerRampUpWattsPerSecond: 100_000);

        Assert.Equal(7_500, result);
    }

    [Fact]
    public void AppliesMaximumForceBeforeRamping()
    {
        var result = LocomotiveAxleDriveRamp.Advance(
            0, 0, 0, 100_000, 20_000, 1, 1);

        Assert.Equal(20_000, result);
    }

    [Fact]
    public void RejectsInvalidAxleRampInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LocomotiveAxleDriveRamp.Advance(0, 0, 0, 1, 1, -0.1f, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LocomotiveAxleDriveRamp.Advance(0, 0, 0, 1, 1, 1, double.NaN));
    }
}

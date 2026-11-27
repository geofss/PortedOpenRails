using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class LocomotiveAxleDriveSetTests
{
    [Fact]
    public void DistributesTotalTargetByDrivenAxleFractions()
    {
        var drive = new LocomotiveAxleDriveSet(new[] { 0.25f, 0.25f, 0.25f, 0.25f });
        var total = drive.Advance(1, 100_000, 100_000, forceRampUpNewtonsPerSecond: 40_000);
        Assert.Equal(40_000, total);
        Assert.All(drive.ForceByAxle, force => Assert.Equal(10_000, force));
    }

    [Fact]
    public void TracksPreviousAndCurrentWheelSpeedsForPowerRamp()
    {
        var drive = new LocomotiveAxleDriveSet(new[] { 0.5f, 0.5f });
        drive.SetAxleSpeeds(new[] { 10d, 10d });
        drive.Advance(1, 20_000, 20_000);
        drive.SetAxleSpeeds(new[] { 20d, 20d });
        var result = drive.Advance(1, 20_000, 20_000, powerRampUpWattsPerSecond: 100_000);
        Assert.True(result > 0);
        Assert.True(result < 20_000);
    }

    [Fact]
    public void IndividualSlipControlTargetsOverrideFractionalAllocation()
    {
        var drive = new LocomotiveAxleDriveSet(new[] { 0.5f, 0.5f });
        var result = drive.Advance(1, 100_000, 100_000,
            forceRampUpNewtonsPerSecond: 100_000,
            individualAxleTargetForceNewtons: new[] { 10_000f, 30_000f });
        Assert.Equal(40_000, result);
        Assert.Equal(new[] { 10_000f, 30_000f }, drive.ForceByAxle);
    }

    [Fact]
    public void RequiresForceFractionsToSumToOne()
    {
        Assert.Throws<ArgumentException>(() =>
            new LocomotiveAxleDriveSet(new[] { 0.4f, 0.4f }));
    }

    [Fact]
    public void ResetClearsForceAndWheelSpeedHistory()
    {
        var drive = new LocomotiveAxleDriveSet(new[] { 1f });
        drive.SetAxleSpeeds(new[] { 12d });
        drive.Advance(1, 10_000, 10_000, forceRampUpNewtonsPerSecond: 10_000);
        drive.Reset();
        Assert.Equal(0, drive.TotalDriveForceNewtons);
        Assert.Equal(0, drive.ForceByAxle[0]);
    }
}

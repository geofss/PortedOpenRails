using ORTS.Common;
using Xunit;

namespace PortedOpenRails.Tests;

public class TrainMotionIntegratorTests
{
    [Fact]
    public void PositiveAcceleration_UpdatesSpeedAndPosition()
    {
        double position = 0;
        double speed = 0;

        TrainMotionIntegrator.Step(ref position, ref speed, 2, 3, 20);

        Assert.Equal(6, speed, 8);
        Assert.Equal(9, position, 8);
    }

    [Fact]
    public void ConstantSpeed_AdvancesPositionLinearly()
    {
        double position = 10;
        double speed = 5;

        TrainMotionIntegrator.Step(ref position, ref speed, 0, 4, 20);

        Assert.Equal(30, position, 8);
        Assert.Equal(5, speed, 8);
    }

    [Fact]
    public void SpeedLimit_IsEnforcedAtTheExactTimeItIsReached()
    {
        double position = 0;
        double speed = 8;

        TrainMotionIntegrator.Step(ref position, ref speed, 4, 3, 10);

        // Accelerate from 8 to 10 m/s for 0.5 s, then cruise for 2.5 s.
        Assert.Equal(10, speed, 8);
        Assert.Equal(27.5, position, 8);
    }

    [Fact]
    public void NegativeAcceleration_ChangesSignedVelocityAndPosition()
    {
        double position = 0;
        double speed = 10;

        TrainMotionIntegrator.Step(ref position, ref speed, -2, 2, 20);

        Assert.Equal(6, speed, 8);
        Assert.Equal(16, position, 8);
    }

    [Fact]
    public void ZeroDuration_DoesNotAdvanceMotion()
    {
        double position = 12;
        double speed = 4;

        TrainMotionIntegrator.Step(ref position, ref speed, 100, 0, 20);

        Assert.Equal(12, position, 8);
        Assert.Equal(4, speed, 8);
    }

    [Fact]
    public void ZeroSpeedLimit_StopsMotion()
    {
        double position = 12;
        double speed = 4;

        TrainMotionIntegrator.Step(ref position, ref speed, 3, 2, 0);

        Assert.Equal(12, position, 8);
        Assert.Equal(0, speed, 8);
    }

    [Fact]
    public void NegativeElapsedTime_IsRejected()
    {
        double position = 0;
        double speed = 0;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrainMotionIntegrator.Step(ref position, ref speed, 1, -1, 20));
    }
}

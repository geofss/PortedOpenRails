using System;
using ORTS.Common;
using Xunit;

namespace PortedOpenRails.Tests;

public class TrainPhysicsStepTests
{
    [Fact]
    public void MotiveForce_UsesFEqualsMaToAdvanceMotion()
    {
        double position = 0;
        double speed = 0;

        var netForce = TrainPhysicsStep.Advance(
            ref position, ref speed,
            2000, 0, 1000,
            0, 0, 0, 0, 0,
            3, 30);

        Assert.Equal(2000, netForce);
        Assert.Equal(6, speed, 8);
        Assert.Equal(9, position, 8);
    }

    [Fact]
    public void BrakingForce_BringsTrainToRestWithoutReversing()
    {
        double position = 0;
        double speed = 10;

        var netForce = TrainPhysicsStep.Advance(
            ref position, ref speed,
            0, 0, 1000,
            0, 5000, 0, 0, 0,
            3, 30);

        Assert.Equal(-5000, netForce);
        Assert.Equal(0, speed, 8);
        Assert.Equal(10, position, 8);
    }

    [Fact]
    public void BrakingForce_OnlyReducesSpeedWhenStopIsNotReached()
    {
        double position = 0;
        double speed = 10;

        TrainPhysicsStep.Advance(
            ref position, ref speed,
            0, 0, 1000,
            0, 2000, 0, 0, 0,
            2, 30);

        Assert.Equal(6, speed, 8);
        Assert.Equal(16, position, 8);
    }

    [Fact]
    public void MotiveForce_CanStartFromRest()
    {
        double position = 0;
        double speed = 0;

        TrainPhysicsStep.Advance(
            ref position, ref speed,
            1000, 0, 1000,
            0, 0, 0, 0, 0,
            2, 20);

        Assert.Equal(2, speed, 8);
        Assert.Equal(2, position, 8);
    }

    [Fact]
    public void NonPositiveMass_IsRejected()
    {
        double position = 0;
        double speed = 0;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrainPhysicsStep.Advance(
                ref position, ref speed,
                1000, 0, 0,
                0, 0, 0, 0, 0,
                1, 20));
    }
}

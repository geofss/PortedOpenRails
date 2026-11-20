using System;
using ORTS.Common;
using Xunit;

namespace PortedOpenRails.Tests;

public class LocomotiveEffortCalculatorTests
{
    [Fact]
    public void TractiveEffort_IsLimitedByThrottleAndMaximumForce()
    {
        var force = LocomotiveEffortCalculator.CalculateTractiveForce(0.5, 100000, 1000000, 10);
        Assert.Equal(50000, force);
    }

    [Fact]
    public void TractiveEffort_IsLimitedByAvailablePowerAtSpeed()
    {
        var force = LocomotiveEffortCalculator.CalculateTractiveForce(1, 100000, 500000, 10);
        Assert.Equal(50000, force);
    }

    [Fact]
    public void TractiveEffort_AtStandstillUsesMaximumForceLimit()
    {
        var force = LocomotiveEffortCalculator.CalculateTractiveForce(1, 80000, 1000, 0);
        Assert.Equal(80000, force);
    }

    [Fact]
    public void ZeroAvailablePower_PreventsTractiveEffortWhileMoving()
    {
        var force = LocomotiveEffortCalculator.CalculateTractiveForce(1, 80000, 0, 5);
        Assert.Equal(0, force);
    }

    [Fact]
    public void BrakeEffort_PrioritizesDynamicAndUsesFrictionForRemainder()
    {
        var effort = LocomotiveEffortCalculator.CalculateBrakeEffort(
            0.5, 40000, 1000000, 60000, 10);

        Assert.Equal(40000, effort.DynamicBrakeForceNewtons);
        Assert.Equal(10000, effort.FrictionBrakeForceNewtons);
    }

    [Fact]
    public void DynamicBrakePowerLimit_ReducesDynamicEffortAtSpeed()
    {
        var effort = LocomotiveEffortCalculator.CalculateBrakeEffort(
            1, 50000, 200000, 60000, 10);

        Assert.Equal(20000, effort.DynamicBrakeForceNewtons);
        Assert.Equal(60000, effort.FrictionBrakeForceNewtons);
    }

    [Fact]
    public void BrakeEffort_AtStandstillUsesFrictionBrakes()
    {
        var effort = LocomotiveEffortCalculator.CalculateBrakeEffort(
            1, 50000, 1000, 60000, 0);

        Assert.Equal(0, effort.DynamicBrakeForceNewtons);
        Assert.Equal(60000, effort.FrictionBrakeForceNewtons);
    }

    [Fact]
    public void ControlFractionsOutsideZeroToOneAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LocomotiveEffortCalculator.CalculateTractiveForce(1.1, 1000, 1000, 1));
    }
}

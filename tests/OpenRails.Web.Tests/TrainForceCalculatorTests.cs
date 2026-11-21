using System;
using ORTS.Common;
using Xunit;

namespace PortedOpenRails.Tests;

public class TrainForceCalculatorTests
{
    [Fact]
    public void ForwardMotion_SubtractsAllResistanceForces()
    {
        var netForce = TrainForceCalculator.CalculateNetForce(
            12000, 500, 10, 100, 2000, 300, 400, 200);

        Assert.Equal(9500, netForce);
    }

    [Fact]
    public void ReverseMotion_AddsResistanceMagnitudesToSignedBaseForce()
    {
        var netForce = TrainForceCalculator.CalculateNetForce(
            -12000, -500, -10, 100, 2000, 300, 400, 200);

        Assert.Equal(-9500, netForce);
    }

    [Fact]
    public void StationaryCar_LeavesBaseForceUnchanged()
    {
        var netForce = TrainForceCalculator.CalculateNetForce(
            1000, -250, 0, 100, 200, 300, 400, 500);

        Assert.Equal(750, netForce);
    }

    [Fact]
    public void NoResistance_ReturnsMotivePlusGravity()
    {
        var netForce = TrainForceCalculator.CalculateNetForce(
            8000, -1000, 15, 0, 0, 0, 0, 0);

        Assert.Equal(7000, netForce);
    }

    [Fact]
    public void NegativeResistanceMagnitude_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrainForceCalculator.CalculateNetForce(0, 0, 1, -1, 0, 0, 0, 0));
    }

    [Fact]
    public void NonFiniteSpeed_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrainForceCalculator.CalculateNetForce(0, 0, double.NaN, 0, 0, 0, 0, 0));
    }
}

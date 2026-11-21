using System;
using ORTS.Common;
using Xunit;

namespace PortedOpenRails.Tests;

public class TrainConsistIntegratorTests
{
    [Fact]
    public void LocomotiveAndCoaches_MoveTogetherUnderTraction()
    {
        var positions = new[] { 100.0, 80.0, 60.0 };
        var masses = new[] { 80000.0, 40000.0, 40000.0 };
        var forces = new[]
        {
            new TrainCarForceState(120000, 0, 0, 0, 0, 0, 0),
            new TrainCarForceState(0, 0, 0, 0, 0, 0, 0),
            new TrainCarForceState(0, 0, 0, 0, 0, 0, 0)
        };
        var speed = 0.0;

        TrainConsistIntegrator.Advance(positions, masses, forces, ref speed, 2, 100);

        Assert.Equal(101.5, positions[0], 8);
        Assert.Equal(81.5, positions[1], 8);
        Assert.Equal(61.5, positions[2], 8);
        Assert.Equal(1.5, speed, 8);
        Assert.Equal(20, positions[0] - positions[1], 8);
        Assert.Equal(20, positions[1] - positions[2], 8);
    }

    [Fact]
    public void CoachResistanceContributesToWholeConsistAcceleration()
    {
        var positions = new[] { 0.0, -15.0 };
        var masses = new[] { 50000.0, 50000.0 };
        var forces = new[]
        {
            new TrainCarForceState(100000, 0, 0, 0, 0, 0, 0),
            new TrainCarForceState(0, 0, 20000, 0, 0, 0, 0)
        };
        var speed = 10.0;

        var netForce = TrainConsistIntegrator.Advance(
            positions, masses, forces, ref speed, 1, 100);

        Assert.Equal(80000, netForce);
        Assert.Equal(0.8, speed - 10, 8);
        Assert.Equal(10.4, positions[0], 8);
        Assert.Equal(-4.6, positions[1], 8);
    }

    [Fact]
    public void BrakingStopsEntireConsistWithoutReversing()
    {
        var positions = new[] { 50.0, 30.0, 10.0 };
        var masses = new[] { 60000.0, 30000.0, 30000.0 };
        var forces = new[]
        {
            new TrainCarForceState(0, 0, 0, 180000, 0, 0, 0),
            new TrainCarForceState(0, 0, 0, 90000, 0, 0, 0),
            new TrainCarForceState(0, 0, 0, 90000, 0, 0, 0)
        };
        var speed = 5.0;

        TrainConsistIntegrator.Advance(positions, masses, forces, ref speed, 2, 100);

        Assert.Equal(0, speed);
        Assert.Equal(54.1666666667, positions[0], 6);
        Assert.Equal(34.1666666667, positions[1], 6);
        Assert.Equal(14.1666666667, positions[2], 6);
    }

    [Fact]
    public void ReverseMovementPreservesCoachSpacing()
    {
        var positions = new[] { 0.0, 20.0 };
        var masses = new[] { 50000.0, 50000.0 };
        var forces = new[]
        {
            new TrainCarForceState(-50000, 0, 0, 0, 0, 0, 0),
            new TrainCarForceState(0, 0, 0, 0, 0, 0, 0)
        };
        var speed = 0.0;

        TrainConsistIntegrator.Advance(positions, masses, forces, ref speed, 1, 100);

        Assert.Equal(-0.25, positions[0], 8);
        Assert.Equal(19.75, positions[1], 8);
        Assert.Equal(20, positions[1] - positions[0], 8);
        Assert.Equal(-0.5, speed, 8);
    }

    [Fact]
    public void EmptyOrMismatchedConsistArraysAreRejected()
    {
        var speed = 0.0;
        Assert.Throws<ArgumentException>(() =>
            TrainConsistIntegrator.Advance(Array.Empty<double>(), Array.Empty<double>(),
                Array.Empty<TrainCarForceState>(), ref speed, 1, 100));
        Assert.Throws<ArgumentException>(() =>
            TrainConsistIntegrator.Advance(new[] { 0.0 }, Array.Empty<double>(),
                Array.Empty<TrainCarForceState>(), ref speed, 1, 100));
    }
}

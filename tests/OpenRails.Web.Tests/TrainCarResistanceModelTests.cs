using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrainCarResistanceModelTests
{
    [Fact]
    public void CalculatesDavisRollingAerodynamicAndGradeForces()
    {
        var state = new TrainCarMotionState(1_000, 10, 0);
        var parameters = new TrainCarResistanceParameters(
            RollingResistanceCoefficient: 0.001,
            DavisA: 5,
            DavisB: 2,
            DavisC: 0.5,
            AerodynamicDragAreaSquareMeters: 6,
            WindSpeedMetersPerSecond: 5,
            GradientRadians: 0.1,
            CurveResistanceNewtons: 12,
            TunnelResistanceNewtons: 8);

        var result = new TrainCarResistanceModel().Calculate(state, parameters);

        Assert.Equal(84.80665, result.RollingFrictionForceNewtons, 5);
        Assert.Equal(91.875, result.WindResistanceForceNewtons, 5);
        Assert.Equal(1_000 * 9.80665 * Math.Sin(0.1), result.GravityForceNewtons, 5);
        Assert.Equal(12, result.CurveResistanceForceNewtons);
        Assert.Equal(8, result.TunnelResistanceForceNewtons);
    }

    [Fact]
    public void ReversingMotionKeepsResistanceMagnitudeNonNegative()
    {
        var result = new TrainCarResistanceModel().Calculate(
            new TrainCarMotionState(2_000, -5, 0),
            new TrainCarResistanceParameters(DavisA: 10, DavisB: 2, DavisC: 0.5));

        Assert.True(result.RollingFrictionForceNewtons > 0);
        Assert.True(result.WindResistanceForceNewtons > 0);
    }

    [Fact]
    public void AppliesDavisBCoefficientsWithSpeedMagnitude()
    {
        var model = new TrainCarResistanceModel();
        var forward = model.Calculate(
            new TrainCarMotionState(1_000, 4, 0),
            new TrainCarResistanceParameters(
                RollingResistanceCoefficient: 0,
                DavisB: 3,
                DavisC: 2,
                AerodynamicDragAreaSquareMeters: 0));
        var reverse = model.Calculate(
            new TrainCarMotionState(1_000, -4, 0),
            new TrainCarResistanceParameters(
                RollingResistanceCoefficient: 0,
                DavisB: 3,
                DavisC: 2,
                AerodynamicDragAreaSquareMeters: 0));

        Assert.Equal(44, forward.RollingFrictionForceNewtons);
        Assert.Equal(44, reverse.RollingFrictionForceNewtons);
    }

    [Fact]
    public void DavisAResistanceAppliesEvenAtStandstill()
    {
        var result = new TrainCarResistanceModel().Calculate(
            new TrainCarMotionState(1_000, 0, 0),
            new TrainCarResistanceParameters(
                RollingResistanceCoefficient: 0,
                DavisA: 125,
                AerodynamicDragAreaSquareMeters: 0));

        Assert.Equal(125, result.RollingFrictionForceNewtons);
    }

    [Fact]
    public void RejectsInvalidResistanceParameters()
    {
        var model = new TrainCarResistanceModel();
        var state = new TrainCarMotionState(1_000, 0, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => model.Calculate(
            state, new TrainCarResistanceParameters(DavisA: -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => model.Calculate(
            state, new TrainCarResistanceParameters(GradientRadians: Math.PI / 2)));
    }

    [Fact]
    public void RejectsOverflowingDavisResistance()
    {
        var model = new TrainCarResistanceModel();
        var state = new TrainCarMotionState(1_000, 1e200, 0);
        var parameters = new TrainCarResistanceParameters(
            RollingResistanceCoefficient: 0,
            DavisC: 1,
            AerodynamicDragAreaSquareMeters: 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => model.Calculate(state, parameters));
    }

    [Fact]
    public void RejectsOverflowingWindResistance()
    {
        var model = new TrainCarResistanceModel();
        var state = new TrainCarMotionState(1_000, double.MaxValue, 0);
        var parameters = new TrainCarResistanceParameters(
            RollingResistanceCoefficient: 0,
            AerodynamicDragAreaSquareMeters: 1,
            WindSpeedMetersPerSecond: -double.MaxValue);

        Assert.Throws<ArgumentOutOfRangeException>(() => model.Calculate(state, parameters));
    }

    [Fact]
    public void RejectsOverflowingGradeForce()
    {
        var model = new TrainCarResistanceModel();
        var state = new TrainCarMotionState(double.MaxValue, 0, 0);
        var parameters = new TrainCarResistanceParameters(
            RollingResistanceCoefficient: 0,
            AerodynamicDragAreaSquareMeters: 0,
            GradientRadians: 0.5);

        Assert.Throws<ArgumentOutOfRangeException>(() => model.Calculate(state, parameters));
    }
}

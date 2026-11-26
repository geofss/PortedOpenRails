using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrainConsistCouplerForceDistributorTests
{
    [Fact]
    public void DistributesBoundaryForcesToReachCommonAcceleration()
    {
        var state = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(10_000, 0, 0),
            new TrainCarMotionState(20_000, 0, 0),
            new TrainCarMotionState(30_000, 0, 0)
        });
        var external = new[]
        {
            new TrainCarForceBreakdown(30_000, 0, 0, 0, 0, 0, 0),
            new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0),
            new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0)
        };
        var distributor = new TrainConsistCouplerForceDistributor();

        var boundaries = distributor.CalculateBoundaryForces(state, external, 0.5);
        var perCar = distributor.ExpandBoundaryForcesToCarForces(boundaries);
        Assert.Equal(perCar, distributor.CalculatePerCarCouplerForces(state, external, 0.5));

        Assert.Equal(2, boundaries.Count);
        Assert.Equal(-25_000, boundaries[0], 8);
        Assert.Equal(-15_000, boundaries[1], 8);
        Assert.Equal(-25_000, perCar[0], 8);
        Assert.Equal(10_000, perCar[1], 8);
        Assert.Equal(15_000, perCar[2], 8);
        for (var i = 0; i < state.Cars.Count; i++)
        {
            var net = new TrainCarForceModel().CalculateNetLongitudinalForce(state.Cars[i], external[i]) + perCar[i];
            Assert.Equal(state.Cars[i].MassKg * 0.5, net, 8);
        }
    }

    [Fact]
    public void RejectsTargetAccelerationThatExternalForcesCannotAchieve()
    {
        var state = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(1_000, 0, 0),
            new TrainCarMotionState(2_000, 0, 0)
        });
        var external = new[]
        {
            new TrainCarForceBreakdown(1_000, 0, 0, 0, 0, 0, 0),
            new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0)
        };

        Assert.Throws<InvalidOperationException>(() =>
            new TrainConsistCouplerForceDistributor().CalculateBoundaryForces(state, external, 0.5));
    }

    [Fact]
    public void EnforcesPerBoundaryTensionAndCompressionRatings()
    {
        var state = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(10_000, 0, 0),
            new TrainCarMotionState(20_000, 0, 0),
            new TrainCarMotionState(30_000, 0, 0)
        });
        var external = new[]
        {
            new TrainCarForceBreakdown(30_000, 0, 0, 0, 0, 0, 0),
            new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0),
            new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0)
        };
        var distributor = new TrainConsistCouplerForceDistributor();

        var valid = new[] { new TrainCouplerForceLimit(5_000, 30_000), new TrainCouplerForceLimit(20_000, 20_000) };
        Assert.Equal(2, distributor.CalculateBoundaryForces(state, external, 0.5, valid).Count);
        Assert.Throws<InvalidOperationException>(() => distributor.CalculateBoundaryForces(
            state, external, 0.5,
            new[] { new TrainCouplerForceLimit(5_000, 20_000), new TrainCouplerForceLimit(20_000, 20_000) }));
        Assert.Throws<ArgumentException>(() => distributor.CalculateBoundaryForces(
            state, external, 0.5, new[] { new TrainCouplerForceLimit(1, 1) }));
    }

    [Fact]
    public void RejectsOverflowingTotalMassAndNullForceRatings()
    {
        var state = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(1e308, 0, 0),
            new TrainCarMotionState(1e308, 0, 0)
        });
        var external = new[]
        {
            new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0),
            new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0)
        };
        var distributor = new TrainConsistCouplerForceDistributor();

        Assert.Throws<ArgumentOutOfRangeException>(() => distributor.CalculateBoundaryForces(
            state, external, 0));
        Assert.Throws<ArgumentException>(() => distributor.CalculateBoundaryForces(
            new TrainConsistMotionState(new[] { new TrainCarMotionState(1_000, 0, 0), new TrainCarMotionState(1_000, 0, 0) }),
            new[] { new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0), new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0) },
            0, new TrainCouplerForceLimit[] { null! }));
    }

    [Fact]
    public void EqualExternalForcesNeedNoCouplerForce()
    {
        var state = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(10_000, 0, 0),
            new TrainCarMotionState(20_000, 0, 0)
        });
        var external = new[]
        {
            new TrainCarForceBreakdown(5_000, 0, 0, 0, 0, 0, 0),
            new TrainCarForceBreakdown(10_000, 0, 0, 0, 0, 0, 0)
        };
        var distributor = new TrainConsistCouplerForceDistributor();

        Assert.Equal(new[] { 0d }, distributor.CalculateBoundaryForces(state, external, 0.5));
    }

    [Fact]
    public void RejectsInvalidInputsAndSupportsSingleCarConsist()
    {
        var single = new TrainConsistMotionState(new[] { new TrainCarMotionState(1_000, 0, 0) });
        var distributor = new TrainConsistCouplerForceDistributor();
        Assert.Empty(distributor.CalculateBoundaryForces(single,
            new[] { new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0) }, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => distributor.CalculateBoundaryForces(
            single, new[] { new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0) }, double.NaN));
        Assert.Throws<ArgumentException>(() => distributor.CalculateBoundaryForces(
            new TrainConsistMotionState(Array.Empty<TrainCarMotionState>()),
            Array.Empty<TrainCarForceBreakdown>(), 0));
        Assert.Throws<ArgumentException>(() => distributor.CalculateBoundaryForces(
            single, Array.Empty<TrainCarForceBreakdown>(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => distributor.ExpandBoundaryForcesToCarForces(
            new[] { double.NaN }));
    }
}

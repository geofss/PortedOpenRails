using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrainConsistCouplerDynamicsTests
{
    [Fact]
    public void NoForceWhileRelativeDisplacementRemainsInsideSlack()
    {
        var state = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(1000, 0, 0),
            new TrainCarMotionState(1000, 0, 2.05)
        });
        var result = new TrainConsistCouplerDynamics().CalculatePerCarForces(
            state, new[] { 2d }, new[] { 0.1d }, new[] { 1000d }, new[] { 0d }, new[] { 5000d });
        Assert.Equal(new[] { 0d, 0d }, result);
    }

    [Fact]
    public void ProducesEqualAndOppositeForcesAfterSlackAndConservesNetForce()
    {
        var state = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(1000, 0.1, 0),
            new TrainCarMotionState(1000, 0.4, 2.2)
        });
        var result = new TrainConsistCouplerDynamics().CalculatePerCarForces(
            state, new[] { 2d }, new[] { 0.1d }, new[] { 1000d }, new[] { 100d }, new[] { 5000d });
        Assert.Equal(-300, result[0], 8);
        Assert.Equal(300, result[1], 8);
        Assert.Equal(0, result.Sum(), 8);
    }

    [Fact]
    public void RequiresExactlyOneParameterSetPerBoundary()
    {
        var state = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(1000, 0, 0),
            new TrainCarMotionState(1000, 0, 2),
            new TrainCarMotionState(1000, 0, 4)
        });
        Assert.Throws<ArgumentException>(() => new TrainConsistCouplerDynamics().CalculatePerCarForces(
            state, new[] { 2d }, new[] { 0d }, new[] { 1d }, new[] { 0d }, new[] { 1d }));
    }
}

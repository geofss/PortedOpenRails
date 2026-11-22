using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrainCouplerForceLimitTests
{
    [Theory]
    [InlineData(5, true)]
    [InlineData(-5, true)]
    [InlineData(6, false)]
    [InlineData(-6, false)]
    public void EvaluatesCompressionAndTensionRatings(double force, bool expected)
    {
        var limit = new TrainCouplerForceLimit(5, 5);
        limit.Validate();
        Assert.Equal(expected, limit.Allows(force));
    }

    [Fact]
    public void RejectsNegativeAndNonFiniteRatings()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrainCouplerForceLimit(-1, 2).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrainCouplerForceLimit(2, double.NaN).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrainCouplerForceLimit(double.PositiveInfinity, 2).Validate());
    }
}

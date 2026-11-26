using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrainCarBrakeModelTests
{
    private readonly TrainCarBrakeModel model = new();

    [Theory]
    [InlineData(12, 50, 20_000, 10_000)]
    [InlineData(-12, 50, 20_000, -10_000)]
    [InlineData(12, 0, 20_000, 0)]
    [InlineData(0, 100, 20_000, 0)]
    [InlineData(12, 150, 20_000, 20_000)]
    [InlineData(12, -20, 20_000, 0)]
    public void CalculatesSignedRetardingForce(double speed, double command, double rating, double expected)
    {
        Assert.Equal(expected, model.CalculateBrakeRetardForceNewtons(speed, command, rating), 8);
    }

    [Fact]
    public void RejectsNonFiniteInputsAndNegativeRating()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => model.CalculateBrakeRetardForceNewtons(double.NaN, 50, 1000));
        Assert.Throws<ArgumentOutOfRangeException>(() => model.CalculateBrakeRetardForceNewtons(10, double.PositiveInfinity, 1000));
        Assert.Throws<ArgumentOutOfRangeException>(() => model.CalculateBrakeRetardForceNewtons(10, 50, -1));
    }
}

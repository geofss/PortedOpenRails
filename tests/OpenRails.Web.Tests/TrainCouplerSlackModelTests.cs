using OpenRails.Web.Simulation;
using Xunit;

public sealed class TrainCouplerSlackModelTests
{
    private readonly TrainCouplerSlackModel model = new();

    [Fact]
    public void ProducesNoForceWhileSlackRemains()
    {
        Assert.Equal(0, model.CalculateForceOnRearCar(0.04, 0.5, 0.05, 10000, 500, 20000));
    }

    [Fact]
    public void AppliesSpringAndDampingForceAfterSlackIsTakenUp()
    {
        var force = model.CalculateForceOnRearCar(0.10, 0.20, 0.05, 10000, 500, 20000);
        Assert.Equal(1100, force, 6);
    }

    [Fact]
    public void ReversesForceForCompression()
    {
        var force = model.CalculateForceOnRearCar(-0.10, -0.20, 0.05, 10000, 500, 20000);
        Assert.Equal(-1100, force, 6);
    }

    [Fact]
    public void ClampsForceToCouplerRating()
    {
        var force = model.CalculateForceOnRearCar(1, 2, 0, 100000, 10000, 5000);
        Assert.Equal(5000, force);
    }

    [Theory]
    [InlineData(-0.01, 0, 100, 100, 100)]
    [InlineData(0, 0, -1, 100, 100)]
    [InlineData(0, 0, 1, double.NaN, 100)]
    public void RejectsInvalidParameters(
        double displacement, double velocity, double slack, double stiffness, double maximum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            model.CalculateForceOnRearCar(displacement, velocity, slack, stiffness, 0, maximum));
    }
}

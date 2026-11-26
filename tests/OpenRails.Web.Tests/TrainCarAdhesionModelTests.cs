using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrainCarAdhesionModelTests
{
    private readonly TrainCarAdhesionModel model = new();

    [Fact]
    public void LimitsPositiveAndNegativeTractiveEffortByVehicleWeight()
    {
        var limit = 0.25 * 10_000 * TrainCarAdhesionModel.StandardGravityMetersPerSecondSquared;
        Assert.Equal(limit, model.LimitTractiveForce(100_000, 10_000), 8);
        Assert.Equal(-limit, model.LimitTractiveForce(-100_000, 10_000), 8);
    }

    [Fact]
    public void PreservesForceBelowAdhesionLimitAndSupportsZeroAdhesion()
    {
        Assert.Equal(5_000, model.LimitTractiveForce(5_000, 10_000));
        Assert.Equal(0, model.LimitTractiveForce(20_000, 10_000, 0));
    }

    [Fact]
    public void RejectsInvalidMassAndAdhesionInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => model.LimitTractiveForce(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => model.LimitTractiveForce(1, 1000, 1.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => model.LimitTractiveForce(double.NaN, 1000));
    }
}

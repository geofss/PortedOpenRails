using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class ElectricMotorThermalModelTests
{
    [Fact]
    public void ZeroTransmissionLossAndCoolingMovesTemperatureTowardZero()
    {
        var model = new ElectricMotorThermalModel(300);
        var result = model.Advance(1, 0, 10, 0.5, 4, 1, 0, 50, 2, 40, 5);
        Assert.InRange(result, 0, 300);
        Assert.Equal(result, model.TemperatureKelvin);
    }

    [Fact]
    public void TransmissionLossHeatsMotorAboveItsInitialTemperature()
    {
        var model = new ElectricMotorThermalModel(273);
        var result = model.Advance(10, 20_000, 10, 0.5, 4, 0.9, 0, 50, 2, 40, 5);
        Assert.True(result > 273);
    }

    [Fact]
    public void CoolingCanOutweighTransmissionLosses()
    {
        var model = new ElectricMotorThermalModel(400);
        var result = model.Advance(5, 1_000, 10, 0.5, 4, 0.95, 5_000, 50, 2, 40, 5);
        Assert.True(result < 400);
    }

    [Fact]
    public void ZeroElapsedTimeDoesNotChangeTemperature()
    {
        var model = new ElectricMotorThermalModel(310);
        Assert.Equal(310, model.Advance(0, 0, 0, 0.5, 4, 0.9, 0, 50, 2, 40, 5));
    }

    [Fact]
    public void RejectsInvalidTransmissionAndThermalParameters()
    {
        var model = new ElectricMotorThermalModel();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            model.Advance(1, 0, 0, 0.5, 0, 0.9, 0, 50, 2, 40, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            model.Advance(1, 0, 0, 0.5, 4, 1.1, 0, 50, 2, 40, 5));
    }
}

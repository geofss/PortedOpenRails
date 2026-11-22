using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrainBrakePipeModelTests
{
    [Fact]
    public void BrakeApplicationPropagatesFromLeadingVehicleToRear()
    {
        var model = new TrainBrakePipeModel();
        var state = model.CreateInitialState(4);

        var applied = model.Advance(state, 100, 0.2);
        var pressures = applied.PressuresKPa;

        Assert.True(pressures[0] < pressures[1]);
        Assert.True(pressures[1] < pressures[2]);
        Assert.True(pressures[2] < pressures[3]);
        Assert.True(pressures[0] < TrainBrakePipeModel.NormalPressureKPa);
        Assert.All(pressures, pressure => Assert.InRange(
            pressure, TrainBrakePipeModel.MinimumServicePressureKPa,
            TrainBrakePipeModel.NormalPressureKPa));
    }

    [Fact]
    public void LongerApplicationBringsRearPressureCloserToCommandedPressure()
    {
        var model = new TrainBrakePipeModel();
        var initial = model.CreateInitialState(5);

        var shortApplication = model.Advance(initial, 75, 0.5);
        var longApplication = model.Advance(initial, 75, 5);

        Assert.True(longApplication.PressuresKPa[^1] < shortApplication.PressuresKPa[^1]);
        Assert.True(longApplication.PressuresKPa[^1] > TrainBrakePipeModel.MinimumServicePressureKPa);
    }

    [Fact]
    public void ReleasingBrakeRestoresPressureGraduallyFromFront()
    {
        var model = new TrainBrakePipeModel();
        var applied = model.Advance(model.CreateInitialState(3), 100, 3);

        var released = model.Advance(applied, 0, 0.2);

        Assert.True(released.PressuresKPa[0] > applied.PressuresKPa[0]);
        Assert.True(released.PressuresKPa[0] > released.PressuresKPa[1]);
    }

    [Fact]
    public void RejectsInvalidPressureStateWhenDerivingBrakeCommands()
    {
        var model = new TrainBrakePipeModel();

        Assert.Throws<ArgumentException>(() => model.GetBrakePercentByCar(
            new TrainBrakePipeState(new[] { double.NaN })));
        Assert.Throws<ArgumentException>(() => model.GetBrakePercentByCar(
            new TrainBrakePipeState(Array.Empty<double>())));
    }

    [Fact]
    public void ConvertsPressureDropToPerCarBrakeCommands()
    {
        var model = new TrainBrakePipeModel();
        var applied = model.Advance(model.CreateInitialState(2), 100, 10);
        var commands = model.GetBrakePercentByCar(applied);

        Assert.All(commands, command => Assert.InRange(command, 99, 100));
    }
}

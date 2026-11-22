using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrainBrakeCylinderModelTests
{
    [Fact]
    public void CylinderPressureBuildsGraduallyAfterBrakePipeCommand()
    {
        var model = new TrainBrakeCylinderModel();
        var initial = model.CreateInitialState(3);
        var command = new[] { 100d, 50d, 0d };

        var shortResponse = model.Advance(initial, command, 0.1);
        var longerResponse = model.Advance(initial, command, 1);

        Assert.True(shortResponse.PressuresKPa[0] > 0);
        Assert.True(shortResponse.PressuresKPa[0] < TrainBrakeCylinderModel.MaximumCylinderPressureKPa);
        Assert.True(longerResponse.PressuresKPa[0] > shortResponse.PressuresKPa[0]);
        Assert.True(shortResponse.PressuresKPa[0] > shortResponse.PressuresKPa[1]);
        Assert.Equal(0, shortResponse.PressuresKPa[2]);
    }

    [Fact]
    public void CylinderPressureExhaustsGraduallyWhenBrakePipeRecharges()
    {
        var model = new TrainBrakeCylinderModel();
        var applied = model.Advance(model.CreateInitialState(2), new[] { 100d, 80d }, 5);

        var released = model.Advance(applied, new[] { 0d, 0d }, 0.1);
        var fullyReleased = model.Advance(applied, new[] { 0d, 0d }, 10);

        Assert.True(released.PressuresKPa[0] < applied.PressuresKPa[0]);
        Assert.True(released.PressuresKPa[0] > 0);
        Assert.True(fullyReleased.PressuresKPa[0] < released.PressuresKPa[0]);
    }

    [Fact]
    public void ConvertsCylinderPressureToPerVehicleBrakePercent()
    {
        var model = new TrainBrakeCylinderModel();
        var state = model.Advance(model.CreateInitialState(2), new[] { 100d, 50d }, 10);

        var commands = model.GetBrakePercentByCar(state);

        Assert.InRange(commands[0], 99, 100);
        Assert.InRange(commands[1], 49, 51);
    }

    [Fact]
    public void RejectsInvalidPressureStateWhenDerivingBrakeCommands()
    {
        var model = new TrainBrakeCylinderModel();

        Assert.Throws<ArgumentException>(() => model.GetBrakePercentByCar(
            new TrainBrakeCylinderState(new[] { double.NaN })));
        Assert.Throws<ArgumentException>(() => model.GetBrakePercentByCar(
            new TrainBrakeCylinderState(Array.Empty<double>())));
    }

    [Fact]
    public void ValidatesStateCommandsAndElapsedTime()
    {
        var model = new TrainBrakeCylinderModel();
        var state = model.CreateInitialState(2);

        Assert.Throws<ArgumentException>(() => model.Advance(state, new[] { 100d }, 0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => model.Advance(state, new[] { 100d, double.NaN }, 0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => model.Advance(state, new[] { 100d, 0d }, -0.1));
        Assert.Throws<ArgumentException>(() => model.Advance(
            new TrainBrakeCylinderState(new[] { -1d }), new[] { 0d }, 0.1));
    }
}

using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrainBrakePipeModelTests
{
    [Fact]
    public void MultiStepAdvanceDoesNotMutateInputPressureSnapshot()
    {
        var model = new TrainBrakePipeModel();
        var initial = model.CreateInitialState(6);
        var originalPressures = initial.PressuresKPa.ToArray();

        var result = model.Advance(initial, 65, 1.25);

        Assert.Equal(originalPressures, initial.PressuresKPa);
        Assert.NotSame(initial.PressuresKPa, result.PressuresKPa);
        Assert.All(result.PressuresKPa, pressure => Assert.InRange(
            pressure, TrainBrakePipeModel.MinimumServicePressureKPa,
            TrainBrakePipeModel.NormalPressureKPa));
        Assert.True(result.PressuresKPa[0] < result.PressuresKPa[^1]);
    }

    [Fact]
    public void EmergencyApplicationVentsBelowServicePressureAndPropagatesRearward()
    {
        var model = new TrainBrakePipeModel();
        var initial = model.CreateInitialState(4);

        var emergency = model.Advance(initial, 100, 0.25, emergency: true);

        Assert.True(emergency.PressuresKPa[0] < TrainBrakePipeModel.MinimumServicePressureKPa);
        Assert.True(emergency.PressuresKPa[0] < emergency.PressuresKPa[1]);
        Assert.True(emergency.PressuresKPa[1] < emergency.PressuresKPa[2]);
        Assert.All(emergency.PressuresKPa, pressure => Assert.InRange(
            pressure, TrainBrakePipeModel.EmergencyPressureKPa,
            TrainBrakePipeModel.NormalPressureKPa));
        var commands = model.GetBrakePercentByCar(emergency);
        Assert.Equal(100, commands[0]);
        Assert.True(commands[0] > commands[^1]);
        Assert.Equal(TrainBrakePipeModel.NormalPressureKPa, initial.PressuresKPa[0]);
    }

    [Fact]
    public void EmergencyBrakePipeRechargesGraduallyAfterEmergencyRelease()
    {
        var model = new TrainBrakePipeModel();
        var emergency = model.Advance(
            model.CreateInitialState(1), 100, 0.25, emergency: true);

        var released = model.Advance(emergency, 0, 0.01);

        Assert.True(released.PressuresKPa[0] > emergency.PressuresKPa[0]);
        Assert.True(released.PressuresKPa[0] < TrainBrakePipeModel.MinimumServicePressureKPa);
    }

    [Fact]
    public void PressureStateSnapshotsDoNotTrackCallerArrayMutations()
    {
        var source = new[] { TrainBrakePipeModel.NormalPressureKPa };
        var state = new TrainBrakePipeState(source);

        source[0] = TrainBrakePipeModel.MinimumServicePressureKPa;

        Assert.Equal(TrainBrakePipeModel.NormalPressureKPa, state.PressuresKPa[0]);
    }

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

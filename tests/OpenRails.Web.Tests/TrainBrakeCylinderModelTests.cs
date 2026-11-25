using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrainBrakeCylinderModelTests
{
    [Fact]
    public void PressureStateSnapshotsDoNotTrackCallerArrayMutations()
    {
        var source = new[] { 100d };
        var state = new TrainBrakeCylinderState(source);

        source[0] = 0;

        Assert.Equal(100, state.PressuresKPa[0]);
    }

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
    [Fact]
    public void UsesExactExponentialRelaxationForApplicationAndRelease()
    {
        var model = new TrainBrakeCylinderModel(
            applicationTimeConstantSeconds: 2,
            releaseTimeConstantSeconds: 4);
        var initial = new TrainBrakeCylinderState(new[] { 100d });

        var applied = model.Advance(initial, new[] { 100d }, 1);
        var expectedApplied = 380 + (100 - 380) * Math.Exp(-1d / 2);
        Assert.Equal(expectedApplied, applied.PressuresKPa[0], precision: 10);

        var released = model.Advance(applied, new[] { 0d }, 1);
        var expectedReleased = expectedApplied * Math.Exp(-1d / 4);
        Assert.Equal(expectedReleased, released.PressuresKPa[0], precision: 10);
    }

    [Fact]
    public void ConstantBrakeCommandIsIndependentOfHowElapsedTimeIsPartitioned()
    {
        var model = new TrainBrakeCylinderModel();
        var initial = new TrainBrakeCylinderState(new[] { 75d });

        var oneUpdate = model.Advance(initial, new[] { 100d }, 1.25);
        var splitUpdates = model.Advance(
            model.Advance(initial, new[] { 100d }, 0.5),
            new[] { 100d }, 0.75);

        Assert.Equal(oneUpdate.PressuresKPa[0], splitUpdates.PressuresKPa[0], precision: 10);

        var oneRelease = model.Advance(oneUpdate, new[] { 0d }, 2);
        var splitRelease = model.Advance(
            model.Advance(oneUpdate, new[] { 0d }, 0.8),
            new[] { 0d }, 1.2);

        Assert.Equal(oneRelease.PressuresKPa[0], splitRelease.PressuresKPa[0], precision: 10);
    }

    [Fact]
    public void VeryLargeFiniteElapsedIntervalReachesCylinderTargetsWithoutTimeStepping()
    {
        var model = new TrainBrakeCylinderModel();
        var initial = model.CreateInitialState(2);

        var applied = model.Advance(initial, new[] { 100d, 50d }, 1e300);
        var released = model.Advance(applied, new[] { 0d, 0d }, 1e300);

        Assert.Equal(TrainBrakeCylinderModel.MaximumCylinderPressureKPa, applied.PressuresKPa[0]);
        Assert.Equal(TrainBrakeCylinderModel.MaximumCylinderPressureKPa * 0.5, applied.PressuresKPa[1]);
        Assert.Equal(0, released.PressuresKPa[0]);
        Assert.Equal(0, released.PressuresKPa[1]);
        Assert.Equal(0, initial.PressuresKPa[0]);
    }

}

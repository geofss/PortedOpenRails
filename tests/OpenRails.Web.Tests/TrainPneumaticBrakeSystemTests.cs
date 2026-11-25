using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrainPneumaticBrakeSystemTests
{
    [Fact]
    public void ServiceApplicationPropagatesThroughPipeBeforeBuildingEachCylinder()
    {
        var brakes = new TrainPneumaticBrakeSystem(4);

        brakes.Advance(100, 0.2);

        Assert.True(brakes.PipeState.PressuresKPa[0] < brakes.PipeState.PressuresKPa[1]);
        Assert.True(brakes.PipeState.PressuresKPa[1] < brakes.PipeState.PressuresKPa[2]);
        Assert.True(brakes.PipeState.PressuresKPa[2] < brakes.PipeState.PressuresKPa[3]);
        Assert.True(brakes.CylinderState.PressuresKPa[0] > brakes.CylinderState.PressuresKPa[^1]);
        Assert.Contains(brakes.CylinderState.PressuresKPa, pressure => pressure > 0);
    }

    [Fact]
    public void EmergencyApplicationProducesStrongerFrontResponseThanServiceApplication()
    {
        var service = new TrainPneumaticBrakeSystem(4);
        var emergency = new TrainPneumaticBrakeSystem(4);

        service.Advance(100, 0.25);
        emergency.Advance(100, 0.25, emergency: true);

        Assert.True(emergency.PipeState.PressuresKPa[0] < service.PipeState.PressuresKPa[0]);
        Assert.True(emergency.CylinderState.PressuresKPa[0] > service.CylinderState.PressuresKPa[0]);
        Assert.True(emergency.BrakePercentByCar[0] > service.BrakePercentByCar[0]);
        Assert.True(emergency.BrakePercentByCar[0] < 100);
        Assert.True(emergency.BrakePercentByCar[0] > emergency.BrakePercentByCar[^1]);
    }

    [Fact]
    public void ReleaseRechargesPipeAndExhaustsCylindersGradually()
    {
        var brakes = new TrainPneumaticBrakeSystem(3);
        brakes.Advance(100, 5);
        var appliedPipe = brakes.PipeState.PressuresKPa.ToArray();
        var appliedCylinders = brakes.CylinderState.PressuresKPa.ToArray();

        brakes.Advance(0, 0.1);

        Assert.True(brakes.PipeState.PressuresKPa[0] > appliedPipe[0]);
        Assert.True(brakes.CylinderState.PressuresKPa[0] < appliedCylinders[0]);
        Assert.True(brakes.CylinderState.PressuresKPa[0] > 0);
    }

    [Fact]
    public void RestoreCopiesBothPressureSnapshotsAndResetReturnsToChargedPipe()
    {
        var brakes = new TrainPneumaticBrakeSystem(2);
        brakes.Advance(80, 1);
        var pipe = new TrainBrakePipeState(new[] { 450d, 460d });
        var cylinders = new TrainBrakeCylinderState(new[] { 120d, 80d });

        brakes.RestoreState(pipe, cylinders);
        brakes.Reset();

        Assert.All(brakes.PipeState.PressuresKPa,
            pressure => Assert.Equal(TrainBrakePipeModel.NormalPressureKPa, pressure));
        Assert.All(brakes.CylinderState.PressuresKPa, pressure => Assert.Equal(0, pressure));
        Assert.Equal(450, pipe.PressuresKPa[0]);
        Assert.Equal(120, cylinders.PressuresKPa[0]);
    }

    [Fact]
    public void InvalidRestoreDoesNotPartiallyChangeLivePneumaticState()
    {
        var brakes = new TrainPneumaticBrakeSystem(2);
        brakes.Advance(75, 0.5);
        var originalPipe = brakes.PipeState.PressuresKPa.ToArray();
        var originalCylinders = brakes.CylinderState.PressuresKPa.ToArray();

        Assert.Throws<ArgumentException>(() => brakes.RestoreState(
            new TrainBrakePipeState(new[] { 400d, double.NaN }),
            new TrainBrakeCylinderState(new[] { 10d, 20d })));
        Assert.Throws<ArgumentException>(() => brakes.RestoreState(
            new TrainBrakePipeState(new[] { 400d, 410d }),
            new TrainBrakeCylinderState(new[] { 10d })));

        Assert.Equal(originalPipe, brakes.PipeState.PressuresKPa);
        Assert.Equal(originalCylinders, brakes.CylinderState.PressuresKPa);
    }

    [Fact]
    public void DistributorTransfersAirFromAuxiliaryReservoirIntoCylinderDuringApplication()
    {
        var model = new TrainBrakeDistributorModel();
        var initial = model.CreateInitialState(2);
        var pipe = new TrainBrakePipeState(new[] { 350d, 350d });
        var cylinders = new TrainBrakeCylinderState(new[] { 0d, 0d });

        var result = model.Advance(initial, pipe, cylinders, 0.5);

        Assert.All(result.Distributor.Modes, mode => Assert.Equal(TrainBrakeDistributorMode.Apply, mode));
        Assert.All(result.Distributor.AuxiliaryReservoirPressuresKPa,
            pressure => Assert.InRange(pressure, 0, TrainBrakePipeModel.NormalPressureKPa));
        Assert.All(result.Cylinders.PressuresKPa, pressure => Assert.True(pressure > 0));
        Assert.True(result.Distributor.AuxiliaryReservoirPressuresKPa[0] < TrainBrakePipeModel.NormalPressureKPa);
    }

    [Fact]
    public void DistributorLapHoldsCylinderAndReservoirPressure()
    {
        var model = new TrainBrakeDistributorModel();
        var state = new TrainBrakeDistributorState(new[] { 500d }, new[] { TrainBrakeDistributorMode.Apply });
        var cylinders = new TrainBrakeCylinderState(new[] { 120d });

        var result = model.Advance(state,
            new TrainBrakePipeState(new[] { 495d }), cylinders, 1);

        Assert.Equal(TrainBrakeDistributorMode.Lap, Assert.Single(result.Distributor.Modes));
        Assert.Equal(500, Assert.Single(result.Distributor.AuxiliaryReservoirPressuresKPa));
        Assert.Equal(120, Assert.Single(result.Cylinders.PressuresKPa));
    }

    [Fact]
    public void DistributorReleaseExhaustsCylinderAndRechargesAuxiliaryReservoir()
    {
        var model = new TrainBrakeDistributorModel();
        var state = new TrainBrakeDistributorState(new[] { 300d }, new[] { TrainBrakeDistributorMode.Apply }, new[] { 300d }, new[] { 300d });
        var cylinders = new TrainBrakeCylinderState(new[] { 180d });

        var result = model.Advance(state,
            new TrainBrakePipeState(new[] { 500d }), cylinders, 1);

        Assert.Equal(TrainBrakeDistributorMode.Release, Assert.Single(result.Distributor.Modes));
        Assert.True(Assert.Single(result.Cylinders.PressuresKPa) < 180);
        Assert.True(Assert.Single(result.Distributor.AuxiliaryReservoirPressuresKPa) > 300);
    }

    [Fact]
    public void PneumaticDistributorStateSurvivesJsonSnapshotRoundTrip()
    {
        var brakes = new TrainPneumaticBrakeSystem(2);
        brakes.Advance(100, 0.5, emergency: true);
        var options = new System.Text.Json.JsonSerializerOptions();
        var serialized = System.Text.Json.JsonSerializer.Serialize(brakes.DistributorState, options);
        var restored = System.Text.Json.JsonSerializer.Deserialize<TrainBrakeDistributorState>(serialized, options);

        Assert.NotNull(restored);
        Assert.Equal(brakes.DistributorState.AuxiliaryReservoirPressuresKPa,
            restored!.AuxiliaryReservoirPressuresKPa);
        Assert.Equal(brakes.DistributorState.Modes, restored.Modes);
        Assert.Equal(brakes.DistributorState.ControlPressuresKPa, restored.ControlPressuresKPa);
        Assert.Equal(brakes.DistributorState.LastPipePressuresKPa, restored.LastPipePressuresKPa);
    }

    [Fact]
    public void DistributorCannotBuildCylinderPressureBeyondAvailableReservoirPressure()
    {
        var model = new TrainBrakeDistributorModel();
        var state = new TrainBrakeDistributorState(
            new[] { 40d },
            new[] { TrainBrakeDistributorMode.Lap },
            new[] { 500d },
            new[] { 500d });

        var result = model.Advance(
            state,
            new TrainBrakePipeState(new[] { 0d }),
            new TrainBrakeCylinderState(new[] { 0d }),
            20);

        Assert.InRange(Assert.Single(result.Cylinders.PressuresKPa), 0, 40);
        Assert.InRange(Assert.Single(result.Distributor.AuxiliaryReservoirPressuresKPa), 0, 0.01);
    }

    [Fact]
    public void DistributorAcceptsNewBrakeApplicationWhilePreviouslyReleasing()
    {
        var model = new TrainBrakeDistributorModel();
        var distributor = model.CreateInitialState(1);
        var cylinders = new TrainBrakeCylinderState(new[] { 0d });

        (distributor, cylinders) = model.Advance(
            distributor, new TrainBrakePipeState(new[] { 350d }), cylinders, 1);
        Assert.True(Assert.Single(cylinders.PressuresKPa) > 0);

        (distributor, cylinders) = model.Advance(
            distributor, new TrainBrakePipeState(new[] { 500d }), cylinders, 8);
        Assert.Equal(TrainBrakeDistributorMode.Lap, Assert.Single(distributor.Modes));
        Assert.True(Assert.Single(cylinders.PressuresKPa) < 1);

        (distributor, cylinders) = model.Advance(
            distributor, new TrainBrakePipeState(new[] { 350d }), cylinders, 0.5);

        Assert.True(Assert.Single(cylinders.PressuresKPa) > 1);
        Assert.Equal(TrainBrakeDistributorMode.Apply, Assert.Single(distributor.Modes));
    }

}

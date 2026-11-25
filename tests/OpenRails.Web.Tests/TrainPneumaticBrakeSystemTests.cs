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
        Assert.True(brakes.CylinderState.PressuresKPa[0] > brakes.CylinderState.PressuresKPa[1]);
        Assert.True(brakes.CylinderState.PressuresKPa[1] > brakes.CylinderState.PressuresKPa[2]);
        Assert.True(brakes.CylinderState.PressuresKPa[2] > brakes.CylinderState.PressuresKPa[3]);
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
}

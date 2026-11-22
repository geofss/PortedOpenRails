using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrainCarForceModelTests
{
    [Fact]
    public void SubtractsRollingBrakeCurveWindAndTunnelResistanceFromDrivingForce()
    {
        var state = new TrainCarMotionState(20_000, 5, 0);
        var forces = new TrainCarForceBreakdown(
            MotiveForceNewtons: 20_000,
            GravityForceNewtons: 0,
            RollingFrictionForceNewtons: 2_000,
            BrakeRetardForceNewtons: 3_000,
            CurveResistanceForceNewtons: 1_000,
            WindResistanceForceNewtons: 500,
            TunnelResistanceForceNewtons: 500);

        var net = new TrainCarForceModel().CalculateNetLongitudinalForce(state, forces);

        Assert.Equal(13_000, net);
    }

    [Fact]
    public void HoldsStationaryVehicleWhenDrivingForceCannotOvercomeResistance()
    {
        var state = new TrainCarMotionState(10_000, 0, 0);
        var forces = new TrainCarForceBreakdown(1_000, 0, 500, 700, 0, 0, 0);

        Assert.Equal(0, new TrainCarForceModel().CalculateNetLongitudinalForce(state, forces));
    }

    [Fact]
    public void ResistanceActsAgainstReverseTravelAndGravityRemainsSigned()
    {
        var model = new TrainCarForceModel();
        var reverseState = new TrainCarMotionState(10_000, -2, 0);
        var resistance = new TrainCarForceBreakdown(0, 0, 500, 0, 0, 0, 0);
        var downhill = new TrainCarForceBreakdown(0, 4_000, 0, 0, 0, 0, 0);

        Assert.Equal(500, model.CalculateNetLongitudinalForce(reverseState, resistance));
        Assert.Equal(4_000, model.CalculateNetLongitudinalForce(reverseState, downhill));
    }

    [Fact]
    public void RejectsNegativeResistanceTerms()
    {
        var state = new TrainCarMotionState(1_000, 0, 0);
        var invalid = new TrainCarForceBreakdown(0, 0, -1, 0, 0, 0, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TrainCarForceModel().CalculateNetLongitudinalForce(state, invalid));
    }
}

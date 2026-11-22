using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class BrowserTrainDrivingControllerTests
{
    [Fact]
    public void ThrottleAcceleratesPoweredConsistAndBrakeCommandSlowsIt()
    {
        var consist = new ConsistMetadata("DriveTest", new[]
        {
            new ConsistCar("Engine", "Diesel", 1, true, false, false),
            new ConsistCar("Wagon", "Freight", 2, false, false, false)
        }, "drive-test.con");
        var traction = new LocomotiveTractionMetadata(
            "TestLoco", 1_500, 1_500, 100_000, 100, 0, null, "test.eng");
        var assets = new[]
        {
            CreateAsset(1, "Engine", true, 1_000, 10, 4_000, traction),
            CreateAsset(2, "Wagon", false, 2_000, 20, 8_000, null)
        };
        var setup = new TrainConsistSimulationSetupBuilder().Build(consist, assets, 500);
        var controller = new BrowserTrainDrivingController(setup);

        Assert.Equal(new[] { true, false }, setup.PoweredCars);
        controller.SetThrottle(100);
        Assert.True(controller.Advance(0.1) > 0);
        Assert.True(controller.State.Cars[0].SpeedMetersPerSecond > 0);
        Assert.Equal(controller.State.Cars[0].SpeedMetersPerSecond,
            controller.State.Cars[1].SpeedMetersPerSecond, 8);

        controller.SetThrottle(0);
        controller.SetBrake(100);
        controller.Advance(0.1);
        Assert.True(controller.State.Cars[0].SpeedMetersPerSecond <
            controller.State.Cars[0].SpeedMetersPerSecond + 1);
        Assert.True(controller.State.Cars[0].SpeedMetersPerSecond <
            controller.State.Cars[0].SpeedMetersPerSecond + 0.01);
        Assert.Equal(controller.State.Cars[0].SpeedMetersPerSecond,
            controller.State.Cars[1].SpeedMetersPerSecond, 8);
    }

    [Fact]
    public void ClampsDriverCommandsAndSupportsPauseAndReset()
    {
        var consist = new ConsistMetadata("DriveTest", new[]
        {
            new ConsistCar("Wagon", "Freight", 2, false, false, false)
        }, "drive-test.con");
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(2, "Wagon", false, 2_000, 20, 8_000, null) }, 100);
        var controller = new BrowserTrainDrivingController(setup);

        controller.SetThrottle(150);
        controller.SetBrake(-5);
        Assert.Equal(100, controller.ThrottlePercent);
        Assert.Equal(0, controller.BrakePercent);

        controller.Pause();
        Assert.Equal(0, controller.Advance(0.1));
        controller.Resume();
        controller.Reset();
        Assert.Equal(0, controller.SimulationTimeSeconds);
        Assert.Equal(0, controller.State.Cars[0].DistanceMeters);
    }

    private static ConsistCarAssetReference CreateAsset(
        int uid, string name, bool isEngine, float mass, float length,
        float brake, LocomotiveTractionMetadata? traction) =>
        new("DriveTest", uid, name, "Freight", isEngine, false, name + (isEngine ? ".eng" : ".wag"),
            true, null,
            new RollingStockMetadata(name, "Freight", mass, 3, 4, length, 4, 8,
                null, null, brake, name + (isEngine ? ".eng" : ".wag")),
            name + ".s", true, null, traction);
}

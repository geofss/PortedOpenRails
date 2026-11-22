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

        var speedBeforeBrake = controller.State.Cars[0].SpeedMetersPerSecond;
        controller.SetThrottle(0);
        controller.SetBrake(100);
        for (var frame = 0; frame < 10; frame++)
            controller.Advance(0.1);
        Assert.True(controller.State.Cars[0].SpeedMetersPerSecond < speedBeforeBrake);
        Assert.Equal(controller.State.Cars[0].SpeedMetersPerSecond,
            controller.State.Cars[1].SpeedMetersPerSecond, 8);
    }

    [Fact]
    public void AppliesRouteGradeToAvailableTraction()
    {
        var consist = new ConsistMetadata("GradeTest", new[]
        {
            new ConsistCar("Engine", "Diesel", 1, true, false, false)
        }, "grade-test.con");
        var traction = new LocomotiveTractionMetadata(
            "TestLoco", 1_500, 1_500, 100_000, 100, 0, null, "test.eng");
        var asset = CreateAsset(1, "Engine", true, 1_000, 10, 4_000, traction);
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { asset }, 200);
        var uphillRoute = CreateSlopeRoute(100, 10);
        var downhillRoute = CreateSlopeRoute(100, -10);
        var uphill = new BrowserTrainDrivingController(setup, route: uphillRoute);
        var downhillSetup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { asset }, 200);
        var downhill = new BrowserTrainDrivingController(downhillSetup, route: downhillRoute);

        uphill.SetThrottle(100);
        downhill.SetThrottle(100);
        uphill.Advance(0.1);
        downhill.Advance(0.1);

        Assert.True(downhill.State.Cars[0].SpeedMetersPerSecond >
            uphill.State.Cars[0].SpeedMetersPerSecond);
    }

    [Fact]
    public void StopsWhenRequestedAccelerationWouldOverloadParsedCoupler()
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
            CreateAsset(1, "Engine", true, 1_000, 10, 4_000, traction, 100),
            CreateAsset(2, "Wagon", false, 2_000, 20, 8_000, null, 100)
        };
        var setup = new TrainConsistSimulationSetupBuilder().Build(consist, assets, 500);
        var controller = new BrowserTrainDrivingController(setup);
        controller.SetThrottle(100);

        var error = Assert.Throws<InvalidOperationException>(() => controller.Advance(0.1));
        Assert.Contains("exceeds its rating", error.Message);
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
        Assert.Equal(10, controller.State.Cars[0].DistanceMeters);
    }

    private static TrackNetworkRoute CreateSlopeRoute(double length, double rise)
    {
        var start = new TrackWorldPosition(0, 0, 0);
        var end = new TrackWorldPosition(0, rise, length);
        var source = new TrackVectorSection(42, 0, 0, 0, 0, 0, 1, "00",
            0, 0, 0, 0, 0, 0, 0, 0);
        var geometry = new TrackSectionGeometry(42, null, (float)length, null, null, false);
        var section = new TrackSectionPlacement(source, geometry, start, length);
        var path = new TrackVectorPath(1, new[] { 2, 3 }, new[] { section }, length, end);
        return new TrackNetworkRoute(
            new[] { new TrackNetworkRouteSegment(path, false) },
            Array.Empty<string>(), length);
    }

    private static ConsistCarAssetReference CreateAsset(
        int uid, string name, bool isEngine, float mass, float length,
        float brake, LocomotiveTractionMetadata? traction, float? couplerStrength = null) =>
        new("DriveTest", uid, name, "Freight", isEngine, false, name + (isEngine ? ".eng" : ".wag"),
            true, null,
            new RollingStockMetadata(name, "Freight", mass, 3, 4, length, 4, 8,
                null, null, brake, name + (isEngine ? ".eng" : ".wag"),
                MinCouplerStrengthN: couplerStrength),
            name + ".s", true, null, traction);
}

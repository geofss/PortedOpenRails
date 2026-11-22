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
        Assert.NotEqual(controller.State.Cars[0].SpeedMetersPerSecond,
            controller.State.Cars[1].SpeedMetersPerSecond);

        var speedBeforeBrake = controller.State.Cars[0].SpeedMetersPerSecond;
        controller.SetThrottle(0);
        controller.SetBrake(100);
        for (var frame = 0; frame < 10; frame++)
            controller.Advance(0.1);
        Assert.True(controller.State.Cars[0].SpeedMetersPerSecond < speedBeforeBrake);

    }

    [Fact]
    public void PausedControllerFreezesBrakePipeAndCylinderSubsystems()
    {
        var consist = new ConsistMetadata("PausedBrake", new[]
        {
            new ConsistCar("Wagon", "Freight", 1, false, false, false)
        }, "paused-brake.con");
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(1, "Wagon", false, 2_000, 20, 8_000, null) }, 200);
        var controller = new BrowserTrainDrivingController(setup);

        controller.SetBrake(100);
        controller.Pause();
        var pipeBefore = controller.BrakePipeState;
        var cylinderBefore = controller.BrakeCylinderState;
        var timeBefore = controller.SimulationTimeSeconds;

        Assert.Equal(0, controller.Advance(0.5));

        Assert.Equal(pipeBefore, controller.BrakePipeState);
        Assert.Equal(cylinderBefore, controller.BrakeCylinderState);
        Assert.Equal(timeBefore, controller.SimulationTimeSeconds);
    }

    [Fact]
    public void RejectsInvalidElapsedTimeBeforeAdvancingBrakeSubsystems()
    {
        var consist = new ConsistMetadata("InvalidElapsed", new[]
        {
            new ConsistCar("Wagon", "Freight", 1, false, false, false)
        }, "invalid-elapsed.con");
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(1, "Wagon", false, 2_000, 20, 8_000, null) }, 200);
        var controller = new BrowserTrainDrivingController(setup);
        controller.SetBrake(100);
        var pipeBefore = controller.BrakePipeState;
        var cylinderBefore = controller.BrakeCylinderState;

        Assert.Throws<ArgumentOutOfRangeException>(() => controller.Advance(double.NaN));

        Assert.Equal(pipeBefore, controller.BrakePipeState);
        Assert.Equal(cylinderBefore, controller.BrakeCylinderState);
    }

    [Fact]
    public void ReportsOneCouplerForceReadingPerAdjacentVehicleBoundary()
    {
        var consist = new ConsistMetadata("ThreeCarTelemetry", new[]
        {
            new ConsistCar("Engine", "Diesel", 1, true, false, false),
            new ConsistCar("Wagon A", "Freight", 2, false, false, false),
            new ConsistCar("Wagon B", "Freight", 3, false, false, false)
        }, "three-car-telemetry.con");
        var traction = new LocomotiveTractionMetadata(
            "TestLoco", 1_500, 1_500, 100_000, 100, 0, null, "test.eng");
        var assets = new[]
        {
            CreateAsset(1, "Engine", true, 1_000, 10, 4_000, traction),
            CreateAsset(2, "Wagon A", false, 2_000, 20, 8_000, null),
            CreateAsset(3, "Wagon B", false, 2_000, 20, 8_000, null)
        };
        var setup = new TrainConsistSimulationSetupBuilder().Build(consist, assets, 500);
        var controller = new BrowserTrainDrivingController(setup);

        controller.SetThrottle(100);
        controller.Advance(0.1);

        Assert.Equal(2, controller.CouplerForceTelemetryNewtons.Count);
    }

    [Fact]
    public void PneumaticBrakesAdvanceOnlyForFixedStepsActuallySimulated()
    {
        var consist = new ConsistMetadata("DroppedBrakeTime", new[]
        {
            new ConsistCar("Wagon", "Freight", 1, false, false, false)
        }, "dropped-brake-time.con");
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(1, "Wagon", false, 2_000, 20, 8_000, null) }, 200);
        var controller = new BrowserTrainDrivingController(setup);
        controller.SetBrake(100);

        var executedSteps = controller.Advance(0.25);
        var simulatedSeconds = 8d / 60d;
        var expectedPipe = new TrainBrakePipeModel().Advance(
            new TrainBrakePipeModel().CreateInitialState(1), 100, simulatedSeconds);
        var expectedCylinderModel = new TrainBrakeCylinderModel();
        var expectedCylinder = expectedCylinderModel.Advance(
            expectedCylinderModel.CreateInitialState(1),
            new TrainBrakePipeModel().GetBrakePercentByCar(expectedPipe),
            simulatedSeconds);

        Assert.Equal(8, executedSteps);
        Assert.Equal(simulatedSeconds, controller.SimulationTimeSeconds, 10);
        Assert.Equal(expectedPipe.PressuresKPa[0], controller.BrakePipeState.PressuresKPa[0], 10);
        Assert.Equal(expectedCylinder.PressuresKPa[0], controller.BrakeCylinderState.PressuresKPa[0], 10);
    }

    [Fact]
    public void RouteGradeTelemetryIsEmptyWhenNoRouteIsAttached()
    {
        var consist = new ConsistMetadata("NoRouteGrade", new[]
        {
            new ConsistCar("Wagon", "Freight", 1, false, false, false)
        }, "no-route-grade.con");
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(1, "Wagon", false, 2_000, 20, 8_000, null) }, 200);
        var controller = new BrowserTrainDrivingController(setup);

        Assert.Empty(controller.RouteGradePercentByCar);
        Assert.Null(controller.LeadCarRouteGradePercent);
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
        Assert.NotNull(uphill.LeadCarRouteGradePercent);
        Assert.NotNull(downhill.LeadCarRouteGradePercent);
        Assert.True(uphill.LeadCarRouteGradePercent < 0);
        Assert.True(downhill.LeadCarRouteGradePercent > 0);
        Assert.InRange(uphill.LeadCarRouteGradePercent!.Value, -10.1, -9.9);
        Assert.InRange(downhill.LeadCarRouteGradePercent!.Value, 9.9, 10.1);
        Assert.Single(uphill.RouteGradePercentByCar);
        Assert.Single(downhill.RouteGradePercentByCar);
        Assert.InRange(uphill.RouteGradePercentByCar[0], -10.1, -9.9);
        Assert.InRange(downhill.RouteGradePercentByCar[0], 9.9, 10.1);
    }

    [Fact]
    public void ReportsRouteGradeForEveryCarInMultiCarConsist()
    {
        var consist = new ConsistMetadata("GradeConsist", new[]
        {
            new ConsistCar("Engine", "Diesel", 1, true, false, false),
            new ConsistCar("Wagon", "Freight", 2, false, false, false)
        }, "grade-consist.con");
        var traction = new LocomotiveTractionMetadata(
            "TestLoco", 1_500, 1_500, 100_000, 100, 0, null, "test.eng");
        var assets = new[]
        {
            CreateAsset(1, "Engine", true, 1_000, 10, 4_000, traction),
            CreateAsset(2, "Wagon", false, 2_000, 20, 8_000, null)
        };
        var setup = new TrainConsistSimulationSetupBuilder().Build(consist, assets, 200);
        var controller = new BrowserTrainDrivingController(
            setup, route: CreateSlopeRoute(100, 10));

        Assert.Equal(2, controller.RouteGradePercentByCar.Count);
        // Positive elevation gain is uphill, so the route-grade convention reports -10%.
        Assert.All(controller.RouteGradePercentByCar,
            grade => Assert.InRange(grade, -10.1, -9.9));
        Assert.Equal(controller.RouteGradePercentByCar[0],
            controller.LeadCarRouteGradePercent);
    }

    [Fact]
    public void AppliesParsedDavisResistanceToVehicleAcceleration()
    {
        var consist = new ConsistMetadata("DriveTest", new[]
        {
            new ConsistCar("Engine", "Diesel", 1, true, false, false)
        }, "davis-drive.con");
        var traction = new LocomotiveTractionMetadata(
            "TestLoco", 1_500, 1_500, 100_000, 100, 0, null, "test.eng");
        var lowResistanceAsset = CreateAsset(1, "Engine", true, 1_000, 10, 4_000, traction);
        var highResistanceAsset = CreateAsset(1, "Engine", true, 1_000, 10, 4_000, traction,
            davisA: 1_000);
        var lowResistanceSetup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { lowResistanceAsset }, 500);
        var highResistanceSetup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { highResistanceAsset }, 500);
        var lowResistance = new BrowserTrainDrivingController(lowResistanceSetup);
        var highResistance = new BrowserTrainDrivingController(highResistanceSetup);

        lowResistance.SetThrottle(100);
        highResistance.SetThrottle(100);
        lowResistance.Advance(0.1);
        highResistance.Advance(0.1);

        Assert.True(lowResistance.State.Cars[0].SpeedMetersPerSecond >
            highResistance.State.Cars[0].SpeedMetersPerSecond);
    }

    [Fact]
    public void BuildsBrakeCylinderPressureGraduallyAndClearsItOnReset()
    {
        var consist = new ConsistMetadata("DriveTest", new[]
        {
            new ConsistCar("Engine", "Diesel", 1, true, false, false)
        }, "drive-test.con");
        var traction = new LocomotiveTractionMetadata(
            "TestLoco", 1_500, 1_500, 100_000, 100, 0, null, "test.eng");
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(1, "Engine", true, 1_000, 10, 4_000, traction) }, 500);
        var controller = new BrowserTrainDrivingController(setup);

        controller.SetBrake(100);
        controller.Advance(0.1);

        var appliedPressure = controller.BrakeCylinderState.PressuresKPa[0];
        Assert.True(appliedPressure > 0);
        Assert.True(appliedPressure < TrainBrakeCylinderModel.MaximumCylinderPressureKPa);

        controller.Reset();

        Assert.Equal(0, controller.BrakeCylinderState.PressuresKPa[0]);
        Assert.Equal(TrainBrakePipeModel.NormalPressureKPa,
            controller.BrakePipeState.PressuresKPa[0]);
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

        controller.Advance(0.1);
        Assert.All(controller.CouplerForceTelemetryNewtons, force => Assert.InRange(Math.Abs(force), 0, 100));
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
        float brake, LocomotiveTractionMetadata? traction, float? couplerStrength = null,
        float? davisA = null, float? davisB = null, float? davisC = null) =>
        new("DriveTest", uid, name, "Freight", isEngine, false, name + (isEngine ? ".eng" : ".wag"),
            true, null,
            new RollingStockMetadata(name, "Freight", mass, 3, 4, length, 4, 8,
                null, null, brake, name + (isEngine ? ".eng" : ".wag"),
                MinCouplerStrengthN: couplerStrength,
                DavisAForceN: davisA, DavisBResistance: davisB, DavisCResistance: davisC),
            name + ".s", true, null, traction);
}

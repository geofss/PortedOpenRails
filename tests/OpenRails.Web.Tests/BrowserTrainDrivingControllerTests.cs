using System.Text.Json;
using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class BrowserTrainDrivingControllerTests
{

    [Fact]
    public void EmergencyStopRemainsLatchedAgainstThrottleAndBrakeInputsUntilReleased()
    {
        var consist = new ConsistMetadata("EmergencyLatch", new[]
        {
            new ConsistCar("Engine", "Diesel", 1, true, false, false)
        }, "emergency-latch.con");
        var traction = new LocomotiveTractionMetadata(
            "TestLoco", 1_500, 1_500, 100_000, 100, 0, null, "test.eng");
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(1, "Engine", true, 1_000, 10, 4_000, traction) }, 500);
        var controller = new BrowserTrainDrivingController(setup);

        controller.SetThrottle(65);
        controller.ApplyEmergencyStop();
        Assert.True(controller.IsEmergencyStopApplied);
        Assert.Equal(0, controller.ThrottlePercent);
        Assert.Equal(100, controller.BrakePercent);

        controller.SetThrottle(90);
        controller.SetBrake(0);
        Assert.Equal(0, controller.ThrottlePercent);
        Assert.Equal(100, controller.BrakePercent);
        controller.Advance(0.25);
        controller.Advance(0.25);
        Assert.True(controller.BrakePipeState.PressuresKPa[0] <
            TrainBrakePipeModel.MinimumServicePressureKPa);
        Assert.True(controller.BrakeCylinderState.PressuresKPa[0] > 0);

        controller.ReleaseEmergencyStop();
        Assert.False(controller.IsEmergencyStopApplied);
        Assert.Equal(0, controller.ThrottlePercent);
        Assert.Equal(100, controller.BrakePercent);
        controller.SetBrake(0);
        controller.SetThrottle(10);
        Assert.Equal(0, controller.BrakePercent);
        Assert.Equal(10, controller.ThrottlePercent);
    }

    [Fact]
    public void BrowserControllerSnapshotSurvivesJsonPersistenceRoundTrip()
    {
        var consist = new ConsistMetadata("JsonSnapshot", new[]
        {
            new ConsistCar("Wagon", "Freight", 1, false, false, false)
        }, "json-snapshot.con");
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(1, "Wagon", false, 2_000, 20, 8_000, null) }, 200);
        var source = new BrowserTrainDrivingController(setup);
        source.SetBrake(42);
        source.SetReverser(TrainReverserPosition.Neutral);
        source.Advance(0.2);
        var json = JsonSerializer.Serialize(source.CaptureState());
        var restoredSnapshot = JsonSerializer.Deserialize<BrowserTrainDrivingControllerSnapshot>(json);
        Assert.NotNull(restoredSnapshot);

        var targetSetup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(1, "Wagon", false, 2_000, 20, 8_000, null) }, 200);
        var target = new BrowserTrainDrivingController(targetSetup);
        target.RestoreState(restoredSnapshot!);

        Assert.Equal(source.State.Cars, target.State.Cars);
        Assert.Equal(source.SimulationTimeSeconds, target.SimulationTimeSeconds, 8);
        Assert.Equal(source.BrakePercent, target.BrakePercent);
        Assert.Equal(source.ReverserPosition, target.ReverserPosition);
        Assert.Equal(source.BrakePipeState.PressuresKPa, target.BrakePipeState.PressuresKPa);
        Assert.Equal(source.BrakeCylinderState.PressuresKPa, target.BrakeCylinderState.PressuresKPa);
        Assert.Equal(source.CouplerForceTelemetryNewtons, target.CouplerForceTelemetryNewtons);
    }

    [Fact]
    public void BrowserControllerPneumaticStateRoundTripsThroughSessionSnapshot()
    {
        var consist = new ConsistMetadata("BrakeSnapshot", new[]
        {
            new ConsistCar("Wagon", "Freight", 1, false, false, false)
        }, "brake-snapshot.con");
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(1, "Wagon", false, 2_000, 20, 8_000, null) }, 200);
        var controller = new BrowserTrainDrivingController(setup);

        controller.SetBrake(100);
        controller.Advance(0.3);
        var snapshot = setup.Session.CaptureState();
        var expectedPipe = snapshot.BrakePipeState!.PressuresKPa.ToArray();
        var expectedCylinder = snapshot.BrakeCylinderState!.PressuresKPa.ToArray();

        controller.SetBrake(0);
        controller.Advance(0.3);
        setup.Session.RestoreState(snapshot);

        Assert.Equal(expectedPipe, controller.BrakePipeState.PressuresKPa);
        Assert.Equal(expectedCylinder, controller.BrakeCylinderState.PressuresKPa);
    }

    [Fact]
    public void ControllerSnapshotRestoresDriverCommandsAndSessionTogether()
    {
        var consist = new ConsistMetadata("ControllerSnapshot", new[]
        {
            new ConsistCar("Wagon", "Freight", 1, false, false, false)
        }, "controller-snapshot.con");
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(1, "Wagon", false, 2_000, 20, 8_000, null) }, 200);
        var controller = new BrowserTrainDrivingController(setup);

        controller.SetThrottle(0);
        controller.SetBrake(35);
        controller.SetReverser(TrainReverserPosition.Reverse);
        controller.SetDoors(true, false);
        controller.Advance(0.3);
        var snapshot = controller.CaptureState();
        var expectedTime = controller.SimulationTimeSeconds;
        var expectedPipe = controller.BrakePipeState.PressuresKPa.ToArray();

        controller.SetDoors(false, false);
        controller.SetBrake(0);
        controller.SetReverser(TrainReverserPosition.Forward);
        controller.RestoreState(snapshot);

        Assert.Equal(35, controller.BrakePercent);
        Assert.Equal(TrainReverserPosition.Reverse, controller.ReverserPosition);
        Assert.True(controller.LeftDoorsOpen);
        Assert.False(controller.RightDoorsOpen);
        Assert.Equal(expectedTime, controller.SimulationTimeSeconds);
        Assert.Equal(expectedPipe, controller.BrakePipeState.PressuresKPa);
    }

    [Fact]
    public void InvalidControllerSnapshotDoesNotPartiallyRestoreLiveState()
    {
        var consist = new ConsistMetadata("InvalidControllerSnapshot", new[]
        {
            new ConsistCar("Wagon", "Freight", 1, false, false, false)
        }, "invalid-controller-snapshot.con");
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(1, "Wagon", false, 2_000, 20, 8_000, null) }, 200);
        var controller = new BrowserTrainDrivingController(setup);
        var snapshot = controller.CaptureState() with { ThrottlePercent = 101 };

        controller.SetThrottle(12);
        var timeBeforeRestore = controller.SimulationTimeSeconds;
        var motionBeforeRestore = controller.State.Cars.ToArray();

        Assert.Throws<ArgumentException>(() => controller.RestoreState(snapshot));
        Assert.Equal(12, controller.ThrottlePercent);
        Assert.Equal(timeBeforeRestore, controller.SimulationTimeSeconds);
        Assert.Equal(motionBeforeRestore, controller.State.Cars);
    }

    [Fact]
    public void RejectsSnapshotForDifferentConsistSizeWithoutChangingLiveState()
    {
        var consist = new ConsistMetadata("ConsistSizeSnapshot", new[]
        {
            new ConsistCar("Wagon", "Freight", 1, false, false, false)
        }, "consist-size-snapshot.con");
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(1, "Wagon", false, 2_000, 20, 8_000, null) }, 200);
        var controller = new BrowserTrainDrivingController(setup);
        var captured = controller.CaptureState();
        var invalidSnapshot = captured with
        {
            SessionState = captured.SessionState with
            {
                MotionState = new TrainConsistMotionState(Array.Empty<TrainCarMotionState>())
            }
        };
        controller.SetBrake(25);
        var timeBeforeRestore = controller.SimulationTimeSeconds;
        var motionBeforeRestore = controller.State.Cars.ToArray();

        Assert.Throws<ArgumentException>(() => controller.RestoreState(invalidSnapshot));
        Assert.Equal(25, controller.BrakePercent);
        Assert.Equal(timeBeforeRestore, controller.SimulationTimeSeconds);
        Assert.Equal(motionBeforeRestore, controller.State.Cars);
    }

    [Fact]
    public void RejectsSnapshotWithPassengerDoorsOpenWhileConsistIsMoving()
    {
        var consist = new ConsistMetadata("MovingDoorSnapshot", new[]
        {
            new ConsistCar("Wagon", "Freight", 1, false, false, false)
        }, "moving-door-snapshot.con");
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(1, "Wagon", false, 2_000, 20, 8_000, null) }, 200,
            initialSpeedMetersPerSecond: 1.0);
        var controller = new BrowserTrainDrivingController(setup);
        var invalidSnapshot = controller.CaptureState() with { LeftDoorsOpen = true };
        var motionBeforeRestore = controller.State.Cars.ToArray();
        var timeBeforeRestore = controller.SimulationTimeSeconds;

        Assert.Throws<ArgumentException>(() => controller.RestoreState(invalidSnapshot));
        Assert.False(controller.LeftDoorsOpen);
        Assert.Equal(motionBeforeRestore, controller.State.Cars);
        Assert.Equal(timeBeforeRestore, controller.SimulationTimeSeconds);
    }

    [Fact]
    public void DoorControlsAreStoppedTrainInterlockedAndPreventTraction()
    {
        var consist = new ConsistMetadata("DoorInterlock", new[]
        {
            new ConsistCar("Engine", "Diesel", 1, true, false, false)
        }, "door-interlock.con");
        var traction = new LocomotiveTractionMetadata(
            "TestLoco", 1_500, 1_500, 100_000, 100, 0, null, "test.eng");
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(1, "Engine", true, 1_000, 10, 4_000, traction) }, 500);
        var controller = new BrowserTrainDrivingController(setup);

        controller.SetThrottle(50);
        Assert.Throws<InvalidOperationException>(() => controller.SetDoors(true, false));
        controller.SetThrottle(0);
        controller.SetDoors(true, false);
        Assert.True(controller.LeftDoorsOpen);
        Assert.False(controller.RightDoorsOpen);
        controller.SetThrottle(75);
        Assert.Equal(0, controller.ThrottlePercent);

        controller.SetDoors(false, false);
        controller.SetThrottle(50);
        controller.Advance(0.1);
        Assert.Throws<InvalidOperationException>(() => controller.SetDoors(false, true));
    }

    [Fact]
    public void ResetClosesBothDoorSides()
    {
        var consist = new ConsistMetadata("DoorReset", new[]
        {
            new ConsistCar("Wagon", "Freight", 1, false, false, false)
        }, "door-reset.con");
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(1, "Wagon", false, 2_000, 20, 8_000, null) }, 200);
        var controller = new BrowserTrainDrivingController(setup);
        controller.SetDoors(true, true);
        controller.Reset();
        Assert.False(controller.LeftDoorsOpen);
        Assert.False(controller.RightDoorsOpen);
    }

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
    public void ReverserSelectsForwardNeutralAndReverseTractionDirection()
    {
        static BrowserTrainDrivingController CreateController()
        {
            var consist = new ConsistMetadata("DriveTest", new[]
            {
                new ConsistCar("Engine", "Diesel", 1, true, false, false)
            }, "reverser-test.con");
            var traction = new LocomotiveTractionMetadata(
                "TestLoco", 1_500, 1_500, 100_000, 100, 0, null, "test.eng");
            var setup = new TrainConsistSimulationSetupBuilder().Build(
                consist, new[] { CreateAsset(1, "Engine", true, 1_000, 10, 4_000, traction) }, 500);
            return new BrowserTrainDrivingController(setup);
        }

        var forward = CreateController();
        forward.SetThrottle(100);
        forward.Advance(0.1);
        Assert.True(forward.State.Cars[0].SpeedMetersPerSecond > 0);

        var reverse = CreateController();
        reverse.SetReverser(TrainReverserPosition.Reverse);
        reverse.SetThrottle(100);
        reverse.Advance(0.1);
        Assert.True(reverse.State.Cars[0].SpeedMetersPerSecond < 0);
        Assert.True(reverse.State.Cars[0].DistanceMeters < 5);

        var neutral = CreateController();
        neutral.SetReverser(TrainReverserPosition.Neutral);
        neutral.SetThrottle(100);
        neutral.Advance(0.1);
        Assert.Equal(0, neutral.State.Cars[0].SpeedMetersPerSecond);
        Assert.Equal(5, neutral.State.Cars[0].DistanceMeters);
    }

    [Fact]
    public void PreventsChangingReverserWhileConsistIsMoving()
    {
        var consist = new ConsistMetadata("DriveTest", new[]
        {
            new ConsistCar("Engine", "Diesel", 1, true, false, false)
        }, "reverser-interlock.con");
        var traction = new LocomotiveTractionMetadata(
            "TestLoco", 1_500, 1_500, 100_000, 100, 0, null, "test.eng");
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(1, "Engine", true, 1_000, 10, 4_000, traction) }, 500);
        var controller = new BrowserTrainDrivingController(setup);

        Assert.True(controller.CanChangeReverser);
        controller.SetThrottle(100);
        controller.Advance(0.1);

        Assert.False(controller.CanChangeReverser);
        Assert.Throws<InvalidOperationException>(() =>
            controller.SetReverser(TrainReverserPosition.Reverse));
        Assert.Equal(TrainReverserPosition.Forward, controller.ReverserPosition);
    }

    [Fact]
    public void ReverserInterlockRequiresThrottleCutEvenWhenTrainIsStopped()
    {
        var consist = new ConsistMetadata("DriveTest", new[]
        {
            new ConsistCar("Engine", "Diesel", 1, true, false, false)
        }, "reverser-throttle-interlock.con");
        var traction = new LocomotiveTractionMetadata(
            "TestLoco", 1_500, 1_500, 100_000, 100, 0, null, "test.eng");
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(1, "Engine", true, 1_000, 10, 4_000, traction) }, 500);
        var controller = new BrowserTrainDrivingController(setup);

        controller.SetThrottle(25);
        Assert.False(controller.CanChangeReverser);
        Assert.Throws<InvalidOperationException>(() =>
            controller.SetReverser(TrainReverserPosition.Reverse));

        controller.SetThrottle(0);
        Assert.True(controller.CanChangeReverser);
        controller.SetReverser(TrainReverserPosition.Reverse);
        Assert.Equal(TrainReverserPosition.Reverse, controller.ReverserPosition);
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

        Assert.Equal(2, controller.CouplerForceTelemetryNewtons.Count);
        Assert.All(controller.CouplerForceTelemetryNewtons, force => Assert.Equal(0, force));

        controller.SetThrottle(100);
        controller.Advance(0.1);

        Assert.Equal(2, controller.CouplerForceTelemetryNewtons.Count);
    }

    [Fact]
    public void PneumaticBrakesWaitForTheFirstCompletedFixedStep()
    {
        var consist = new ConsistMetadata("SubstepBrake", new[]
        {
            new ConsistCar("Wagon", "Freight", 1, false, false, false)
        }, "substep-brake.con");
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(1, "Wagon", false, 2_000, 20, 8_000, null) }, 200);
        var controller = new BrowserTrainDrivingController(setup);
        controller.SetBrake(100);
        var initialPipe = controller.BrakePipeState;
        var initialCylinder = controller.BrakeCylinderState;

        Assert.Equal(0, controller.Advance(1d / 120d));
        Assert.Equal(initialPipe.PressuresKPa[0], controller.BrakePipeState.PressuresKPa[0]);
        Assert.Equal(initialCylinder.PressuresKPa[0], controller.BrakeCylinderState.PressuresKPa[0]);

        Assert.Equal(1, controller.Advance(1d / 120d));
        Assert.True(controller.BrakePipeState.PressuresKPa[0] < initialPipe.PressuresKPa[0]);
        Assert.True(controller.BrakeCylinderState.PressuresKPa[0] > initialCylinder.PressuresKPa[0]);
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
        var expectedBrakeSystem = new TrainPneumaticBrakeSystem(1);
        for (var step = 0; step < executedSteps; step++)
            expectedBrakeSystem.Advance(100, 1d / 60d);

        Assert.Equal(8, executedSteps);
        Assert.Equal(simulatedSeconds, controller.SimulationTimeSeconds, 10);
        Assert.Equal(0.25 - simulatedSeconds, controller.DroppedTimeSeconds, 10);
        Assert.Equal(expectedBrakeSystem.PipeState.PressuresKPa[0], controller.BrakePipeState.PressuresKPa[0], 10);
        Assert.Equal(expectedBrakeSystem.CylinderState.PressuresKPa[0], controller.BrakeCylinderState.PressuresKPa[0], 10);
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

        controller.ApplyEmergencyStop();
        Assert.True(controller.IsEmergencyStopApplied);
        controller.Reset();

        Assert.False(controller.IsEmergencyStopApplied);
        Assert.Equal(0, controller.ThrottlePercent);
        Assert.Equal(0, controller.BrakePercent);
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
    public void EmergencyStopDrivesBrakePipeVentAndCylinderResponse()
    {
        var consist = new ConsistMetadata("EmergencyPipe", new[]
        {
            new ConsistCar("Engine", "Diesel", 1, true, false, false),
            new ConsistCar("Wagon", "Freight", 2, false, false, false)
        }, "emergency-pipe.con");
        var traction = new LocomotiveTractionMetadata(
            "TestLoco", 1_500, 1_500, 100_000, 100, 0, null, "test.eng");
        var assets = new[]
        {
            CreateAsset(1, "Engine", true, 1_000, 10, 4_000, traction),
            CreateAsset(2, "Wagon", false, 2_000, 20, 8_000, null)
        };
        var setup = new TrainConsistSimulationSetupBuilder().Build(consist, assets, 500);
        var controller = new BrowserTrainDrivingController(setup);

        controller.ApplyEmergencyStop();
        for (var frame = 0; frame < 10; frame++)
            controller.Advance(0.1);

        Assert.True(controller.BrakePipeState.PressuresKPa[0] <
            TrainBrakePipeModel.MinimumServicePressureKPa);
        Assert.True(controller.BrakePipeState.PressuresKPa[0] <
            controller.BrakePipeState.PressuresKPa[1]);
        Assert.True(controller.BrakeCylinderState.PressuresKPa[0] > 0);
        Assert.True(controller.BrakeCylinderState.PressuresKPa[1] > 0);
        Assert.True(controller.BrakeCylinderState.PressuresKPa[0] >
            controller.BrakeCylinderState.PressuresKPa[1]);
        Assert.Equal(100, controller.BrakePercent);
        Assert.True(controller.IsEmergencyStopApplied);
    }

    [Fact]
    public void EmergencyStopCutsTractionAndAppliesFullBrake()
    {
        var consist = new ConsistMetadata("EmergencyStop", new[]
        {
            new ConsistCar("Engine", "Diesel", 1, true, false, false)
        }, "emergency-stop.con");
        var traction = new LocomotiveTractionMetadata(
            "TestLoco", 1_500, 1_500, 100_000, 100, 0, null, "test.eng");
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(1, "Engine", true, 1_000, 10, 4_000, traction) }, 500);
        var controller = new BrowserTrainDrivingController(setup);
        controller.SetThrottle(85);
        controller.SetBrake(10);

        controller.ApplyEmergencyStop();

        Assert.Equal(0, controller.ThrottlePercent);
        Assert.Equal(100, controller.BrakePercent);
    }

    [Fact]
    public void ClampsDriverCommandsAndSupportsPauseAndReset()
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
        var setup = new TrainConsistSimulationSetupBuilder().Build(consist, assets, 100);
        var controller = new BrowserTrainDrivingController(setup);
        var initialDistances = setup.InitialState.Cars.Select(car => car.DistanceMeters).ToArray();

        controller.SetThrottle(150);
        controller.SetBrake(-5);
        Assert.Equal(100, controller.ThrottlePercent);
        Assert.Equal(0, controller.BrakePercent);

        controller.Advance(0.1);
        Assert.Single(controller.CouplerForceTelemetryNewtons);

        controller.Pause();
        var frozenTime = controller.SimulationTimeSeconds;
        Assert.Equal(0, controller.Advance(0.1));
        Assert.Equal(frozenTime, controller.SimulationTimeSeconds);
        controller.Resume();
        controller.Reset();

        Assert.Equal(0, controller.SimulationTimeSeconds);
        Assert.Equal(initialDistances, controller.State.Cars.Select(car => car.DistanceMeters));
        Assert.Equal(0, controller.ThrottlePercent);
        Assert.Equal(0, controller.BrakePercent);
        Assert.Equal(TrainReverserPosition.Forward, controller.ReverserPosition);
        Assert.Single(controller.CouplerForceTelemetryNewtons);
        Assert.Equal(0, controller.CouplerForceTelemetryNewtons[0]);
    }

    [Fact]
    public void EmergencyStopLatchRejectsDriverCommandsUntilExplicitRelease()
    {
        var consist = new ConsistMetadata("EmergencyStopLatch", new[]
        {
            new ConsistCar("Engine", "Diesel", 1, true, false, false)
        }, "emergency-stop-latch.con");
        var traction = new LocomotiveTractionMetadata(
            "TestLoco", 1_500, 1_500, 100_000, 100, 0, null, "test.eng");
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { CreateAsset(1, "Engine", true, 1_000, 10, 4_000, traction) }, 500);
        var controller = new BrowserTrainDrivingController(setup);

        controller.ApplyEmergencyStop();
        controller.SetThrottle(75);
        controller.SetBrake(25);
        Assert.Equal(0, controller.ThrottlePercent);
        Assert.Equal(100, controller.BrakePercent);
        Assert.True(controller.IsEmergencyStopApplied);

        controller.ReleaseEmergencyStop();
        Assert.False(controller.IsEmergencyStopApplied);
        Assert.Equal(0, controller.ThrottlePercent);
        Assert.Equal(100, controller.BrakePercent);
        controller.SetBrake(25);
        controller.SetThrottle(75);
        Assert.Equal(25, controller.BrakePercent);
        Assert.Equal(75, controller.ThrottlePercent);
    }

    [Fact]
    public void PassengerDoorsInterlockTractionAndRequireStoppedTrain()
    {
        var consist = new ConsistMetadata("DoorInterlock", new[]
        {
            new ConsistCar("Engine", "Diesel", 1, true, false, false),
            new ConsistCar("Wagon", "Freight", 2, false, false, false)
        }, "door-interlock.con");
        var traction = new LocomotiveTractionMetadata(
            "TestLoco", 1_500, 1_500, 100_000, 100, 0, null, "test.eng");
        var assets = new[]
        {
            CreateAsset(1, "Engine", true, 1_000, 10, 4_000, traction),
            CreateAsset(2, "Wagon", false, 2_000, 20, 8_000, null)
        };
        var setup = new TrainConsistSimulationSetupBuilder().Build(consist, assets, 500);
        var controller = new BrowserTrainDrivingController(setup);

        controller.SetThrottle(50);
        Assert.Throws<InvalidOperationException>(() => controller.SetDoors(true, false));
        controller.SetThrottle(0);
        controller.SetDoors(true, false);
        Assert.True(controller.LeftDoorsOpen);
        controller.SetThrottle(80);
        Assert.Equal(0, controller.ThrottlePercent);

        controller.SetDoors(false, false);
        Assert.False(controller.LeftDoorsOpen);
        Assert.False(controller.RightDoorsOpen);
        controller.SetThrottle(100);
        Assert.Equal(100, controller.ThrottlePercent);
        Assert.Throws<InvalidOperationException>(() => controller.SetDoors(false, true));

        var movingSetup = new TrainConsistSimulationSetupBuilder().Build(consist, assets, 500,
            initialSpeedMetersPerSecond: 1.0);
        var movingController = new BrowserTrainDrivingController(movingSetup);
        Assert.Contains(movingController.State.Cars, car => Math.Abs(car.SpeedMetersPerSecond) > 0.1);
        Assert.Throws<InvalidOperationException>(() => movingController.SetDoors(true, false));
        Assert.False(movingController.LeftDoorsOpen, "door-open request while moving must not change door state");
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

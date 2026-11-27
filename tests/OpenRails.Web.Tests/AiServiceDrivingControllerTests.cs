using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class AiServiceDrivingControllerTests
{
    private readonly AiServiceDrivingController controller = new();

    [Fact]
    public void AcceleratesTowardRequestedSpeedWhilePathIsClear()
    {
        var decision = controller.Calculate(Plan(10, 100), 10, 2, 12);
        Assert.True(decision.ThrottlePercent > 0);
        Assert.Equal(0, decision.BrakePercent);
        Assert.False(decision.IsPathComplete);
        Assert.Equal(1, decision.NextWaypointIndex);
    }

    [Fact]
    public void ReducesTargetSpeedToFitAvailableBrakingDistance()
    {
        var decision = controller.Calculate(Plan(0, 100), 90, 20, 30);
        Assert.True(decision.TargetSpeedMetersPerSecond < 30);
        Assert.True(decision.BrakePercent > 0);
    }

    [Fact]
    public void BrakesForTheNearestStopSignalBeforeThePathEnd()
    {
        var route = Plan(0, 100);
        var signals = new[]
        {
            new AiServiceSignalConstraint(40, RouteSignalAspect.Stop, null),
            new AiServiceSignalConstraint(70, RouteSignalAspect.Stop, null)
        };

        var decision = controller.Calculate(route, 10, 15, 20, signalConstraints: signals);

        Assert.True(decision.BrakePercent > 0);
        Assert.True(decision.TargetSpeedMetersPerSecond < 20);
        Assert.False(decision.IsPathComplete);
        Assert.Equal(30, decision.RemainingDistanceMeters, 3);
    }

    [Fact]
    public void AppliesApproachAspectSpeedLimitAhead()
    {
        var route = Plan(0, 100);
        var signals = new[]
        {
            new AiServiceSignalConstraint(50, RouteSignalAspect.Approach, 5)
        };

        var decision = controller.Calculate(route, 10, 0, 20, signalConstraints: signals);

        Assert.Equal(5, decision.TargetSpeedMetersPerSecond, 3);
    }

    [Fact]
    public void IgnoresStopSignalBehindReverseMovingService()
    {
        var route = new ServiceRouteStartPosition(90, 0,
            new TrackWorldPosition(0, 0, 90), new[] { 90d, 50d, 10d }, -1);
        var signals = new[]
        {
            new AiServiceSignalConstraint(95, RouteSignalAspect.Stop, null)
        };

        var decision = controller.Calculate(route, 90, 0, 10, signalConstraints: signals);

        Assert.True(decision.ThrottlePercent > 0);
        Assert.Equal(80, decision.RemainingDistanceMeters, 3);
    }

    [Fact]
    public void StopsAtNativeWaitingPointUntilReleased()
    {
        var route = new ServiceRouteStartPosition(0, 0,
            new TrackWorldPosition(0, 0, 0), new[] { 0d, 40d, 100d }, 1,
            new[]
            {
                new ServiceRouteWaypoint(0, false, false),
                new ServiceRouteWaypoint(40, true, false),
                new ServiceRouteWaypoint(100, false, false)
            });

        var approach = controller.Calculate(route, 10, 12, 15);
        Assert.True(approach.BrakePercent > 0);
        Assert.Equal(30, approach.RemainingDistanceMeters, 3);
        Assert.Equal(1, approach.WaitingWaypointIndex);

        var stopped = controller.Calculate(route, 39, 0, 15);
        Assert.Equal(100, stopped.BrakePercent);
        Assert.Equal(1, stopped.WaitingWaypointIndex);

        var released = controller.Calculate(route, 39, 0, 15,
            releasedWaitingPointIndices: new HashSet<int> { 1 });
        Assert.Null(released.WaitingWaypointIndex);
        Assert.True(released.ThrottlePercent > 0);
    }

    [Fact]
    public void AppliesFullBrakeAtPathEnd()
    {
        var decision = controller.Calculate(Plan(0, 100), 99, 4, 10);
        Assert.Equal(0, decision.ThrottlePercent);
        Assert.Equal(100, decision.BrakePercent);
        Assert.True(decision.IsPathComplete);
    }

    [Fact]
    public void BrakesWhenTrainIsMovingAgainstServiceDirection()
    {
        var forwardRoute = Plan(0, 100);
        var forwardDecision = controller.Calculate(forwardRoute, 10, -8, 15);
        Assert.Equal(0, forwardDecision.ThrottlePercent);
        Assert.True(forwardDecision.BrakePercent > 0);
        Assert.Equal(0, forwardDecision.TargetSpeedMetersPerSecond);

        var reverseRoute = new ServiceRouteStartPosition(90, 0,
            new TrackWorldPosition(0, 0, 90), new[] { 90d, 50d, 10d }, -1);
        var reverseDecision = controller.Calculate(reverseRoute, 90, 8, 15);
        Assert.Equal(0, reverseDecision.ThrottlePercent);
        Assert.True(reverseDecision.BrakePercent > 0);
    }

    [Fact]
    public void SupportsReverseOrderedServicePath()
    {
        var route = new ServiceRouteStartPosition(90, 0,
            new TrackWorldPosition(0, 0, 90), new[] { 90d, 50d, 10d }, -1);
        var decision = controller.Calculate(route, 90, 0, 10);
        Assert.Equal(1, decision.NextWaypointIndex);
        Assert.True(decision.ThrottlePercent > 0);
    }

    [Fact]
    public void RejectsWaypointsThatConflictWithDeclaredRouteDirectionOrMetadata()
    {
        var reversed = new ServiceRouteStartPosition(0, 0,
            new TrackWorldPosition(0, 0, 0), new[] { 0d, 40d, 20d }, 1);
        Assert.Throws<ArgumentException>(() => controller.Calculate(reversed, 0, 0, 10));

        var overflowing = new ServiceRouteStartPosition(-1e308, 0,
            new TrackWorldPosition(0, 0, 0), new[] { -1e308, 1e308 }, 1);
        Assert.Throws<ArgumentException>(() => controller.Calculate(overflowing, 0, 0, 10));

        var missingDistances = new ServiceRouteStartPosition(0, 0,
            new TrackWorldPosition(0, 0, 0), null, 1,
            new[] { new ServiceRouteWaypoint(0, true, false) });
        Assert.Throws<ArgumentException>(() => controller.Calculate(missingDistances, 0, 0, 10));

        var mismatched = new ServiceRouteStartPosition(0, 0,
            new TrackWorldPosition(0, 0, 0), new[] { 0d, 40d, 100d }, 1,
            new[]
            {
                new ServiceRouteWaypoint(0, false, false),
                new ServiceRouteWaypoint(50, true, false),
                new ServiceRouteWaypoint(100, false, false)
            });
        Assert.Throws<ArgumentException>(() => controller.Calculate(mismatched, 0, 0, 10));
    }

    [Fact]
    public void RejectsServiceDistancesWhoseDifferencesOverflow()
    {
        Assert.Throws<ArgumentException>(() =>
            controller.Calculate(Plan(0, double.MaxValue), -double.MaxValue, 0, 10));
    }

    [Fact]
    public void RejectsInvalidSignalAndRouteWaypointData()
    {
        var route = Plan(0, 100);
        Assert.Throws<ArgumentException>(() => controller.Calculate(route, 0, 0, 10,
            signalConstraints: new[]
            {
                new AiServiceSignalConstraint(double.NaN, RouteSignalAspect.Stop, null)
            }));
        Assert.Throws<ArgumentException>(() => controller.Calculate(route, 0, 0, 10,
            signalConstraints: new[]
            {
                new AiServiceSignalConstraint(10, RouteSignalAspect.Approach, -1)
            }));
        Assert.Throws<ArgumentException>(() => controller.Calculate(route, 0, 0, 10,
            signalConstraints: new[]
            {
                new AiServiceSignalConstraint(10, RouteSignalAspect.Approach, 0)
            }));
        Assert.Throws<ArgumentException>(() => controller.Calculate(
            route with { RouteWaypointDistancesMeters = new[] { 0d, double.PositiveInfinity } },
            0, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => controller.Calculate(
            route with { Direction = 0 }, 0, 0, 10));
    }

    [Fact]
    public void WaitingPointReleasesCanBeResetWhenSwitchingServices()
    {
        var consist = new ConsistMetadata("AiWaitingReset", new[]
        {
            new ConsistCar("Engine", "Diesel", 1, true, false, false)
        }, "ai-waiting-reset.con");
        var traction = new LocomotiveTractionMetadata(
            "AiTestLoco", 1_500, 1_500, 100_000, 100, 0, null, "ai-test.eng");
        var asset = new ConsistCarAssetReference("AiTest", 1, "Engine", "Diesel", true,
            false, "ai-test.eng", true, null,
            new RollingStockMetadata("Engine", "Diesel", 40_000, 3, 4, 20, 4, 8,
                "air_single_pipe", "distributor", 100_000, "ai-test.eng"),
            "ai-test.s", true, null, traction);
        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, new[] { asset }, 200, startDistanceMeters: 38);
        var live = new BrowserTrainDrivingController(setup);
        var route = new ServiceRouteStartPosition(0, 0,
            new TrackWorldPosition(0, 0, 0), new[] { 0d, 48d, 100d }, 1,
            new[]
            {
                new ServiceRouteWaypoint(0, false, false),
                new ServiceRouteWaypoint(48, true, false),
                new ServiceRouteWaypoint(100, false, false)
            });
        var ai = new AiServiceDrivingController();

        ai.ReleaseWaitingPoint(1);
        var released = ai.Apply(live, route, 10);
        Assert.True(released.ThrottlePercent > 0);

        ai.ResetWaitingPoints();
        var stopped = ai.Apply(live, route, 10);
        Assert.Equal(100, stopped.BrakePercent);
        Assert.True(stopped.IsWaitingAtWaypoint);
    }

    [Fact]
    public void AppliesPathFollowerCommandsToTheLiveBrowserTrainController()
    {
        var consist = new ConsistMetadata("AiService", new[]
        {
            new ConsistCar("Engine", "Diesel", 1, true, false, false)
        }, "ai-service.con");
        var traction = new LocomotiveTractionMetadata(
            "AiTestLoco", 1_500, 1_500, 100_000, 100, 0, null, "ai-test.eng");
        var asset = new ConsistCarAssetReference("AiTest", 1, "Engine", "Diesel", true,
            false, "ai-test.eng", true, null,
            new RollingStockMetadata("Engine", "Diesel", 40_000, 3, 4, 20, 4, 8,
                "air_single_pipe", "distributor", 100_000, "ai-test.eng"),
            "ai-test.s", true, null, traction);
        var setup = new TrainConsistSimulationSetupBuilder().Build(consist, new[] { asset }, 200);
        var liveController = new BrowserTrainDrivingController(setup);
        var plan = Plan(0, 150);

        var decision = controller.Apply(liveController, plan, 10);

        Assert.True(decision.ThrottlePercent > 0);
        Assert.Equal(decision.ThrottlePercent, liveController.ThrottlePercent, 3);
        Assert.Equal(0, liveController.BrakePercent);
    }

    private static ServiceRouteStartPosition Plan(double start, double end) =>
        new(start, 0, new TrackWorldPosition(0, 0, start), new[] { start, end }, 1);
    [Fact]
    public void AppliesConfiguredSignalSpeedRestrictionEvenWhenAspectIsClear()
    {
        var route = Plan(0, 100);
        var signals = new[]
        {
            new AiServiceSignalConstraint(50, RouteSignalAspect.Clear2, 6)
        };

        var decision = controller.Calculate(route, 10, 0, 20, signalConstraints: signals);

        Assert.Equal(6, decision.TargetSpeedMetersPerSecond, 3);
}
}

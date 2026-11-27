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
    public void AppliesFullBrakeAtPathEnd()
    {
        var decision = controller.Calculate(Plan(0, 100), 99, 4, 10);
        Assert.Equal(0, decision.ThrottlePercent);
        Assert.Equal(100, decision.BrakePercent);
        Assert.True(decision.IsPathComplete);
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

    private static ServiceRouteStartPosition Plan(double start, double end) =>
        new(start, 0, new TrackWorldPosition(0, 0, start), new[] { start, end }, 1);
}

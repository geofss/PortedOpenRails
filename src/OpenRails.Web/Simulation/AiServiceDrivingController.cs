using System;
using System.Linq;

namespace OpenRails.Web.Simulation;

/// <summary>
/// Path-following controller for one selected native service. It follows the
/// projected .pat waypoint order and brakes for the path end. Target speed is
/// supplied by the operator because native speed restrictions and timetables
/// are not yet available to this browser controller.
/// </summary>
public sealed class AiServiceDrivingController
{
    public AiServiceControlDecision Calculate(
        ServiceRouteStartPosition route,
        double currentDistanceMeters,
        double currentSpeedMetersPerSecond,
        double targetSpeedMetersPerSecond,
        double serviceDecelerationMetersPerSecondSquared = 0.65,
        double stoppingBufferMeters = 2)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (!double.IsFinite(currentDistanceMeters))
            throw new ArgumentOutOfRangeException(nameof(currentDistanceMeters));
        if (!double.IsFinite(currentSpeedMetersPerSecond))
            throw new ArgumentOutOfRangeException(nameof(currentSpeedMetersPerSecond));
        if (!double.IsFinite(targetSpeedMetersPerSecond) || targetSpeedMetersPerSecond < 0)
            throw new ArgumentOutOfRangeException(nameof(targetSpeedMetersPerSecond));
        if (!double.IsFinite(serviceDecelerationMetersPerSecondSquared) || serviceDecelerationMetersPerSecondSquared <= 0)
            throw new ArgumentOutOfRangeException(nameof(serviceDecelerationMetersPerSecondSquared));
        if (!double.IsFinite(stoppingBufferMeters) || stoppingBufferMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(stoppingBufferMeters));

        IReadOnlyList<double> waypoints = route.RouteWaypointDistancesMeters is { Count: > 0 } distances
            ? distances : new[] { route.DistanceMeters };
        var direction = route.Direction < 0 ? -1 : 1;
        var endDistance = waypoints[^1];
        var remaining = direction > 0 ? endDistance - currentDistanceMeters : currentDistanceMeters - endDistance;
        var speed = Math.Abs(currentSpeedMetersPerSecond);
        var nextWaypoint = FindNextWaypoint(waypoints, currentDistanceMeters, direction);
        if (remaining <= stoppingBufferMeters)
            return new AiServiceControlDecision(0, 100, 0, nextWaypoint, true, Math.Max(0, remaining));

        var availableBrakingDistance = Math.Max(0, remaining - stoppingBufferMeters);
        var brakingLimitedSpeed = Math.Sqrt(2 * serviceDecelerationMetersPerSecondSquared * availableBrakingDistance);
        var commandedSpeed = Math.Min(targetSpeedMetersPerSecond, brakingLimitedSpeed);
        var error = commandedSpeed - speed;
        var throttle = error > 0.35 ? Math.Clamp(error * 22, 0, 100) : 0;
        var brake = error < -0.35 ? Math.Clamp(-error * 35, 0, 100) : 0;
        return new AiServiceControlDecision(throttle, brake, commandedSpeed,
            nextWaypoint, false, remaining);
    }

    public AiServiceControlDecision Apply(
        BrowserTrainDrivingController controller,
        ServiceRouteStartPosition route,
        double targetSpeedMetersPerSecond)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(route);
        if (controller.State.Cars.Count == 0)
        {
            controller.SetThrottle(0);
            controller.SetBrake(100);
            return new AiServiceControlDecision(0, 100, 0, -1, true, 0);
        }

        var lead = controller.State.Cars[0];
        var requiredDirection = route.Direction < 0
            ? TrainReverserPosition.Reverse
            : TrainReverserPosition.Forward;
        if (controller.ReverserPosition != requiredDirection)
        {
            controller.SetThrottle(0);
            controller.SetBrake(100);
            if (controller.CanChangeReverser)
            {
                controller.SetReverser(requiredDirection);
                controller.SetBrake(0);
            }
            return new AiServiceControlDecision(0, controller.BrakePercent, 0, 0, false,
                Math.Abs(lead.SpeedMetersPerSecond));
        }

        var decision = Calculate(route, lead.DistanceMeters, lead.SpeedMetersPerSecond,
            targetSpeedMetersPerSecond);
        controller.SetThrottle((float)decision.ThrottlePercent);
        controller.SetBrake(decision.BrakePercent);
        return decision;
    }

    private static int FindNextWaypoint(IReadOnlyList<double> waypoints, double currentDistance, int direction)
    {
        for (var index = 1; index < waypoints.Count; index++)
            if ((direction > 0 && waypoints[index] > currentDistance + 1) ||
                (direction < 0 && waypoints[index] < currentDistance - 1))
                return index;
        return Math.Max(0, waypoints.Count - 1);
    }
}

public sealed record AiServiceControlDecision(
    double ThrottlePercent,
    double BrakePercent,
    double TargetSpeedMetersPerSecond,
    int NextWaypointIndex,
    bool IsPathComplete,
    double RemainingDistanceMeters);

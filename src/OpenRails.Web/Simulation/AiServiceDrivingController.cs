using System;
using System.Linq;

namespace OpenRails.Web.Simulation;

/// <summary>
/// Path-following controller for one selected native service. It follows the
/// projected .pat waypoint order and brakes for the path end. Target speed is
/// supplied by the operator because native speed restrictions and timetables
/// are not yet available to this browser controller.
/// </summary>
public sealed record AiServiceSignalConstraint(
    double RouteDistanceMeters,
    RouteSignalAspect Aspect,
    float? SpeedLimitMetersPerSecond);

public sealed class AiServiceDrivingController
{
    private readonly HashSet<int> releasedWaitingPoints = [];
    private readonly object waitingPointLock = new();
    public AiServiceControlDecision Calculate(
        ServiceRouteStartPosition route,
        double currentDistanceMeters,
        double currentSpeedMetersPerSecond,
        double targetSpeedMetersPerSecond,
        double serviceDecelerationMetersPerSecondSquared = 0.65,
        double stoppingBufferMeters = 2,
        IReadOnlyList<AiServiceSignalConstraint>? signalConstraints = null,
        IReadOnlySet<int>? releasedWaitingPointIndices = null)
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
        if (route.Direction is not (-1 or 1))
            throw new ArgumentOutOfRangeException(nameof(route), "Service route direction must be forward (1) or reverse (-1).");
        if (!double.IsFinite(route.DistanceMeters) ||
            !double.IsFinite(route.DistanceMeters - currentDistanceMeters))
            throw new ArgumentOutOfRangeException(nameof(route), "Service route start distance must be finite and representable relative to the train.");
        if (route.RouteWaypointDistancesMeters?.Any(distance =>
                !double.IsFinite(distance) || !double.IsFinite(distance - currentDistanceMeters)) == true ||
            route.RouteWaypoints?.Any(waypoint => waypoint is null ||
                !double.IsFinite(waypoint.DistanceMeters) ||
                !double.IsFinite(waypoint.DistanceMeters - currentDistanceMeters)) == true)
            throw new ArgumentException("Service route waypoint distances must be finite.", nameof(route));
        if (route.RouteWaypoints is { Count: > 0 } &&
            route.RouteWaypointDistancesMeters is not { Count: > 0 })
            throw new ArgumentException("Waiting-point metadata requires the matching route distance sequence.", nameof(route));
        if (route.RouteWaypointDistancesMeters is { Count: > 0 } routeDistances &&
            route.RouteWaypoints is { Count: > 0 } routeWaypointsForValidation &&
            routeDistances.Count != routeWaypointsForValidation.Count)
            throw new ArgumentException("Service route distance and waypoint collections must have matching lengths.", nameof(route));

        var signals = signalConstraints ?? Array.Empty<AiServiceSignalConstraint>();
        if (signals.Any(signal => signal is null ||
            !double.IsFinite(signal.RouteDistanceMeters) ||
            !double.IsFinite(signal.RouteDistanceMeters - currentDistanceMeters) ||
            !Enum.IsDefined(signal.Aspect) ||
            (signal.SpeedLimitMetersPerSecond.HasValue &&
                (!float.IsFinite(signal.SpeedLimitMetersPerSecond.Value) || signal.SpeedLimitMetersPerSecond.Value <= 0))))
            throw new ArgumentException("Signal constraints must contain finite route positions and valid positive speed limits.", nameof(signalConstraints));

        IReadOnlyList<double> waypoints = route.RouteWaypointDistancesMeters is { Count: > 0 } distances
            ? distances : new[] { route.DistanceMeters };
        var direction = route.Direction;
        if (Math.Abs(waypoints[0] - route.DistanceMeters) > 0.5)
            throw new ArgumentException("The first service waypoint must match the route start distance.", nameof(route));
        for (var index = 1; index < waypoints.Count; index++)
        {
            var progress = (waypoints[index] - waypoints[index - 1]) * direction;
            if (!double.IsFinite(progress) || progress < -0.5)
                throw new ArgumentException("Service route waypoints must follow the declared route direction with finite spacing.", nameof(route));
        }
        if (route.RouteWaypoints is { Count: > 0 } alignedWaypoints)
            for (var index = 0; index < waypoints.Count; index++)
                if (Math.Abs(alignedWaypoints[index].DistanceMeters - waypoints[index]) > 0.5)
                    throw new ArgumentException("Service route waypoint metadata must align with route distances.", nameof(route));
        var endDistance = waypoints[^1];
        var remaining = direction > 0 ? endDistance - currentDistanceMeters : currentDistanceMeters - endDistance;
        if (!double.IsFinite(remaining))
            throw new ArgumentOutOfRangeException(nameof(currentDistanceMeters),
                "Remaining service route distance exceeded the finite simulation range.");
        var pathEndIsNearestLimit = true;
        var stopSignalAhead = signals
            .Where(signal => signal.Aspect == RouteSignalAspect.Stop &&
                (direction > 0
                    ? signal.RouteDistanceMeters > currentDistanceMeters + 0.5
                    : signal.RouteDistanceMeters < currentDistanceMeters - 0.5))
            .Select(signal => direction > 0
                ? signal.RouteDistanceMeters - currentDistanceMeters
                : currentDistanceMeters - signal.RouteDistanceMeters)
            .Where(distance => distance >= 0)
            .OrderBy(distance => distance)
            .FirstOrDefault(double.PositiveInfinity);
        if (stopSignalAhead < remaining)
        {
            remaining = stopSignalAhead;
            pathEndIsNearestLimit = false;
        }
        int? waitingWaypointIndex = null;
        var waitingDistance = double.PositiveInfinity;
        if (route.RouteWaypoints is { Count: > 0 } routeWaypoints)
        {
            for (var index = 0; index < routeWaypoints.Count; index++)
            {
                if (!routeWaypoints[index].IsWaitingPoint ||
                    (releasedWaitingPointIndices?.Contains(index) ?? false))
                    continue;
                var waypointDistance = routeWaypoints[index].DistanceMeters;
                var distanceAhead = direction > 0
                    ? waypointDistance - currentDistanceMeters
                    : currentDistanceMeters - waypointDistance;
                if (distanceAhead >= -0.5 && distanceAhead < waitingDistance)
                {
                    waitingDistance = Math.Max(0, distanceAhead);
                    waitingWaypointIndex = index;
                }
            }
        }
        if (waitingWaypointIndex is not null && waitingDistance <= remaining)
        {
            remaining = waitingDistance;
            pathEndIsNearestLimit = false;
        }

        var approachSpeedLimit = signals
            .Where(signal => signal.Aspect == RouteSignalAspect.Approach &&
                signal.SpeedLimitMetersPerSecond is > 0 &&
                (direction > 0
                    ? signal.RouteDistanceMeters > currentDistanceMeters
                    : signal.RouteDistanceMeters < currentDistanceMeters))
            .Select(signal => (double)signal.SpeedLimitMetersPerSecond!.Value)
            .DefaultIfEmpty(double.PositiveInfinity)
            .Min();
        var requestedSpeed = Math.Min(targetSpeedMetersPerSecond, approachSpeedLimit);
        var speed = Math.Abs(currentSpeedMetersPerSecond);
        var nextWaypoint = FindNextWaypoint(waypoints, currentDistanceMeters, direction);
        if (remaining <= stoppingBufferMeters)
            return new AiServiceControlDecision(0, 100, 0, nextWaypoint,
                pathEndIsNearestLimit, Math.Max(0, remaining), waitingWaypointIndex,
                waitingWaypointIndex is not null && speed <= 0.25);

        var availableBrakingDistance = Math.Max(0, remaining - stoppingBufferMeters);
        var brakingLimitedSpeed = Math.Sqrt(2 * serviceDecelerationMetersPerSecondSquared * availableBrakingDistance);
        var commandedSpeed = Math.Min(requestedSpeed, brakingLimitedSpeed);
        var error = commandedSpeed - speed;
        var throttle = error > 0.35 ? Math.Clamp(error * 22, 0, 100) : 0;
        var brake = error < -0.35 ? Math.Clamp(-error * 35, 0, 100) : 0;
        return new AiServiceControlDecision(throttle, brake, commandedSpeed,
            nextWaypoint, false, remaining, waitingWaypointIndex, false);
    }

    public AiServiceControlDecision Apply(
        BrowserTrainDrivingController controller,
        ServiceRouteStartPosition route,
        double targetSpeedMetersPerSecond,
        IReadOnlyList<AiServiceSignalConstraint>? signalConstraints = null)
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

        HashSet<int> releasedSnapshot;
        lock (waitingPointLock)
            releasedSnapshot = new HashSet<int>(releasedWaitingPoints);
        var decision = Calculate(route, lead.DistanceMeters, lead.SpeedMetersPerSecond,
            targetSpeedMetersPerSecond, signalConstraints: signalConstraints,
            releasedWaitingPointIndices: releasedSnapshot);
        controller.SetThrottle((float)decision.ThrottlePercent);
        controller.SetBrake(decision.BrakePercent);
        return decision;
    }

    public void ReleaseWaitingPoint(int waypointIndex)
    {
        if (waypointIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(waypointIndex));
        lock (waitingPointLock)
            releasedWaitingPoints.Add(waypointIndex);
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
    double RemainingDistanceMeters,
    int? WaitingWaypointIndex = null,
    bool IsWaitingAtWaypoint = false);

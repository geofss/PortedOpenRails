namespace OpenRails.Web.Simulation;

/// <summary>
/// Integrates one car's longitudinal motion from a signed total force supplied by
/// the upstream-compatible force calculation layer. The acceleration equation
/// follows Open Rails Train.UpdateCarSpeeds; traction, brake, adhesion, resistance,
/// and coupler-force calculation remain separate systems.
/// </summary>
public sealed class TrainCarMotionIntegrator
{
    public TrainCarMotionStepResult Step(
        TrainCarMotionState state,
        double totalForceNewtons,
        double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!double.IsFinite(state.MassKg) || state.MassKg <= 0)
            throw new ArgumentOutOfRangeException(nameof(state), "Vehicle mass must be finite and greater than zero.");
        if (!double.IsFinite(state.SpeedMetersPerSecond) ||
            !double.IsFinite(state.DistanceMeters) ||
            !double.IsFinite(totalForceNewtons))
            throw new ArgumentOutOfRangeException(nameof(state), "Motion state and force must be finite.");
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

        if (elapsedSeconds == 0)
            return new TrainCarMotionStepResult(state, totalForceNewtons / state.MassKg);

        var acceleration = totalForceNewtons / state.MassKg;
        var nextSpeed = state.SpeedMetersPerSecond + acceleration * elapsedSeconds;
        var motionSeconds = elapsedSeconds;

        // A resisting force can bring a car to rest partway through this step.
        // Stop at the zero-velocity instant instead of incorrectly applying the
        // remaining frame time as though the car were still moving.
        if (state.SpeedMetersPerSecond > 0 && nextSpeed < 0 ||
            state.SpeedMetersPerSecond < 0 && nextSpeed > 0)
        {
            motionSeconds = -state.SpeedMetersPerSecond / acceleration;
            nextSpeed = 0;
        }

        // Trapezoidal integration is exact for constant acceleration over the
        // portion of the step during which the car is actually moving.
        var nextDistance = state.DistanceMeters +
            (state.SpeedMetersPerSecond + nextSpeed) * 0.5 * motionSeconds;
        var nextState = state with
        {
            SpeedMetersPerSecond = nextSpeed,
            DistanceMeters = nextDistance
        };
        return new TrainCarMotionStepResult(nextState, acceleration);
    }
}

public sealed record TrainCarMotionState(
    double MassKg,
    double SpeedMetersPerSecond,
    double DistanceMeters);

public sealed record TrainCarMotionStepResult(
    TrainCarMotionState State,
    double AccelerationMetersPerSecondSquared);

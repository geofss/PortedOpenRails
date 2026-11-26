namespace OpenRails.Web.Simulation;

/// <summary>
/// Calculates equal-and-opposite spring/damper forces for adjacent cars.
/// Boundary displacement is rear-car position minus front-car position minus
/// the configured rest gap. Positive force acts on the rear car; the front car
/// receives the opposite force. This utility is deliberately independent of
/// route/track geometry so it can be tested and integrated deterministically.
/// </summary>
public sealed class TrainConsistCouplerDynamics
{
    private readonly TrainCouplerSlackModel slackModel = new();

    public IReadOnlyList<double> CalculatePerCarForces(
        TrainConsistMotionState state,
        IReadOnlyList<double> restGapsMeters,
        IReadOnlyList<double> slackMetersByBoundary,
        IReadOnlyList<double> stiffnessNewtonsPerMeter,
        IReadOnlyList<double> dampingNewtonSecondsPerMeter,
        IReadOnlyList<double> maximumForceNewtons) =>
        CalculatePerCarForces(state, restGapsMeters, slackMetersByBoundary,
            stiffnessNewtonsPerMeter, dampingNewtonSecondsPerMeter,
            maximumForceNewtons, maximumForceNewtons);

    public IReadOnlyList<double> CalculatePerCarForces(
        TrainConsistMotionState state,
        IReadOnlyList<double> restGapsMeters,
        IReadOnlyList<double> slackMetersByBoundary,
        IReadOnlyList<double> stiffnessNewtonsPerMeter,
        IReadOnlyList<double> dampingNewtonSecondsPerMeter,
        IReadOnlyList<double> maximumCompressionNewtons,
        IReadOnlyList<double> maximumTensionNewtons)
    {
        ArgumentNullException.ThrowIfNull(state);
        var boundaryForces = CalculatePerBoundaryForcesOnRearCars(
            state, restGapsMeters, slackMetersByBoundary, stiffnessNewtonsPerMeter,
            dampingNewtonSecondsPerMeter, maximumCompressionNewtons, maximumTensionNewtons);
        var forces = new double[state.Cars.Count];
        for (var i = 0; i < boundaryForces.Count; i++)
        {
            forces[i] -= boundaryForces[i];
            forces[i + 1] += boundaryForces[i];
        }
        return Array.AsReadOnly(forces);
    }

    /// <summary>
    /// Returns one signed coupler force per adjacent-car boundary, applied to
    /// the rear car. Negative values indicate tension; positive values indicate compression.
    /// </summary>
    public IReadOnlyList<double> CalculatePerBoundaryForcesOnRearCars(
        TrainConsistMotionState state,
        IReadOnlyList<double> restGapsMeters,
        IReadOnlyList<double> slackMetersByBoundary,
        IReadOnlyList<double> stiffnessNewtonsPerMeter,
        IReadOnlyList<double> dampingNewtonSecondsPerMeter,
        IReadOnlyList<double> maximumCompressionNewtons,
        IReadOnlyList<double> maximumTensionNewtons)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(restGapsMeters);
        ArgumentNullException.ThrowIfNull(slackMetersByBoundary);
        ArgumentNullException.ThrowIfNull(stiffnessNewtonsPerMeter);
        ArgumentNullException.ThrowIfNull(dampingNewtonSecondsPerMeter);
        ArgumentNullException.ThrowIfNull(maximumCompressionNewtons);
        ArgumentNullException.ThrowIfNull(maximumTensionNewtons);

        var boundaryCount = Math.Max(0, state.Cars.Count - 1);
        if (restGapsMeters.Count != boundaryCount ||
            slackMetersByBoundary.Count != boundaryCount ||
            stiffnessNewtonsPerMeter.Count != boundaryCount ||
            dampingNewtonSecondsPerMeter.Count != boundaryCount ||
            maximumCompressionNewtons.Count != boundaryCount ||
            maximumTensionNewtons.Count != boundaryCount)
            throw new ArgumentException("Coupler parameter arrays must have one entry per adjacent-car boundary.");

        var forces = new double[boundaryCount];
        for (var i = 0; i < boundaryCount; i++)
        {
            var front = state.Cars[i];
            var rear = state.Cars[i + 1];
            var relativeDisplacement = rear.DistanceMeters - front.DistanceMeters - restGapsMeters[i];
            var relativeVelocity = rear.SpeedMetersPerSecond - front.SpeedMetersPerSecond;
            forces[i] = -slackModel.CalculateForceOnRearCar(
                relativeDisplacement, relativeVelocity, slackMetersByBoundary[i],
                stiffnessNewtonsPerMeter[i], dampingNewtonSecondsPerMeter[i],
                maximumCompressionNewtons[i], maximumTensionNewtons[i]);
        }
        return Array.AsReadOnly(forces);
    }
}

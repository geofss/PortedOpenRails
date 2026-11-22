namespace OpenRails.Web.Simulation;

/// <summary>
/// Estimates internal coupler forces required for a target common consist
/// acceleration from each car's external longitudinal forces. Positive boundary
/// force means compression (forward force on the leading portion); negative is
/// tension. This is an algebraic force distribution, not slack/drawbar dynamics.
/// </summary>
public sealed class TrainConsistCouplerForceDistributor
{
    public IReadOnlyList<double> CalculateBoundaryForces(
        TrainConsistMotionState state,
        IReadOnlyList<TrainCarForceBreakdown> externalForces,
        double targetAccelerationMetersPerSecondSquared,
        IReadOnlyList<TrainCouplerForceLimit>? forceLimits = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(externalForces);
        if (state.Cars.Count == 0)
            throw new ArgumentException("A consist must contain at least one car.", nameof(state));
        if (state.Cars.Count != externalForces.Count)
            throw new ArgumentException("Each car requires an external-force breakdown.", nameof(externalForces));
        if (forceLimits is not null && forceLimits.Count != Math.Max(0, state.Cars.Count - 1))
            throw new ArgumentException("Each coupler boundary requires a matching force limit.", nameof(forceLimits));
        if (forceLimits is not null)
            foreach (var limit in forceLimits)
            {
                if (limit is null)
                    throw new ArgumentException("Coupler force limits cannot contain null entries.", nameof(forceLimits));
                limit.Validate();
            }
        if (!double.IsFinite(targetAccelerationMetersPerSecondSquared))
            throw new ArgumentOutOfRangeException(nameof(targetAccelerationMetersPerSecondSquared));

        var boundaries = new double[Math.Max(0, state.Cars.Count - 1)];
        double residualPrefix = 0;
        var forceModel = new TrainCarForceModel();
        for (var i = 0; i < state.Cars.Count; i++)
        {
            var car = state.Cars[i];
            var forces = externalForces[i];
            if (!double.IsFinite(car.MassKg) || car.MassKg <= 0)
                throw new ArgumentOutOfRangeException(nameof(state), "Car masses must be finite and positive.");
            var externalNet = forceModel.CalculateNetLongitudinalForce(car, forces);
            residualPrefix += car.MassKg * targetAccelerationMetersPerSecondSquared - externalNet;
            if (!double.IsFinite(residualPrefix))
                throw new ArgumentOutOfRangeException(nameof(externalForces), "Force distribution produced a non-finite value.");
            if (i < boundaries.Length)
                boundaries[i] = residualPrefix;
        }

        if (forceLimits is not null)
            for (var i = 0; i < boundaries.Length; i++)
                if (!forceLimits[i].Allows(boundaries[i]))
                    throw new InvalidOperationException(
                        $"Coupler boundary {i} force {boundaries[i]:N0} N exceeds its rating.");

        var totalMass = state.Cars.Sum(car => car.MassKg);
        var totalTargetForce = totalMass * targetAccelerationMetersPerSecondSquared;
        if (!double.IsFinite(totalMass) || !double.IsFinite(totalTargetForce))
            throw new ArgumentOutOfRangeException(nameof(state), "Total consist mass and target force must remain finite.");
        var tolerance = Math.Max(1e-6, Math.Abs(totalTargetForce) * 1e-9);
        if (Math.Abs(residualPrefix) > tolerance)
            throw new InvalidOperationException(
                $"The requested common acceleration is infeasible from external forces: net force residual {residualPrefix:N3} N.");

        return Array.AsReadOnly(boundaries);
    }

    /// <summary>
    /// Computes boundary forces for the requested common acceleration and
    /// converts them to per-car signed inputs ready for the consist integrator.
    /// </summary>
    public IReadOnlyList<double> CalculatePerCarCouplerForces(
        TrainConsistMotionState state,
        IReadOnlyList<TrainCarForceBreakdown> externalForces,
        double targetAccelerationMetersPerSecondSquared,
        IReadOnlyList<TrainCouplerForceLimit>? forceLimits = null)
    {
        var boundaries = CalculateBoundaryForces(
            state, externalForces, targetAccelerationMetersPerSecondSquared, forceLimits);
        return ExpandBoundaryForcesToCarForces(boundaries);
    }

    /// <summary>
    /// Expands front-segment boundary forces into signed per-car forces. For a
    /// front-to-rear car order, each boundary applies equal/opposite forces to
    /// adjacent cars, preserving action/reaction.
    /// </summary>
    public IReadOnlyList<double> ExpandBoundaryForcesToCarForces(
        IReadOnlyList<double> boundaryForces)
    {
        ArgumentNullException.ThrowIfNull(boundaryForces);
        if (boundaryForces.Any(force => !double.IsFinite(force)))
            throw new ArgumentOutOfRangeException(nameof(boundaryForces));
        var carForces = new double[boundaryForces.Count + 1];
        for (var i = 0; i < boundaryForces.Count; i++)
        {
            carForces[i] += boundaryForces[i];
            carForces[i + 1] -= boundaryForces[i];
        }
        return Array.AsReadOnly(carForces);
    }
}

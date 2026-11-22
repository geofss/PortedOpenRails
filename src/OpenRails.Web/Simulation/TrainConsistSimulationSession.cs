using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

/// <summary>
/// Applies per-locomotive traction to a multi-car fixed-step consist.
/// Supports caller-supplied coupler forces or algebraic force distribution
/// for a target common acceleration with optional coupler force ratings.
/// Dynamic slack, breakage, and transient drawbar behavior remain future systems.
/// </summary>
public sealed class TrainConsistSimulationSession
{
    private readonly TrainConsistSimulationLoop loop;
    private readonly IReadOnlyList<LocomotiveTractionModel?> tractionModels;
    private readonly IReadOnlyList<LocomotiveThrottleResponse> throttleResponses;

    public TrainConsistMotionState State => loop.State;
    public double SimulationTimeSeconds => loop.SimulationTimeSeconds;
    public double InterpolationAlpha => loop.InterpolationAlpha;
    public double DroppedTimeSeconds => loop.DroppedTimeSeconds;
    public bool IsPaused => loop.IsPaused;

    public TrainConsistSimulationSession(
        TrainConsistMotionState initialState,
        IReadOnlyList<LocomotiveTractionMetadata?> locomotiveMetadataByCar,
        IReadOnlyList<LocomotiveThrottleResponse>? throttleResponses = null,
        double fixedStepSeconds = 1d / 60d,
        double maximumFrameSeconds = 0.25d,
        int maximumStepsPerFrame = 8)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(locomotiveMetadataByCar);
        if (initialState.Cars.Count != locomotiveMetadataByCar.Count)
            throw new ArgumentException("Each car must have a matching locomotive metadata slot.", nameof(locomotiveMetadataByCar));
        this.tractionModels = locomotiveMetadataByCar
            .Select(metadata => metadata is null ? null : new LocomotiveTractionModel(metadata))
            .ToArray();
        this.throttleResponses = throttleResponses?.ToArray() ??
            Enumerable.Repeat(LocomotiveThrottleResponse.Linear, initialState.Cars.Count).ToArray();
        if (this.throttleResponses.Count != initialState.Cars.Count)
            throw new ArgumentException("Each car must have a matching throttle response.", nameof(throttleResponses));
        loop = new TrainConsistSimulationLoop(
            initialState, fixedStepSeconds, maximumFrameSeconds, maximumStepsPerFrame);
    }

    public int Advance(
        double elapsedFrameSeconds,
        IReadOnlyList<float> throttlePercentByCar,
        Func<int, TrainCarMotionState, TrainCarResistanceForces> resistanceProvider,
        IReadOnlyList<double> couplerForcesNewtons,
        IReadOnlyList<float>? powerReductionByCar = null,
        IReadOnlyList<float>? averageForceByCar = null)
    {
        ArgumentNullException.ThrowIfNull(throttlePercentByCar);
        ArgumentNullException.ThrowIfNull(resistanceProvider);
        ArgumentNullException.ThrowIfNull(couplerForcesNewtons);
        var carCount = State.Cars.Count;
        if (throttlePercentByCar.Count != carCount || couplerForcesNewtons.Count != carCount ||
            (powerReductionByCar is not null && powerReductionByCar.Count != carCount) ||
            (averageForceByCar is not null && averageForceByCar.Count != carCount))
            throw new ArgumentException("All per-car input arrays must match the consist size.");
        if (throttlePercentByCar.Any(value => !float.IsFinite(value)) ||
            (powerReductionByCar is not null && powerReductionByCar.Any(value => !float.IsFinite(value))) ||
            (averageForceByCar is not null && averageForceByCar.Any(value => !float.IsFinite(value))) ||
            couplerForcesNewtons.Any(value => !double.IsFinite(value)))
            throw new ArgumentOutOfRangeException(nameof(throttlePercentByCar), "Per-car throttle, power, average-force, and coupler inputs must be finite.");

        return loop.Advance(elapsedFrameSeconds, state =>
        {
            var forces = new TrainCarForceBreakdown[carCount];
            for (var i = 0; i < carCount; i++)
            {
                var resistance = resistanceProvider(i, state.Cars[i]) ??
                    throw new InvalidOperationException($"Resistance provider returned null for car {i}.");
                var motiveForce = tractionModels[i]?.CalculateTractiveForce(
                    throttlePercentByCar[i],
                    (float)state.Cars[i].SpeedMetersPerSecond,
                    throttleResponses[i],
                    powerReductionByCar is null ? 0 : powerReductionByCar[i],
                    averageForceByCar is null ? 0 : averageForceByCar[i]) ?? 0;
                forces[i] = new TrainCarForceBreakdown(
                    motiveForce,
                    resistance.GravityForceNewtons,
                    resistance.RollingFrictionForceNewtons,
                    resistance.BrakeRetardForceNewtons,
                    resistance.CurveResistanceForceNewtons,
                    resistance.WindResistanceForceNewtons,
                    resistance.TunnelResistanceForceNewtons);
            }

            return new TrainConsistForceFrame(forces, couplerForcesNewtons);
        });
    }

    /// <summary>
    /// Advances the consist while calculating algebraic coupler forces each
    /// fixed-step tick to maintain a requested common acceleration from the
    /// supplied external forces. This is a force-balance controller, not a
    /// coupler slack or transient drawbar model.
    /// </summary>
    public int AdvanceWithCommonAcceleration(
        double elapsedFrameSeconds,
        IReadOnlyList<float> throttlePercentByCar,
        Func<int, TrainCarMotionState, TrainCarResistanceForces> resistanceProvider,
        double targetAccelerationMetersPerSecondSquared,
        float powerReduction = 0,
        IReadOnlyList<float>? averageForceByCar = null,
        IReadOnlyList<TrainCouplerForceLimit>? couplerForceLimits = null)
    {
        ArgumentNullException.ThrowIfNull(throttlePercentByCar);
        ArgumentNullException.ThrowIfNull(resistanceProvider);
        var carCount = State.Cars.Count;
        if (throttlePercentByCar.Count != carCount ||
            (averageForceByCar is not null && averageForceByCar.Count != carCount))
            throw new ArgumentException("Per-car throttle and average-force inputs must match the consist size.");
        if (couplerForceLimits is not null && couplerForceLimits.Count != Math.Max(0, carCount - 1))
            throw new ArgumentException("Coupler force limits must match the consist boundary count.", nameof(couplerForceLimits));
        if (throttlePercentByCar.Any(value => !float.IsFinite(value)) ||
            !float.IsFinite(powerReduction) ||
            (averageForceByCar is not null && averageForceByCar.Any(value => !float.IsFinite(value))) ||
            !double.IsFinite(targetAccelerationMetersPerSecondSquared))
            throw new ArgumentOutOfRangeException(nameof(throttlePercentByCar),
                "Throttle, power reduction, average force, and target acceleration must be finite.");

        var couplerDistributor = new TrainConsistCouplerForceDistributor();
        return loop.Advance(elapsedFrameSeconds, state =>
        {
            var forces = new TrainCarForceBreakdown[carCount];
            for (var i = 0; i < carCount; i++)
            {
                var resistance = resistanceProvider(i, state.Cars[i]) ??
                    throw new InvalidOperationException($"Resistance provider returned null for car {i}.");
                var motiveForce = tractionModels[i]?.CalculateTractiveForce(
                    throttlePercentByCar[i],
                    (float)state.Cars[i].SpeedMetersPerSecond,
                    throttleResponses[i],
                    powerReduction,
                    averageForceByCar is null ? 0 : averageForceByCar[i]) ?? 0;
                forces[i] = new TrainCarForceBreakdown(
                    motiveForce,
                    resistance.GravityForceNewtons,
                    resistance.RollingFrictionForceNewtons,
                    resistance.BrakeRetardForceNewtons,
                    resistance.CurveResistanceForceNewtons,
                    resistance.WindResistanceForceNewtons,
                    resistance.TunnelResistanceForceNewtons);
            }

            var couplerForces = couplerDistributor.CalculatePerCarCouplerForces(
                state, forces, targetAccelerationMetersPerSecondSquared, couplerForceLimits);
            return new TrainConsistForceFrame(forces, couplerForces);
        });
    }

    public void Pause() => loop.Pause();
    public void Resume() => loop.Resume();
    public void Reset(TrainConsistMotionState initialState) => loop.Reset(initialState);
}

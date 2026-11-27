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
    private readonly IReadOnlyList<LocomotiveTractionMetadata?> tractionMetadataByCar;
    private readonly IReadOnlyList<LocomotiveThrottleResponse> throttleResponses;
    private readonly float[] appliedTractionForceByCar;
    private readonly TrainPneumaticBrakeSystem pneumaticBrakes;
    private static readonly TrainCarAdhesionModel AdhesionModel = new();

    public TrainConsistMotionState State => loop.State;
    public double SimulationTimeSeconds => loop.SimulationTimeSeconds;
    public double InterpolationAlpha => loop.InterpolationAlpha;
    public double DroppedTimeSeconds => loop.DroppedTimeSeconds;
    public bool IsPaused => loop.IsPaused;
    public TrainBrakePipeState BrakePipeState => pneumaticBrakes.PipeState;
    public TrainBrakeCylinderState BrakeCylinderState => pneumaticBrakes.CylinderState;
    public TrainBrakeDistributorState BrakeDistributorState => pneumaticBrakes.DistributorState;
    public IReadOnlyList<double> PneumaticBrakePercentByCar => pneumaticBrakes.BrakePercentByCar;

    public TrainConsistSimulationSession(
        TrainConsistMotionState initialState,
        IReadOnlyList<LocomotiveTractionMetadata?> locomotiveMetadataByCar,
        IReadOnlyList<LocomotiveThrottleResponse>? throttleResponses = null,
        double fixedStepSeconds = 1d / 60d,
        double maximumFrameSeconds = 0.25d,
        int maximumStepsPerFrame = 8,
        double? routeLengthMeters = null,
        IReadOnlyList<RollingStockBrakeProfile>? brakeProfilesByCar = null)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(locomotiveMetadataByCar);
        if (initialState.Cars.Count != locomotiveMetadataByCar.Count)
            throw new ArgumentException("Each car must have a matching locomotive metadata slot.", nameof(locomotiveMetadataByCar));
        if (brakeProfilesByCar is not null && brakeProfilesByCar.Count != initialState.Cars.Count)
            throw new ArgumentException("Each car must have a matching brake profile.", nameof(brakeProfilesByCar));
        tractionMetadataByCar = locomotiveMetadataByCar.ToArray();
        this.tractionModels = locomotiveMetadataByCar
            .Select(metadata => metadata is null ? null : new LocomotiveTractionModel(metadata))
            .ToArray();
        appliedTractionForceByCar = new float[initialState.Cars.Count];
        this.throttleResponses = throttleResponses?.ToArray() ??
            Enumerable.Repeat(LocomotiveThrottleResponse.Linear, initialState.Cars.Count).ToArray();
        if (this.throttleResponses.Count != initialState.Cars.Count)
            throw new ArgumentException("Each car must have a matching throttle response.", nameof(throttleResponses));
        if (this.throttleResponses.Any(response => !Enum.IsDefined(response)))
            throw new ArgumentOutOfRangeException(nameof(throttleResponses),
                "Every locomotive throttle response must be a supported mode.");
        loop = new TrainConsistSimulationLoop(
            initialState, fixedStepSeconds, maximumFrameSeconds, maximumStepsPerFrame, routeLengthMeters);
        pneumaticBrakes = new TrainPneumaticBrakeSystem(
            initialState.Cars.Count, brakeProfilesByCar: brakeProfilesByCar);
    }

    public int Advance(
        double elapsedFrameSeconds,
        IReadOnlyList<float> throttlePercentByCar,
        Func<int, TrainCarMotionState, TrainCarResistanceForces> resistanceProvider,
        IReadOnlyList<double> couplerForcesNewtons,
        IReadOnlyList<float>? powerReductionByCar = null,
        IReadOnlyList<float>? averageForceByCar = null,
        IReadOnlyList<double>? adhesionCoefficientByCar = null)
    {
        ArgumentNullException.ThrowIfNull(throttlePercentByCar);
        ArgumentNullException.ThrowIfNull(resistanceProvider);
        ArgumentNullException.ThrowIfNull(couplerForcesNewtons);
        var carCount = State.Cars.Count;
        ValidateAdhesionCoefficients(adhesionCoefficientByCar, carCount);
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
                motiveForce = LimitMotiveForce(motiveForce, state.Cars[i], i, adhesionCoefficientByCar);
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
    /// Applies per-car brake commands and ratings in addition to locomotive
    /// traction, environmental forces, and supplied coupler forces.
    /// </summary>
    public int AdvanceWithBrakeCommands(
        double elapsedFrameSeconds,
        IReadOnlyList<float> throttlePercentByCar,
        IReadOnlyList<double> brakePercentByCar,
        IReadOnlyList<double> maximumBrakeForceNewtonsByCar,
        Func<int, TrainCarMotionState, TrainCarResistanceForces> resistanceProvider,
        IReadOnlyList<double> couplerForcesNewtons,
        float powerReduction = 0,
        IReadOnlyList<float>? averageForceByCar = null,
        Action<double>? beforeSimulationStep = null,
        Func<IReadOnlyList<double>>? brakePercentProvider = null,
        Func<TrainConsistMotionState, IReadOnlyList<double>>? couplerForceProvider = null,
        int tractionDirection = 1,
        IReadOnlyList<double>? adhesionCoefficientByCar = null,
        IReadOnlyList<double>? additionalBrakeForceNewtonsByCar = null)
    {
        ArgumentNullException.ThrowIfNull(throttlePercentByCar);
        ArgumentNullException.ThrowIfNull(brakePercentByCar);
        ArgumentNullException.ThrowIfNull(maximumBrakeForceNewtonsByCar);
        ArgumentNullException.ThrowIfNull(resistanceProvider);
        ArgumentNullException.ThrowIfNull(couplerForcesNewtons);
        if (tractionDirection is < -1 or > 1)
            throw new ArgumentOutOfRangeException(nameof(tractionDirection));
        if (adhesionCoefficientByCar is not null &&
            (adhesionCoefficientByCar.Count != State.Cars.Count ||
             adhesionCoefficientByCar.Any(value => !double.IsFinite(value) || value < 0 || value > 1)))
            throw new ArgumentOutOfRangeException(nameof(adhesionCoefficientByCar),
                "Adhesion coefficients must match the consist and be between zero and one.");
        var adhesionModel = AdhesionModel;
        var carCount = State.Cars.Count;
        if (throttlePercentByCar.Count != carCount || brakePercentByCar.Count != carCount ||
            maximumBrakeForceNewtonsByCar.Count != carCount || couplerForcesNewtons.Count != carCount ||
            (averageForceByCar is not null && averageForceByCar.Count != carCount) ||
            (additionalBrakeForceNewtonsByCar is not null && additionalBrakeForceNewtonsByCar.Count != carCount))
            throw new ArgumentException("All per-car brake, rating, throttle, average-force, and coupler inputs must match the consist size.");
        if (throttlePercentByCar.Any(value => !float.IsFinite(value)) ||
            brakePercentByCar.Any(value => !double.IsFinite(value) || value < 0 || value > 100) ||
            maximumBrakeForceNewtonsByCar.Any(value => !double.IsFinite(value) || value < 0) ||
            couplerForcesNewtons.Any(value => !double.IsFinite(value)) ||
            !float.IsFinite(powerReduction) ||
            (averageForceByCar is not null && averageForceByCar.Any(value => !float.IsFinite(value))) ||
            (additionalBrakeForceNewtonsByCar is not null && additionalBrakeForceNewtonsByCar.Any(value => !double.IsFinite(value) || value < 0)))
            throw new ArgumentOutOfRangeException(nameof(brakePercentByCar),
                "Brake, rating, throttle, power, average-force, and coupler inputs must be finite; brake ratings must be non-negative.");

        var brakeModel = new TrainCarBrakeModel();
        return loop.Advance(elapsedFrameSeconds, state =>
        {
            var forces = new TrainCarForceBreakdown[carCount];
            var activeCouplerForces = couplerForceProvider?.Invoke(state) ?? couplerForcesNewtons;
            if (activeCouplerForces.Count != carCount || activeCouplerForces.Any(value => !double.IsFinite(value)))
                throw new InvalidOperationException("Fixed-step coupler forces must match the consist and remain finite.");
            var activeBrakePercentByCar = brakePercentProvider?.Invoke() ?? brakePercentByCar;
            if (activeBrakePercentByCar.Count != carCount || activeBrakePercentByCar.Any(value => !double.IsFinite(value) || value < 0 || value > 100))
                throw new InvalidOperationException("Fixed-step brake commands must match the consist and remain between 0 and 100 percent.");
            for (var i = 0; i < carCount; i++)
            {
                var resistance = resistanceProvider(i, state.Cars[i]) ??
                    throw new InvalidOperationException($"Resistance provider returned null for car {i}.");
                var motiveForce = appliedTractionForceByCar[i];
                var adhesionCoefficient = adhesionCoefficientByCar is null
                    ? TrainCarAdhesionModel.DefaultAdhesionCoefficient
                    : adhesionCoefficientByCar[i];
                motiveForce = (float)adhesionModel.LimitTractiveForce(
                    motiveForce, state.Cars[i].MassKg, adhesionCoefficient);
                var commandedBrake = Math.Abs(brakeModel.CalculateBrakeRetardForceNewtons(
                    state.Cars[i].SpeedMetersPerSecond,
                    activeBrakePercentByCar[i],
                    maximumBrakeForceNewtonsByCar[i]));
                forces[i] = new TrainCarForceBreakdown(
                    motiveForce * tractionDirection,
                    resistance.GravityForceNewtons,
                    resistance.RollingFrictionForceNewtons,
                    resistance.BrakeRetardForceNewtons + commandedBrake + (additionalBrakeForceNewtonsByCar?[i] ?? 0),
                    resistance.CurveResistanceForceNewtons,
                    resistance.WindResistanceForceNewtons,
                    resistance.TunnelResistanceForceNewtons);
            }
            return new TrainConsistForceFrame(forces, activeCouplerForces);
        }, stepSeconds =>
        {
            beforeSimulationStep?.Invoke(stepSeconds);
            var currentState = State;
            for (var i = 0; i < carCount; i++)
            {
                var targetForce = tractionDirection == 0 ? 0 :
                    tractionModels[i]?.CalculateTractiveForce(
                        throttlePercentByCar[i],
                        (float)currentState.Cars[i].SpeedMetersPerSecond,
                        throttleResponses[i],
                        powerReduction,
                        averageForceByCar is null ? 0 : averageForceByCar[i]) ?? 0;
                var metadata = tractionModels[i] is null ? null : tractionMetadataByCar[i];
                appliedTractionForceByCar[i] = metadata is null
                    ? 0
                    : LocomotiveTractionRamp.Advance(
                        appliedTractionForceByCar[i], targetForce, stepSeconds, metadata);
            }
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
        IReadOnlyList<TrainCouplerForceLimit>? couplerForceLimits = null,
        IReadOnlyList<double>? brakePercentByCar = null,
        IReadOnlyList<double>? maximumBrakeForceNewtonsByCar = null,
        IReadOnlyList<double>? adhesionCoefficientByCar = null)
    {
        ArgumentNullException.ThrowIfNull(throttlePercentByCar);
        ArgumentNullException.ThrowIfNull(resistanceProvider);
        var carCount = State.Cars.Count;
        if (throttlePercentByCar.Count != carCount ||
            (averageForceByCar is not null && averageForceByCar.Count != carCount))
            throw new ArgumentException("Per-car throttle and average-force inputs must match the consist size.");
        if (couplerForceLimits is not null && couplerForceLimits.Count != Math.Max(0, carCount - 1))
            throw new ArgumentException("Coupler force limits must match the consist boundary count.", nameof(couplerForceLimits));
        if ((brakePercentByCar is null) != (maximumBrakeForceNewtonsByCar is null))
            throw new ArgumentException("Brake commands and brake ratings must be supplied together.");
        if (brakePercentByCar is not null && (brakePercentByCar.Count != carCount || maximumBrakeForceNewtonsByCar!.Count != carCount))
            throw new ArgumentException("Brake commands and ratings must match the consist size.");
        if (throttlePercentByCar.Any(value => !float.IsFinite(value)) ||
            !float.IsFinite(powerReduction) ||
            (averageForceByCar is not null && averageForceByCar.Any(value => !float.IsFinite(value))) ||
            (brakePercentByCar is not null && brakePercentByCar.Any(value => !double.IsFinite(value) || value < 0 || value > 100)) ||
            (maximumBrakeForceNewtonsByCar is not null && maximumBrakeForceNewtonsByCar.Any(value => !double.IsFinite(value) || value < 0)) ||
            !double.IsFinite(targetAccelerationMetersPerSecondSquared))
            throw new ArgumentOutOfRangeException(nameof(throttlePercentByCar),
                "Throttle, power reduction, average force, and target acceleration must be finite.");

        ValidateAdhesionCoefficients(adhesionCoefficientByCar, carCount);
        var couplerDistributor = new TrainConsistCouplerForceDistributor();
        var brakeModel = new TrainCarBrakeModel();
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
                motiveForce = LimitMotiveForce(motiveForce, state.Cars[i], i, adhesionCoefficientByCar);
                var commandedBrake = brakePercentByCar is null ? 0 : Math.Abs(brakeModel.CalculateBrakeRetardForceNewtons(
                    state.Cars[i].SpeedMetersPerSecond, brakePercentByCar[i], maximumBrakeForceNewtonsByCar![i]));
                forces[i] = new TrainCarForceBreakdown(
                    motiveForce,
                    resistance.GravityForceNewtons,
                    resistance.RollingFrictionForceNewtons,
                    resistance.BrakeRetardForceNewtons + commandedBrake,
                    resistance.CurveResistanceForceNewtons,
                    resistance.WindResistanceForceNewtons,
                    resistance.TunnelResistanceForceNewtons);
            }

            var couplerForces = couplerDistributor.CalculatePerCarCouplerForces(
                state, forces, targetAccelerationMetersPerSecondSquared, couplerForceLimits);
            return new TrainConsistForceFrame(forces, couplerForces);
        });
    }

    /// <summary>
    /// Advances the complete pneumatic brake chain on each accepted fixed tick:
    /// driver command -> brake pipe -> per-car cylinders -> braking force.
    /// </summary>
    public int AdvanceWithPneumaticBrakes(
        double elapsedFrameSeconds,
        IReadOnlyList<float> throttlePercentByCar,
        double brakeCommandPercent,
        IReadOnlyList<double> maximumBrakeForceNewtonsByCar,
        Func<int, TrainCarMotionState, TrainCarResistanceForces> resistanceProvider,
        IReadOnlyList<double> couplerForcesNewtons,
        bool emergency = false,
        float powerReduction = 0,
        IReadOnlyList<float>? averageForceByCar = null,
        Func<TrainConsistMotionState, IReadOnlyList<double>>? couplerForceProvider = null,
        int tractionDirection = 1,
        IReadOnlyList<double>? adhesionCoefficientByCar = null,
        IReadOnlyList<double>? additionalBrakeForceNewtonsByCar = null)
    {
        if (!double.IsFinite(brakeCommandPercent) || brakeCommandPercent < 0 || brakeCommandPercent > 100)
            throw new ArgumentOutOfRangeException(nameof(brakeCommandPercent));
        return AdvanceWithBrakeCommands(
            elapsedFrameSeconds,
            throttlePercentByCar,
            pneumaticBrakes.BrakePercentByCar,
            maximumBrakeForceNewtonsByCar,
            resistanceProvider,
            couplerForcesNewtons,
            powerReduction,
            averageForceByCar,
            stepSeconds => pneumaticBrakes.Advance(brakeCommandPercent, stepSeconds, emergency),
            () => pneumaticBrakes.BrakePercentByCar,
            couplerForceProvider,
            tractionDirection,
            adhesionCoefficientByCar,
            additionalBrakeForceNewtonsByCar);
    }

    /// <summary>
    /// Advances a train using the net external force over total consist mass to
    /// derive common acceleration, then balances the internal coupler forces.
    /// Unlike a caller-selected target acceleration, this remains consistent
    /// with the traction and brake forces actually available in the current tick.
    /// </summary>
    public int AdvanceWithAutomaticCouplers(
        double elapsedFrameSeconds,
        IReadOnlyList<float> throttlePercentByCar,
        IReadOnlyList<double> brakePercentByCar,
        IReadOnlyList<double> maximumBrakeForceNewtonsByCar,
        Func<int, TrainCarMotionState, TrainCarResistanceForces> resistanceProvider,
        IReadOnlyList<TrainCouplerForceLimit>? couplerForceLimits = null,
        float powerReduction = 0,
        IReadOnlyList<float>? averageForceByCar = null,
        IReadOnlyList<double>? adhesionCoefficientByCar = null)
    {
        ArgumentNullException.ThrowIfNull(throttlePercentByCar);
        ArgumentNullException.ThrowIfNull(brakePercentByCar);
        ArgumentNullException.ThrowIfNull(maximumBrakeForceNewtonsByCar);
        ArgumentNullException.ThrowIfNull(resistanceProvider);
        var carCount = State.Cars.Count;
        if (throttlePercentByCar.Count != carCount ||
            brakePercentByCar.Count != carCount ||
            maximumBrakeForceNewtonsByCar.Count != carCount ||
            (averageForceByCar is not null && averageForceByCar.Count != carCount))
            throw new ArgumentException("Throttle, brake, brake-rating, and average-force inputs must match the consist size.");
        if (couplerForceLimits is not null && couplerForceLimits.Count != Math.Max(0, carCount - 1))
            throw new ArgumentException("Coupler force limits must match the consist boundary count.", nameof(couplerForceLimits));
        if (throttlePercentByCar.Any(value => !float.IsFinite(value)) ||
            brakePercentByCar.Any(value => !double.IsFinite(value) || value < 0 || value > 100) ||
            maximumBrakeForceNewtonsByCar.Any(value => !double.IsFinite(value) || value < 0) ||
            !float.IsFinite(powerReduction) ||
            (averageForceByCar is not null && averageForceByCar.Any(value => !float.IsFinite(value))))
            throw new ArgumentOutOfRangeException(nameof(throttlePercentByCar),
                "Throttle, brake, power, and average-force inputs must be finite; brake ratings must be non-negative.");

        ValidateAdhesionCoefficients(adhesionCoefficientByCar, carCount);
        var forceModel = new TrainCarForceModel();
        var couplerDistributor = new TrainConsistCouplerForceDistributor();
        var brakeModel = new TrainCarBrakeModel();
        return loop.Advance(elapsedFrameSeconds, state =>
        {
            var forces = new TrainCarForceBreakdown[carCount];
            var totalExternalForce = 0d;
            var totalMass = 0d;
            for (var i = 0; i < carCount; i++)
            {
                var car = state.Cars[i];
                var resistance = resistanceProvider(i, car) ??
                    throw new InvalidOperationException($"Resistance provider returned null for car {i}.");
                var motiveForce = tractionModels[i]?.CalculateTractiveForce(
                    throttlePercentByCar[i],
                    (float)car.SpeedMetersPerSecond,
                    throttleResponses[i],
                    powerReduction,
                    averageForceByCar is null ? 0 : averageForceByCar[i]) ?? 0;
                motiveForce = LimitMotiveForce(motiveForce, car, i, adhesionCoefficientByCar);
                var commandedBrake = Math.Abs(brakeModel.CalculateBrakeRetardForceNewtons(
                    car.SpeedMetersPerSecond, brakePercentByCar[i], maximumBrakeForceNewtonsByCar[i]));
                forces[i] = new TrainCarForceBreakdown(
                    motiveForce,
                    resistance.GravityForceNewtons,
                    resistance.RollingFrictionForceNewtons,
                    resistance.BrakeRetardForceNewtons + commandedBrake,
                    resistance.CurveResistanceForceNewtons,
                    resistance.WindResistanceForceNewtons,
                    resistance.TunnelResistanceForceNewtons);
                totalExternalForce += forceModel.CalculateNetLongitudinalForce(car, forces[i]);
                totalMass += car.MassKg;
            }

            if (!double.IsFinite(totalExternalForce) || !double.IsFinite(totalMass) || totalMass <= 0)
                throw new InvalidOperationException("Consist external force and total mass must remain finite and positive.");
            var acceleration = totalExternalForce / totalMass;
            var couplerForces = couplerDistributor.CalculatePerCarCouplerForces(
                state, forces, acceleration, couplerForceLimits);
            return new TrainConsistForceFrame(forces, couplerForces);
        });
    }

    /// <summary>
    /// Advances pneumatic braking and computes spring/damper coupler forces from
    /// current car positions and velocities on every accepted fixed simulation tick.
    /// Coupler parameters are supplied once per adjacent-car boundary.
    /// </summary>
    public int AdvanceWithPneumaticBrakesAndDynamicCouplers(
        double elapsedFrameSeconds,
        IReadOnlyList<float> throttlePercentByCar,
        double brakeCommandPercent,
        IReadOnlyList<double> maximumBrakeForceNewtonsByCar,
        Func<int, TrainCarMotionState, TrainCarResistanceForces> resistanceProvider,
        IReadOnlyList<double> restGapsMeters,
        IReadOnlyList<double> slackMetersByBoundary,
        IReadOnlyList<double> stiffnessNewtonsPerMeter,
        IReadOnlyList<double> dampingNewtonSecondsPerMeter,
        IReadOnlyList<double> maximumCompressionNewtons,
        IReadOnlyList<double> maximumTensionNewtons,
        bool emergency = false,
        float powerReduction = 0,
        IReadOnlyList<float>? averageForceByCar = null,
        int tractionDirection = 1,
        IReadOnlyList<double>? adhesionCoefficientByCar = null,
        IReadOnlyList<double>? additionalBrakeForceNewtonsByCar = null)
    {
        ArgumentNullException.ThrowIfNull(restGapsMeters);
        ArgumentNullException.ThrowIfNull(slackMetersByBoundary);
        ArgumentNullException.ThrowIfNull(stiffnessNewtonsPerMeter);
        ArgumentNullException.ThrowIfNull(dampingNewtonSecondsPerMeter);
        ArgumentNullException.ThrowIfNull(maximumCompressionNewtons);
        ArgumentNullException.ThrowIfNull(maximumTensionNewtons);

        var couplerDynamics = new TrainConsistCouplerDynamics();
        IReadOnlyList<double> CalculateCouplerForces(TrainConsistMotionState state) =>
            couplerDynamics.CalculatePerCarForces(
                state, restGapsMeters, slackMetersByBoundary,
                stiffnessNewtonsPerMeter, dampingNewtonSecondsPerMeter,
                maximumCompressionNewtons, maximumTensionNewtons);

        // Validate all boundary arrays even when the scheduler accepts no ticks.
        _ = CalculateCouplerForces(State);
        return AdvanceWithPneumaticBrakes(
            elapsedFrameSeconds, throttlePercentByCar, brakeCommandPercent,
            maximumBrakeForceNewtonsByCar, resistanceProvider,
            new double[State.Cars.Count], emergency, powerReduction,
            averageForceByCar, CalculateCouplerForces, tractionDirection,
            adhesionCoefficientByCar, additionalBrakeForceNewtonsByCar);
    }

    private static void ValidateAdhesionCoefficients(IReadOnlyList<double>? coefficients, int carCount)
    {
        if (coefficients is not null &&
            (coefficients.Count != carCount ||
             coefficients.Any(value => !double.IsFinite(value) || value < 0 || value > 1)))
            throw new ArgumentOutOfRangeException(nameof(coefficients),
                "Adhesion coefficients must match the consist and be between zero and one.");
    }

    private static float LimitMotiveForce(float requestedForce, TrainCarMotionState car,
        int carIndex, IReadOnlyList<double>? adhesionCoefficientByCar)
    {
        var coefficient = adhesionCoefficientByCar is null
            ? TrainCarAdhesionModel.DefaultAdhesionCoefficient
            : adhesionCoefficientByCar[carIndex];
        return (float)AdhesionModel.LimitTractiveForce(
            requestedForce, car.MassKg, coefficient);
    }


    public TrainConsistSimulationSnapshot CaptureState()
    {
        var snapshot = loop.CaptureState();
        return snapshot with
        {
            AppliedTractionForceNewtonsByCar = appliedTractionForceByCar.ToArray(),
            BrakePipeState = new TrainBrakePipeState(pneumaticBrakes.PipeState.PressuresKPa),
            BrakeCylinderState = new TrainBrakeCylinderState(pneumaticBrakes.CylinderState.PressuresKPa),
            BrakeDistributorState = new TrainBrakeDistributorState(
                pneumaticBrakes.DistributorState.AuxiliaryReservoirPressuresKPa,
                pneumaticBrakes.DistributorState.Modes,
                pneumaticBrakes.DistributorState.ControlPressuresKPa,
                pneumaticBrakes.DistributorState.LastPipePressuresKPa)
        };
    }

    public void RestoreState(TrainConsistSimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if ((snapshot.BrakePipeState is null) != (snapshot.BrakeCylinderState is null))
            throw new ArgumentException("Pneumatic snapshot must include both brake-pipe and brake-cylinder state.", nameof(snapshot));
        if (snapshot.BrakePipeState is not null)
            pneumaticBrakes.ValidateSnapshot(snapshot.BrakePipeState, snapshot.BrakeCylinderState!, snapshot.BrakeDistributorState);

        if (snapshot.AppliedTractionForceNewtonsByCar is not null &&
            (snapshot.AppliedTractionForceNewtonsByCar.Count != State.Cars.Count ||
             snapshot.AppliedTractionForceNewtonsByCar.Any(value => !float.IsFinite(value))))
            throw new ArgumentException("Traction-ramp snapshot must contain one finite force per vehicle.", nameof(snapshot));

        // Restore the loop first: its motion and clock validation is atomic.
        // Pneumatic state has already been fully validated and cannot fail here.
        loop.RestoreState(snapshot);
        Array.Clear(appliedTractionForceByCar);
        if (snapshot.AppliedTractionForceNewtonsByCar is not null)
            for (var i = 0; i < appliedTractionForceByCar.Length; i++)
                appliedTractionForceByCar[i] = snapshot.AppliedTractionForceNewtonsByCar[i];
        if (snapshot.BrakePipeState is null)
            pneumaticBrakes.Reset();
        else
            pneumaticBrakes.RestoreState(snapshot.BrakePipeState, snapshot.BrakeCylinderState!, snapshot.BrakeDistributorState);
    }

    public void Pause() => loop.Pause();
    public void Resume() => loop.Resume();
    public void Reset(TrainConsistMotionState initialState)
    {
        loop.Reset(initialState);
        Array.Clear(appliedTractionForceByCar);
        pneumaticBrakes.Reset();
    }
}

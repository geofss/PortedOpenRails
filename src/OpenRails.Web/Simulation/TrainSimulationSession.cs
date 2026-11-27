namespace OpenRails.Web.Simulation;

/// <summary>
/// Coordinates consist motion and the per-vehicle pneumatic brake subsystem on
/// the same accepted fixed simulation ticks. Force providers supply native-port
/// traction, gravity, resistance, and coupler calculations; this session injects
/// the live distributor/cylinder brake force before motion integration.
/// </summary>
public sealed class TrainSimulationSession
{
    private readonly TrainConsistSimulationLoop motion;
    private readonly TrainPneumaticBrakeSystem pneumaticBrakes;
    private readonly TrainCarBrakeModel brakeForceModel = new();
    private readonly double[] maximumBrakeForceNewtonsByCar;

    public TrainConsistMotionState State => motion.State;
    public TrainBrakePipeState BrakePipeState => pneumaticBrakes.PipeState;
    public TrainBrakeCylinderState BrakeCylinderState => pneumaticBrakes.CylinderState;
    public TrainBrakeDistributorState BrakeDistributorState => pneumaticBrakes.DistributorState;
    public IReadOnlyList<double> BrakePercentByCar => pneumaticBrakes.BrakePercentByCar;
    public double SimulationTimeSeconds => motion.SimulationTimeSeconds;
    public double InterpolationAlpha => motion.InterpolationAlpha;
    public double DroppedTimeSeconds => motion.DroppedTimeSeconds;
    public bool IsPaused => motion.IsPaused;

    public TrainSimulationSession(
        TrainConsistMotionState initialState,
        IReadOnlyList<double> maximumBrakeForceNewtonsByCar,
        IReadOnlyList<RollingStockBrakeProfile>? brakeProfilesByCar = null,
        double fixedStepSeconds = 1d / 60d,
        double maximumFrameSeconds = 0.25d,
        int maximumStepsPerFrame = 8,
        double? routeLengthMeters = null)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(maximumBrakeForceNewtonsByCar);
        if (maximumBrakeForceNewtonsByCar.Count != initialState.Cars.Count)
            throw new ArgumentException("Brake ratings must match the consist vehicle count.",
                nameof(maximumBrakeForceNewtonsByCar));
        if (maximumBrakeForceNewtonsByCar.Any(force => !double.IsFinite(force) || force < 0))
            throw new ArgumentOutOfRangeException(nameof(maximumBrakeForceNewtonsByCar));
        if (brakeProfilesByCar is not null && brakeProfilesByCar.Count != initialState.Cars.Count)
            throw new ArgumentException("Brake profiles must match the consist vehicle count.",
                nameof(brakeProfilesByCar));

        this.maximumBrakeForceNewtonsByCar = maximumBrakeForceNewtonsByCar.ToArray();
        pneumaticBrakes = new TrainPneumaticBrakeSystem(
            initialState.Cars.Count, brakeProfilesByCar: brakeProfilesByCar);
        motion = new TrainConsistSimulationLoop(
            initialState, fixedStepSeconds, maximumFrameSeconds,
            maximumStepsPerFrame, routeLengthMeters);
    }

    /// <summary>
    /// Advances pneumatic propagation before each accepted motion tick, then
    /// adds each vehicle's current cylinder-derived brake force to its force frame.
    /// </summary>
    public int Advance(
        double elapsedFrameSeconds,
        double brakeCommandPercent,
        Func<TrainConsistMotionState, TrainConsistForceFrame> forceProvider,
        bool emergencyBrake = false,
        Action<double>? beforeStep = null)
    {
        ArgumentNullException.ThrowIfNull(forceProvider);
        if (!double.IsFinite(brakeCommandPercent) ||
            brakeCommandPercent < 0 || brakeCommandPercent > 100)
            throw new ArgumentOutOfRangeException(nameof(brakeCommandPercent));

        return motion.Advance(
            elapsedFrameSeconds,
            state => AddPneumaticBrakeForces(state, forceProvider(state)),
            stepSeconds =>
            {
                pneumaticBrakes.Advance(brakeCommandPercent, stepSeconds, emergencyBrake);
                beforeStep?.Invoke(stepSeconds);
            });
    }

    public TrainConsistSimulationSnapshot CaptureState() =>
        new(
            new TrainConsistMotionState(State.Cars.ToArray()),
            motion.CaptureState().ClockState,
            new TrainBrakePipeState(BrakePipeState.PressuresKPa),
            new TrainBrakeCylinderState(BrakeCylinderState.PressuresKPa),
            new TrainBrakeDistributorState(
                BrakeDistributorState.AuxiliaryReservoirPressuresKPa,
                BrakeDistributorState.Modes,
                BrakeDistributorState.ControlPressuresKPa,
                BrakeDistributorState.LastPipePressuresKPa));

    public void RestoreState(TrainConsistSimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.BrakePipeState is null || snapshot.BrakeCylinderState is null ||
            snapshot.BrakeDistributorState is null)
            throw new ArgumentException("A train-session snapshot must include all pneumatic brake states.",
                nameof(snapshot));

        // Validate pneumatic state before mutating the motion clock or consist.
        pneumaticBrakes.ValidateSnapshot(
            snapshot.BrakePipeState, snapshot.BrakeCylinderState, snapshot.BrakeDistributorState);
        motion.RestoreState(snapshot);
        pneumaticBrakes.RestoreState(
            snapshot.BrakePipeState, snapshot.BrakeCylinderState, snapshot.BrakeDistributorState);
    }

    public void Pause() => motion.Pause();
    public void Resume() => motion.Resume();

    public void Reset(TrainConsistMotionState initialState)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        if (initialState.Cars.Count != maximumBrakeForceNewtonsByCar.Length)
            throw new ArgumentException("Reset consist size must match the configured brake ratings.",
                nameof(initialState));
        motion.Reset(initialState);
        pneumaticBrakes.Reset();
    }

    private TrainConsistForceFrame AddPneumaticBrakeForces(
        TrainConsistMotionState state,
        TrainConsistForceFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var count = state.Cars.Count;
        if (frame.CarForces is null || frame.CouplerForcesNewtons is null ||
            frame.CarForces.Count != count ||
            frame.CouplerForcesNewtons.Count != Math.Max(0, count - 1))
            throw new ArgumentException("Force frame must contain one force set per car and one force per coupler.",
                nameof(frame));

        var brakePercent = pneumaticBrakes.BrakePercentByCar;
        var forces = new TrainCarForceBreakdown[count];
        for (var i = 0; i < count; i++)
        {
            var car = state.Cars[i];
            var source = frame.CarForces[i] ??
                throw new ArgumentException("A car force entry cannot be null.", nameof(frame));
            var brakeForce = Math.Abs(brakeForceModel.CalculateBrakeRetardForceNewtons(
                car.SpeedMetersPerSecond, brakePercent[i], maximumBrakeForceNewtonsByCar[i]));
            forces[i] = source with
            {
                BrakeRetardForceNewtons = source.BrakeRetardForceNewtons + brakeForce
            };
        }

        return new TrainConsistForceFrame(forces, frame.CouplerForcesNewtons);
    }
}

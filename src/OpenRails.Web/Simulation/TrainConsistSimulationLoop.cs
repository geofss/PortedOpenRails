namespace OpenRails.Web.Simulation;

/// <summary>
/// Fixed-step browser simulation loop for a multi-car consist. The force-frame
/// provider supplies per-car locomotive/gravity/resistance forces and signed
/// coupler forces; the loop owns deterministic timing and state integration.
/// </summary>
public sealed class TrainConsistSimulationLoop
{
    private readonly FixedStepSimulationScheduler scheduler;
    private readonly TrainConsistMotionIntegrator integrator = new();
    private readonly double? routeLengthMeters;

    public TrainConsistMotionState State { get; private set; }
    public double SimulationTimeSeconds => scheduler.SimulationTimeSeconds;
    public double InterpolationAlpha => scheduler.InterpolationAlpha;
    public double DroppedTimeSeconds => scheduler.DroppedTimeSeconds;
    public bool IsPaused => scheduler.IsPaused;

    public TrainConsistSimulationLoop(
        TrainConsistMotionState initialState,
        double fixedStepSeconds = 1d / 60d,
        double maximumFrameSeconds = 0.25d,
        int maximumStepsPerFrame = 8,
        double? routeLengthMeters = null)
    {
        State = ValidateState(initialState ?? throw new ArgumentNullException(nameof(initialState)));
        if (routeLengthMeters is not null &&
            (!double.IsFinite(routeLengthMeters.Value) || routeLengthMeters.Value <= 0))
            throw new ArgumentOutOfRangeException(nameof(routeLengthMeters));
        this.routeLengthMeters = routeLengthMeters;
        scheduler = new FixedStepSimulationScheduler(
            fixedStepSeconds, maximumFrameSeconds, maximumStepsPerFrame);
    }

    public int Advance(
        double elapsedFrameSeconds,
        Func<TrainConsistMotionState, TrainConsistForceFrame> forceProvider,
        Action<double>? beforeStep = null)
    {
        ArgumentNullException.ThrowIfNull(forceProvider);
        return scheduler.Advance(elapsedFrameSeconds, stepSeconds =>
        {
            beforeStep?.Invoke(stepSeconds);
            var frame = forceProvider(State);
            ArgumentNullException.ThrowIfNull(frame);
            State = integrator.Step(
                State, frame.CarForces, frame.CouplerForcesNewtons, stepSeconds);
            if (routeLengthMeters is not null)
                State = ClampCarsToRoute(State, routeLengthMeters.Value);
        });
    }

    public void Pause() => scheduler.Pause();

    public void Resume() => scheduler.Resume();

    public void Reset(TrainConsistMotionState initialState)
    {
        State = ValidateState(initialState ?? throw new ArgumentNullException(nameof(initialState)));
        scheduler.Reset();
    }

    private static TrainConsistMotionState ClampCarsToRoute(
        TrainConsistMotionState state,
        double routeLengthMeters)
    {
        var cars = state.Cars.Select(car =>
        {
            if (car.DistanceMeters <= 0 && car.SpeedMetersPerSecond < 0)
                return car with { DistanceMeters = 0, SpeedMetersPerSecond = 0 };
            if (car.DistanceMeters >= routeLengthMeters && car.SpeedMetersPerSecond > 0)
                return car with { DistanceMeters = routeLengthMeters, SpeedMetersPerSecond = 0 };
            if (car.DistanceMeters < 0)
                return car with { DistanceMeters = 0 };
            if (car.DistanceMeters > routeLengthMeters)
                return car with { DistanceMeters = routeLengthMeters };
            return car;
        }).ToArray();
        return new TrainConsistMotionState(cars);
    }

    private static TrainConsistMotionState ValidateState(TrainConsistMotionState state)
    {
        if (state.Cars.Count == 0)
            throw new ArgumentException("A consist simulation requires at least one car.", nameof(state));
        foreach (var car in state.Cars)
        {
            if (!double.IsFinite(car.MassKg) || car.MassKg <= 0 ||
                !double.IsFinite(car.SpeedMetersPerSecond) || !double.IsFinite(car.DistanceMeters))
                throw new ArgumentException("Initial car mass must be positive and motion state values finite.", nameof(state));
        }
        return state;
    }
}

public sealed record TrainConsistForceFrame(
    IReadOnlyList<TrainCarForceBreakdown> CarForces,
    IReadOnlyList<double> CouplerForcesNewtons);

namespace OpenRails.Web.Simulation;

/// <summary>
/// Runs a single rolling-stock vehicle through the browser-safe fixed-step clock.
/// The supplied force provider is the extension point for ported locomotive,
/// brake, resistance, and environment subsystems.
/// </summary>
public sealed class TrainCarSimulationLoop
{
    private readonly FixedStepSimulationScheduler scheduler;
    private readonly TrainCarForceModel forceModel = new();
    private readonly TrainCarMotionIntegrator integrator = new();

    public TrainCarMotionState State { get; private set; }
    public double SimulationTimeSeconds => scheduler.SimulationTimeSeconds;
    public double InterpolationAlpha => scheduler.InterpolationAlpha;
    public double DroppedTimeSeconds => scheduler.DroppedTimeSeconds;
    public bool IsPaused => scheduler.IsPaused;

    public TrainCarSimulationLoop(
        TrainCarMotionState initialState,
        double fixedStepSeconds = 1d / 60d,
        double maximumFrameSeconds = 0.25d,
        int maximumStepsPerFrame = 8)
    {
        State = initialState ?? throw new ArgumentNullException(nameof(initialState));
        scheduler = new FixedStepSimulationScheduler(
            fixedStepSeconds, maximumFrameSeconds, maximumStepsPerFrame);
    }

    public int Advance(
        double elapsedFrameSeconds,
        Func<TrainCarMotionState, TrainCarForceBreakdown> forceProvider)
    {
        ArgumentNullException.ThrowIfNull(forceProvider);
        return scheduler.Advance(elapsedFrameSeconds, stepSeconds =>
        {
            var forces = forceProvider(State);
            var netForce = forceModel.CalculateNetLongitudinalForce(State, forces);
            State = integrator.Step(State, netForce, stepSeconds).State;
        });
    }


    public TrainCarSimulationSnapshot CaptureState() =>
        new(State with { }, scheduler.CaptureState());

    public void RestoreState(TrainCarSimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var state = snapshot.MotionState;
        if (!double.IsFinite(state.MassKg) || state.MassKg <= 0 ||
            !double.IsFinite(state.SpeedMetersPerSecond) ||
            !double.IsFinite(state.DistanceMeters))
            throw new ArgumentException("Snapshot car motion must have finite values and positive mass.", nameof(snapshot));
        State = state with { };
        scheduler.RestoreState(snapshot.ClockState);
    }

    public void Pause() => scheduler.Pause();

    public void Resume() => scheduler.Resume();

    public void Reset(TrainCarMotionState initialState)
    {
        State = initialState ?? throw new ArgumentNullException(nameof(initialState));
        scheduler.Reset();
    }
}

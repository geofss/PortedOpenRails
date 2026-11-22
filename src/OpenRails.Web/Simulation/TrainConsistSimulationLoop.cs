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

    public TrainConsistMotionState State { get; private set; }
    public double SimulationTimeSeconds => scheduler.SimulationTimeSeconds;
    public double InterpolationAlpha => scheduler.InterpolationAlpha;
    public double DroppedTimeSeconds => scheduler.DroppedTimeSeconds;
    public bool IsPaused => scheduler.IsPaused;

    public TrainConsistSimulationLoop(
        TrainConsistMotionState initialState,
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
        Func<TrainConsistMotionState, TrainConsistForceFrame> forceProvider)
    {
        ArgumentNullException.ThrowIfNull(forceProvider);
        return scheduler.Advance(elapsedFrameSeconds, stepSeconds =>
        {
            var frame = forceProvider(State);
            ArgumentNullException.ThrowIfNull(frame);
            State = integrator.Step(
                State, frame.CarForces, frame.CouplerForcesNewtons, stepSeconds);
        });
    }

    public void Pause() => scheduler.Pause();

    public void Resume() => scheduler.Resume();

    public void Reset(TrainConsistMotionState initialState)
    {
        State = initialState ?? throw new ArgumentNullException(nameof(initialState));
        scheduler.Reset();
    }
}

public sealed record TrainConsistForceFrame(
    IReadOnlyList<TrainCarForceBreakdown> CarForces,
    IReadOnlyList<double> CouplerForcesNewtons);

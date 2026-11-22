namespace OpenRails.Web.Simulation;

/// <summary>
/// Browser-friendly fixed-step simulation clock. The browser supplies elapsed frame
/// time; this class bounds catch-up work so background-tab throttling cannot trigger
/// an unbounded simulation loop.
/// </summary>
public sealed class FixedStepSimulationScheduler
{
    private readonly double fixedStepSeconds;
    private readonly double maximumFrameSeconds;
    private readonly int maximumStepsPerFrame;
    private double accumulatorSeconds;

    public double SimulationTimeSeconds { get; private set; }
    public double InterpolationAlpha => Math.Clamp(accumulatorSeconds / fixedStepSeconds, 0, 1);
    public double DroppedTimeSeconds { get; private set; }
    public bool IsPaused { get; private set; }
    public int LastStepsExecuted { get; private set; }

    public FixedStepSimulationScheduler(
        double fixedStepSeconds = 1d / 60d,
        double maximumFrameSeconds = 0.25d,
        int maximumStepsPerFrame = 8)
    {
        if (!double.IsFinite(fixedStepSeconds) || fixedStepSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(fixedStepSeconds));
        if (!double.IsFinite(maximumFrameSeconds) || maximumFrameSeconds < fixedStepSeconds)
            throw new ArgumentOutOfRangeException(nameof(maximumFrameSeconds));
        if (maximumStepsPerFrame < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumStepsPerFrame));

        this.fixedStepSeconds = fixedStepSeconds;
        this.maximumFrameSeconds = maximumFrameSeconds;
        this.maximumStepsPerFrame = maximumStepsPerFrame;
    }

    public int Advance(double elapsedFrameSeconds, Action<double> simulationStep)
    {
        ArgumentNullException.ThrowIfNull(simulationStep);
        if (!double.IsFinite(elapsedFrameSeconds) || elapsedFrameSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedFrameSeconds));

        LastStepsExecuted = 0;
        if (IsPaused || elapsedFrameSeconds == 0)
            return 0;

        var acceptedFrameTime = Math.Min(elapsedFrameSeconds, maximumFrameSeconds);
        DroppedTimeSeconds += elapsedFrameSeconds - acceptedFrameTime;
        accumulatorSeconds += acceptedFrameTime;

        while (accumulatorSeconds + 1e-12 >= fixedStepSeconds &&
            LastStepsExecuted < maximumStepsPerFrame)
        {
            simulationStep(fixedStepSeconds);
            accumulatorSeconds = Math.Max(0, accumulatorSeconds - fixedStepSeconds);
            SimulationTimeSeconds += fixedStepSeconds;
            LastStepsExecuted++;
        }

        if (accumulatorSeconds >= fixedStepSeconds)
        {
            var retainedFraction = accumulatorSeconds % fixedStepSeconds;
            DroppedTimeSeconds += accumulatorSeconds - retainedFraction;
            accumulatorSeconds = retainedFraction;
        }

        return LastStepsExecuted;
    }

    public void Pause() => IsPaused = true;

    public void Resume() => IsPaused = false;

    public void Reset()
    {
        accumulatorSeconds = 0;
        SimulationTimeSeconds = 0;
        DroppedTimeSeconds = 0;
        LastStepsExecuted = 0;
        IsPaused = false;
    }
}

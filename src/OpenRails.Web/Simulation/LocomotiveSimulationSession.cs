using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

/// <summary>
/// Connects a parsed locomotive traction model to the fixed-step single-car
/// simulation loop. Environmental and braking forces are supplied separately.
/// </summary>
public sealed class LocomotiveSimulationSession
{
    private readonly TrainCarSimulationLoop loop;
    private readonly LocomotiveTractionModel tractionModel;
    private readonly LocomotiveThrottleResponse throttleResponse;

    public TrainCarMotionState State => loop.State;
    public double SimulationTimeSeconds => loop.SimulationTimeSeconds;
    public double InterpolationAlpha => loop.InterpolationAlpha;
    public double DroppedTimeSeconds => loop.DroppedTimeSeconds;
    public bool IsPaused => loop.IsPaused;

    public LocomotiveSimulationSession(
        LocomotiveTractionMetadata metadata,
        TrainCarMotionState initialState,
        LocomotiveThrottleResponse throttleResponse = LocomotiveThrottleResponse.Linear,
        double fixedStepSeconds = 1d / 60d,
        double maximumFrameSeconds = 0.25d,
        int maximumStepsPerFrame = 8)
    {
        tractionModel = new LocomotiveTractionModel(metadata);
        this.throttleResponse = throttleResponse;
        loop = new TrainCarSimulationLoop(
            initialState, fixedStepSeconds, maximumFrameSeconds, maximumStepsPerFrame);
    }

    public int Advance(
        double elapsedFrameSeconds,
        float throttlePercent,
        Func<TrainCarMotionState, TrainCarResistanceForces> resistanceProvider,
        float powerReduction = 0,
        float averageForceNewtons = 0)
    {
        ArgumentNullException.ThrowIfNull(resistanceProvider);
        return loop.Advance(elapsedFrameSeconds, state =>
        {
            var resistance = resistanceProvider(state);
            ArgumentNullException.ThrowIfNull(resistance);
            var motiveForce = tractionModel.CalculateTractiveForce(
                throttlePercent, (float)state.SpeedMetersPerSecond,
                throttleResponse, powerReduction, averageForceNewtons);
            return new TrainCarForceBreakdown(
                motiveForce,
                resistance.GravityForceNewtons,
                resistance.RollingFrictionForceNewtons,
                resistance.BrakeRetardForceNewtons,
                resistance.CurveResistanceForceNewtons,
                resistance.WindResistanceForceNewtons,
                resistance.TunnelResistanceForceNewtons);
        });
    }

    /// <summary>
    /// Advances with a driver brake command and rated brake force, adding the
    /// generated retarding force to the provider's other resistance inputs.
    /// </summary>
    public int AdvanceWithBrakeCommand(
        double elapsedFrameSeconds,
        float throttlePercent,
        double brakePercent,
        double maximumBrakeForceNewtons,
        Func<TrainCarMotionState, TrainCarResistanceForces> resistanceProvider,
        float powerReduction = 0,
        float averageForceNewtons = 0)
    {
        ArgumentNullException.ThrowIfNull(resistanceProvider);
        if (!double.IsFinite(brakePercent) || !double.IsFinite(maximumBrakeForceNewtons) ||
            maximumBrakeForceNewtons < 0)
            throw new ArgumentOutOfRangeException(nameof(brakePercent),
                "Brake command and rating must be finite; rating cannot be negative.");
        var brakeModel = new TrainCarBrakeModel();
        return loop.Advance(elapsedFrameSeconds, state =>
        {
            var resistance = resistanceProvider(state) ??
                throw new InvalidOperationException("Resistance provider returned null.");
            var motiveForce = tractionModel.CalculateTractiveForce(
                throttlePercent, (float)state.SpeedMetersPerSecond,
                throttleResponse, powerReduction, averageForceNewtons);
            var commandedBrakeForce = Math.Abs(brakeModel.CalculateBrakeRetardForceNewtons(
                state.SpeedMetersPerSecond, brakePercent, maximumBrakeForceNewtons));
            return new TrainCarForceBreakdown(
                motiveForce,
                resistance.GravityForceNewtons,
                resistance.RollingFrictionForceNewtons,
                resistance.BrakeRetardForceNewtons + commandedBrakeForce,
                resistance.CurveResistanceForceNewtons,
                resistance.WindResistanceForceNewtons,
                resistance.TunnelResistanceForceNewtons);
        });
    }


    public TrainCarSimulationSnapshot CaptureState() => loop.CaptureState();

    public void RestoreState(TrainCarSimulationSnapshot snapshot) => loop.RestoreState(snapshot);

    public void Pause() => loop.Pause();

    public void Resume() => loop.Resume();

    public void Reset(TrainCarMotionState initialState) => loop.Reset(initialState);
}

public sealed record TrainCarResistanceForces(
    double GravityForceNewtons,
    double RollingFrictionForceNewtons,
    double BrakeRetardForceNewtons,
    double CurveResistanceForceNewtons,
    double WindResistanceForceNewtons,
    double TunnelResistanceForceNewtons);

namespace OpenRails.Web.Simulation;

/// <summary>
/// Brake-pipe pressure for each vehicle, ordered front-to-rear in consist order.
/// </summary>
public sealed record TrainBrakePipeState
{
    public IReadOnlyList<double> PressuresKPa { get; }

    public TrainBrakePipeState(IEnumerable<double> pressuresKPa)
    {
        ArgumentNullException.ThrowIfNull(pressuresKPa);
        PressuresKPa = Array.AsReadOnly(pressuresKPa.ToArray());
    }
}

/// <summary>
/// A simplified pneumatic brake-pipe propagation model. The driver's command
/// changes pressure at the leading vehicle first; downstream vehicles follow
/// with a configurable propagation delay. It is not a complete Open Rails
/// triple-valve or reservoir model, but avoids instantaneous whole-train braking.
/// </summary>
public sealed class TrainBrakePipeModel
{
    public const double NormalPressureKPa = 500;
    public const double MinimumServicePressureKPa = 100;

    private readonly double applicationTimeConstantSeconds;
    private readonly double releaseTimeConstantSeconds;
    private readonly double propagationTimeConstantSeconds;

    public TrainBrakePipeModel(
        double applicationTimeConstantSeconds = 0.5,
        double releaseTimeConstantSeconds = 1.2,
        double propagationTimeConstantSeconds = 0.8)
    {
        if (!double.IsFinite(applicationTimeConstantSeconds) || applicationTimeConstantSeconds <= 0 ||
            !double.IsFinite(releaseTimeConstantSeconds) || releaseTimeConstantSeconds <= 0 ||
            !double.IsFinite(propagationTimeConstantSeconds) || propagationTimeConstantSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(applicationTimeConstantSeconds),
                "Brake-pipe time constants must be finite and positive.");

        this.applicationTimeConstantSeconds = applicationTimeConstantSeconds;
        this.releaseTimeConstantSeconds = releaseTimeConstantSeconds;
        this.propagationTimeConstantSeconds = propagationTimeConstantSeconds;
    }

    public TrainBrakePipeState CreateInitialState(int carCount)
    {
        if (carCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(carCount));
        return new TrainBrakePipeState(Enumerable.Repeat(NormalPressureKPa, carCount));
    }

    public TrainBrakePipeState Advance(
        TrainBrakePipeState state,
        double brakeCommandPercent,
        double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.PressuresKPa.Count == 0 ||
            state.PressuresKPa.Any(pressure => !double.IsFinite(pressure) ||
                pressure < MinimumServicePressureKPa || pressure > NormalPressureKPa))
            throw new ArgumentException("Brake-pipe pressures must be between service minimum and normal pressure.", nameof(state));
        if (!double.IsFinite(brakeCommandPercent) || brakeCommandPercent < 0 || brakeCommandPercent > 100)
            throw new ArgumentOutOfRangeException(nameof(brakeCommandPercent));
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

        if (elapsedSeconds == 0)
            return state;

        var targetPressure = NormalPressureKPa -
            (NormalPressureKPa - MinimumServicePressureKPa) * brakeCommandPercent / 100;
        var steps = Math.Max(1, (int)Math.Ceiling(elapsedSeconds / (1d / 60d)));
        var stepSeconds = elapsedSeconds / steps;
        var pressures = state.PressuresKPa.ToArray();
        var next = new double[pressures.Length];

        for (var step = 0; step < steps; step++)
        {
            var firstTau = targetPressure < pressures[0]
                ? applicationTimeConstantSeconds
                : releaseTimeConstantSeconds;
            next[0] = Approach(pressures[0], targetPressure, stepSeconds, firstTau);

            for (var i = 1; i < pressures.Length; i++)
            {
                var upstreamPressure = next[i - 1];
                var tau = upstreamPressure < pressures[i]
                    ? propagationTimeConstantSeconds
                    : propagationTimeConstantSeconds * 1.5;
                next[i] = Approach(pressures[i], upstreamPressure, stepSeconds, tau);
            }

            (pressures, next) = (next, pressures);
        }

        return new TrainBrakePipeState(pressures);
    }

    public IReadOnlyList<double> GetBrakePercentByCar(TrainBrakePipeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.PressuresKPa.Count == 0 ||
            state.PressuresKPa.Any(pressure => !double.IsFinite(pressure) ||
                pressure < MinimumServicePressureKPa || pressure > NormalPressureKPa))
            throw new ArgumentException("Brake-pipe pressures must be between service minimum and normal pressure.", nameof(state));
        return Array.AsReadOnly(state.PressuresKPa.Select(pressure =>
            Math.Clamp(
                (NormalPressureKPa - pressure) /
                (NormalPressureKPa - MinimumServicePressureKPa) * 100,
                0, 100)).ToArray());
    }

    private static double Approach(double current, double target, double elapsed, double timeConstant)
    {
        var alpha = 1 - Math.Exp(-elapsed / timeConstant);
        return Math.Clamp(current + (target - current) * alpha,
            MinimumServicePressureKPa, NormalPressureKPa);
    }
}

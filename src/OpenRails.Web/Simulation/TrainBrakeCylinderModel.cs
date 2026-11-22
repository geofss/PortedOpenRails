namespace OpenRails.Web.Simulation;

/// <summary>
/// Per-vehicle brake-cylinder pressure. Pressure builds after brake-pipe
/// reduction and exhausts gradually when the pipe is recharged.
/// </summary>
public sealed record TrainBrakeCylinderState(IReadOnlyList<double> PressuresKPa)
{
    public TrainBrakeCylinderState(IEnumerable<double> pressuresKPa)
        : this(Array.AsReadOnly(pressuresKPa?.ToArray() ??
            throw new ArgumentNullException(nameof(pressuresKPa))))
    {
    }
}

/// <summary>
/// Models the delayed pneumatic response between a train brake pipe and each
/// vehicle's brake cylinder. This is a simplified reservoir/valve response,
/// not a full triple-valve, auxiliary-reservoir, or pressure-lap simulation.
/// </summary>
public sealed class TrainBrakeCylinderModel
{
    public const double MaximumCylinderPressureKPa = 380;

    private readonly double applicationTimeConstantSeconds;
    private readonly double releaseTimeConstantSeconds;

    public TrainBrakeCylinderModel(
        double applicationTimeConstantSeconds = 0.8,
        double releaseTimeConstantSeconds = 1.5)
    {
        if (!double.IsFinite(applicationTimeConstantSeconds) || applicationTimeConstantSeconds <= 0 ||
            !double.IsFinite(releaseTimeConstantSeconds) || releaseTimeConstantSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(applicationTimeConstantSeconds),
                "Brake-cylinder time constants must be finite and positive.");
        this.applicationTimeConstantSeconds = applicationTimeConstantSeconds;
        this.releaseTimeConstantSeconds = releaseTimeConstantSeconds;
    }

    public TrainBrakeCylinderState CreateInitialState(int carCount)
    {
        if (carCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(carCount));
        return new TrainBrakeCylinderState(Enumerable.Repeat(0d, carCount));
    }

    public TrainBrakeCylinderState Advance(
        TrainBrakeCylinderState state,
        IReadOnlyList<double> brakePipeBrakePercentByCar,
        double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(brakePipeBrakePercentByCar);
        if (state.PressuresKPa.Count == 0 ||
            state.PressuresKPa.Any(value => !double.IsFinite(value) ||
                value < 0 || value > MaximumCylinderPressureKPa))
            throw new ArgumentException("Brake-cylinder pressures must be finite and within the model range.", nameof(state));
        if (brakePipeBrakePercentByCar.Count != state.PressuresKPa.Count)
            throw new ArgumentException("Brake-pipe commands must match the vehicle count.", nameof(brakePipeBrakePercentByCar));
        if (brakePipeBrakePercentByCar.Any(value => !double.IsFinite(value) || value < 0 || value > 100))
            throw new ArgumentOutOfRangeException(nameof(brakePipeBrakePercentByCar));
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (elapsedSeconds == 0)
            return state;

        var steps = Math.Max(1, (int)Math.Ceiling(elapsedSeconds / (1d / 60d)));
        var stepSeconds = elapsedSeconds / steps;
        var pressures = state.PressuresKPa.ToArray();
        for (var step = 0; step < steps; step++)
        {
            for (var i = 0; i < pressures.Length; i++)
            {
                var target = MaximumCylinderPressureKPa * brakePipeBrakePercentByCar[i] / 100d;
                var tau = target > pressures[i]
                    ? applicationTimeConstantSeconds
                    : releaseTimeConstantSeconds;
                var alpha = 1 - Math.Exp(-stepSeconds / tau);
                pressures[i] = Math.Clamp(
                    pressures[i] + (target - pressures[i]) * alpha,
                    0, MaximumCylinderPressureKPa);
            }
        }
        return new TrainBrakeCylinderState(pressures);
    }

    public IReadOnlyList<double> GetBrakePercentByCar(TrainBrakeCylinderState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Array.AsReadOnly(state.PressuresKPa.Select(pressure =>
            Math.Clamp(pressure / MaximumCylinderPressureKPa * 100, 0, 100)).ToArray());
    }
}

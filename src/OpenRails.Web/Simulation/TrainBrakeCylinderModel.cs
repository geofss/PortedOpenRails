using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenRails.Web.Simulation;

/// <summary>
/// Per-vehicle brake-cylinder pressure. Pressure builds after brake-pipe
/// reduction and exhausts gradually when the pipe is recharged.
/// </summary>
[JsonConverter(typeof(TrainBrakeCylinderStateJsonConverter))]
public sealed record TrainBrakeCylinderState
{
    public IReadOnlyList<double> PressuresKPa { get; }

    public TrainBrakeCylinderState(IEnumerable<double> pressuresKPa)
    {
        ArgumentNullException.ThrowIfNull(pressuresKPa);
        PressuresKPa = Array.AsReadOnly(pressuresKPa.ToArray());
    }
}

internal sealed class TrainBrakeCylinderStateJsonConverter : JsonConverter<TrainBrakeCylinderState>
{
    public override TrainBrakeCylinderState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var values = document.RootElement.GetProperty(nameof(TrainBrakeCylinderState.PressuresKPa))
            .EnumerateArray().Select(value => value.GetDouble()).ToArray();
        return new TrainBrakeCylinderState(values);
    }

    public override void Write(Utf8JsonWriter writer, TrainBrakeCylinderState value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName(nameof(TrainBrakeCylinderState.PressuresKPa));
        JsonSerializer.Serialize(writer, value.PressuresKPa, options);
        writer.WriteEndObject();
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

        // Each vehicle approaches a fixed target independently during this
        // update. The exponential relaxation has a closed-form solution, so one
        // calculation per vehicle is exact and avoids time-step loops entirely.
        var pressures = new double[state.PressuresKPa.Count];
        for (var i = 0; i < pressures.Length; i++)
        {
            var current = state.PressuresKPa[i];
            var target = MaximumCylinderPressureKPa * brakePipeBrakePercentByCar[i] / 100d;
            var tau = target > current
                ? applicationTimeConstantSeconds
                : releaseTimeConstantSeconds;
            var remainingFraction = Math.Exp(-elapsedSeconds / tau);
            pressures[i] = Math.Clamp(
                target + (current - target) * remainingFraction,
                0, MaximumCylinderPressureKPa);
        }
        return new TrainBrakeCylinderState(pressures);
    }

    public IReadOnlyList<double> GetBrakePercentByCar(TrainBrakeCylinderState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.PressuresKPa.Count == 0 ||
            state.PressuresKPa.Any(pressure => !double.IsFinite(pressure) ||
                pressure < 0 || pressure > MaximumCylinderPressureKPa))
            throw new ArgumentException("Brake-cylinder pressures must be finite and within the model range.", nameof(state));
        return Array.AsReadOnly(state.PressuresKPa.Select(pressure =>
            Math.Clamp(pressure / MaximumCylinderPressureKPa * 100, 0, 100)).ToArray());
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenRails.Web.Simulation;

public enum TrainBrakeDistributorMode
{
    Release,
    Apply,
    Lap
}

/// <summary>Per-vehicle auxiliary reservoir and distributor state.</summary>
[JsonConverter(typeof(TrainBrakeDistributorStateJsonConverter))]
public sealed record TrainBrakeDistributorState
{
    public IReadOnlyList<double> AuxiliaryReservoirPressuresKPa { get; }
    public IReadOnlyList<TrainBrakeDistributorMode> Modes { get; }

    public TrainBrakeDistributorState(
        IEnumerable<double> auxiliaryReservoirPressuresKPa,
        IEnumerable<TrainBrakeDistributorMode> modes)
    {
        ArgumentNullException.ThrowIfNull(auxiliaryReservoirPressuresKPa);
        ArgumentNullException.ThrowIfNull(modes);
        AuxiliaryReservoirPressuresKPa = Array.AsReadOnly(auxiliaryReservoirPressuresKPa.ToArray());
        Modes = Array.AsReadOnly(modes.ToArray());
        if (AuxiliaryReservoirPressuresKPa.Count == 0 ||
            AuxiliaryReservoirPressuresKPa.Count != Modes.Count ||
            AuxiliaryReservoirPressuresKPa.Any(p => !double.IsFinite(p) || p < 0 || p > TrainBrakePipeModel.NormalPressureKPa) ||
            Modes.Any(m => !Enum.IsDefined(m)))
            throw new ArgumentException("Distributor state must contain matching, valid per-car reservoir pressures and modes.");
    }
}

internal sealed class TrainBrakeDistributorStateJsonConverter : JsonConverter<TrainBrakeDistributorState>
{
    public override TrainBrakeDistributorState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        return new TrainBrakeDistributorState(
            root.GetProperty(nameof(TrainBrakeDistributorState.AuxiliaryReservoirPressuresKPa)).EnumerateArray().Select(x => x.GetDouble()),
            root.GetProperty(nameof(TrainBrakeDistributorState.Modes)).EnumerateArray().Select(x => (TrainBrakeDistributorMode)x.GetInt32()));
    }

    public override void Write(Utf8JsonWriter writer, TrainBrakeDistributorState value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName(nameof(TrainBrakeDistributorState.AuxiliaryReservoirPressuresKPa));
        JsonSerializer.Serialize(writer, value.AuxiliaryReservoirPressuresKPa, options);
        writer.WritePropertyName(nameof(TrainBrakeDistributorState.Modes));
        JsonSerializer.Serialize(writer, value.Modes, options);
        writer.WriteEndObject();
    }
}

/// <summary>
/// Simplified per-car triple-valve distributor: pipe reductions charge the
/// cylinder from the auxiliary reservoir, stable pipe pressure laps the valve,
/// and pipe recharge releases the cylinder while replenishing the reservoir.
/// This is a browser-port approximation, not the complete MSTS/ORTS valve family.
/// </summary>
public sealed class TrainBrakeDistributorModel
{
    public const double MinimumApplicationDifferentialKPa = 5;
    private readonly double applicationTimeConstantSeconds;
    private readonly double releaseTimeConstantSeconds;
    private readonly double reservoirRechargeTimeConstantSeconds;

    public TrainBrakeDistributorModel(
        double applicationTimeConstantSeconds = 0.7,
        double releaseTimeConstantSeconds = 1.4,
        double reservoirRechargeTimeConstantSeconds = 8)
    {
        if (!double.IsFinite(applicationTimeConstantSeconds) || applicationTimeConstantSeconds <= 0 ||
            !double.IsFinite(releaseTimeConstantSeconds) || releaseTimeConstantSeconds <= 0 ||
            !double.IsFinite(reservoirRechargeTimeConstantSeconds) || reservoirRechargeTimeConstantSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(applicationTimeConstantSeconds));
        this.applicationTimeConstantSeconds = applicationTimeConstantSeconds;
        this.releaseTimeConstantSeconds = releaseTimeConstantSeconds;
        this.reservoirRechargeTimeConstantSeconds = reservoirRechargeTimeConstantSeconds;
    }

    public TrainBrakeDistributorState CreateInitialState(int carCount)
    {
        if (carCount <= 0) throw new ArgumentOutOfRangeException(nameof(carCount));
        return new TrainBrakeDistributorState(
            Enumerable.Repeat(TrainBrakePipeModel.NormalPressureKPa, carCount),
            Enumerable.Repeat(TrainBrakeDistributorMode.Release, carCount));
    }

    public (TrainBrakeDistributorState Distributor, TrainBrakeCylinderState Cylinders) Advance(
        TrainBrakeDistributorState state,
        TrainBrakePipeState pipe,
        TrainBrakeCylinderState cylinders,
        double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(pipe);
        ArgumentNullException.ThrowIfNull(cylinders);
        var count = state.Modes.Count;
        if (pipe.PressuresKPa.Count != count || cylinders.PressuresKPa.Count != count)
            throw new ArgumentException("Pipe, distributor, and cylinder state must have the same vehicle count.");
        if (pipe.PressuresKPa.Any(p => !double.IsFinite(p) || p < 0 || p > TrainBrakePipeModel.NormalPressureKPa) ||
            cylinders.PressuresKPa.Any(p => !double.IsFinite(p) || p < 0 || p > TrainBrakeCylinderModel.MaximumCylinderPressureKPa))
            throw new ArgumentException("Pneumatic pressures are outside the supported range.");
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (elapsedSeconds == 0) return (state, cylinders);

        var reservoirs = new double[count];
        var modes = new TrainBrakeDistributorMode[count];
        var cylinderPressures = new double[count];
        for (var i = 0; i < count; i++)
        {
            var pipePressure = pipe.PressuresKPa[i];
            var reservoir = state.AuxiliaryReservoirPressuresKPa[i];
            var cylinder = cylinders.PressuresKPa[i];
            var differential = reservoir - pipePressure;
            var mode = differential > MinimumApplicationDifferentialKPa
                ? TrainBrakeDistributorMode.Apply
                : pipePressure > reservoir - MinimumApplicationDifferentialKPa
                    ? TrainBrakeDistributorMode.Release
                    : TrainBrakeDistributorMode.Lap;

            if (mode == TrainBrakeDistributorMode.Apply)
            {
                var target = Math.Min(TrainBrakeCylinderModel.MaximumCylinderPressureKPa,
                    Math.Max(cylinder, Math.Min(TrainBrakeCylinderModel.MaximumCylinderPressureKPa, differential * 0.76)));
                var nextCylinder = Relax(cylinder, target, elapsedSeconds, applicationTimeConstantSeconds);
                var transferred = Math.Max(0, nextCylinder - cylinder);
                reservoirs[i] = Math.Max(0, reservoir - transferred);
                cylinderPressures[i] = nextCylinder;
            }
            else if (mode == TrainBrakeDistributorMode.Release)
            {
                cylinderPressures[i] = Relax(cylinder, 0, elapsedSeconds, releaseTimeConstantSeconds);
                reservoirs[i] = Relax(reservoir, pipePressure, elapsedSeconds, reservoirRechargeTimeConstantSeconds);
            }
            else
            {
                cylinderPressures[i] = cylinder;
                reservoirs[i] = reservoir;
            }

            modes[i] = mode;
        }

        return (
            new TrainBrakeDistributorState(reservoirs, modes),
            new TrainBrakeCylinderState(cylinderPressures));
    }

    private static double Relax(double current, double target, double elapsed, double tau) =>
        Math.Clamp(target + (current - target) * Math.Exp(-elapsed / tau), 0, TrainBrakePipeModel.NormalPressureKPa);
}

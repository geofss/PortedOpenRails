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
    public IReadOnlyList<double> ControlPressuresKPa { get; }
    public IReadOnlyList<double> LastPipePressuresKPa { get; }

    public TrainBrakeDistributorState(
        IEnumerable<double> auxiliaryReservoirPressuresKPa,
        IEnumerable<TrainBrakeDistributorMode> modes)
        : this(MaterializeInitialState(auxiliaryReservoirPressuresKPa, modes))
    {
    }

    public TrainBrakeDistributorState(
        IEnumerable<double> auxiliaryReservoirPressuresKPa,
        IEnumerable<TrainBrakeDistributorMode> modes,
        IEnumerable<double> controlPressuresKPa)
        : this(MaterializeInitialState(auxiliaryReservoirPressuresKPa, modes, controlPressuresKPa))
    {
    }

    private TrainBrakeDistributorState(
        (double[] Reservoirs, TrainBrakeDistributorMode[] Modes, double[] Controls, double[] LastPipes) values)
        : this(values.Reservoirs, values.Modes, values.Controls, values.LastPipes)
    {
    }

    private static (double[] Reservoirs, TrainBrakeDistributorMode[] Modes, double[] Controls, double[] LastPipes)
        MaterializeInitialState(
            IEnumerable<double> auxiliaryReservoirPressuresKPa,
            IEnumerable<TrainBrakeDistributorMode> modes,
            IEnumerable<double>? controlPressuresKPa = null)
    {
        ArgumentNullException.ThrowIfNull(auxiliaryReservoirPressuresKPa);
        ArgumentNullException.ThrowIfNull(modes);
        var reservoirs = auxiliaryReservoirPressuresKPa.ToArray();
        var modeValues = modes.ToArray();
        var controls = controlPressuresKPa?.ToArray() ??
            Enumerable.Repeat(TrainBrakePipeModel.NormalPressureKPa, reservoirs.Length).ToArray();
        var lastPipes = Enumerable.Repeat(TrainBrakePipeModel.NormalPressureKPa, reservoirs.Length).ToArray();
        return (reservoirs, modeValues, controls, lastPipes);
    }

    public TrainBrakeDistributorState(
        IEnumerable<double> auxiliaryReservoirPressuresKPa,
        IEnumerable<TrainBrakeDistributorMode> modes,
        IEnumerable<double> controlPressuresKPa,
        IEnumerable<double> lastPipePressuresKPa)
    {
        ArgumentNullException.ThrowIfNull(auxiliaryReservoirPressuresKPa);
        ArgumentNullException.ThrowIfNull(modes);
        ArgumentNullException.ThrowIfNull(controlPressuresKPa);
        ArgumentNullException.ThrowIfNull(lastPipePressuresKPa);
        AuxiliaryReservoirPressuresKPa = Array.AsReadOnly(auxiliaryReservoirPressuresKPa.ToArray());
        Modes = Array.AsReadOnly(modes.ToArray());
        ControlPressuresKPa = Array.AsReadOnly(controlPressuresKPa.ToArray());
        LastPipePressuresKPa = Array.AsReadOnly(lastPipePressuresKPa.ToArray());
        if (AuxiliaryReservoirPressuresKPa.Count == 0 ||
            AuxiliaryReservoirPressuresKPa.Count != Modes.Count ||
            AuxiliaryReservoirPressuresKPa.Count != ControlPressuresKPa.Count ||
            AuxiliaryReservoirPressuresKPa.Count != LastPipePressuresKPa.Count ||
            AuxiliaryReservoirPressuresKPa.Any(p => !double.IsFinite(p) || p < 0 || p > TrainBrakePipeModel.NormalPressureKPa) ||
            ControlPressuresKPa.Any(p => !double.IsFinite(p) || p < 0 || p > TrainBrakePipeModel.NormalPressureKPa) ||
            LastPipePressuresKPa.Any(p => !double.IsFinite(p) || p < 0 || p > TrainBrakePipeModel.NormalPressureKPa) ||
            Modes.Any(m => !Enum.IsDefined(m)))
            throw new ArgumentException("Distributor state must contain matching, valid per-car reservoir, control pressures, and modes.");
    }
}

internal sealed class TrainBrakeDistributorStateJsonConverter : JsonConverter<TrainBrakeDistributorState>
{
    public override TrainBrakeDistributorState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var reservoirs = root.GetProperty(nameof(TrainBrakeDistributorState.AuxiliaryReservoirPressuresKPa)).EnumerateArray().Select(x => x.GetDouble()).ToArray();
        var modes = root.GetProperty(nameof(TrainBrakeDistributorState.Modes)).EnumerateArray().Select(x => (TrainBrakeDistributorMode)x.GetInt32()).ToArray();
        var controls = root.TryGetProperty(nameof(TrainBrakeDistributorState.ControlPressuresKPa), out var controlElement)
            ? controlElement.EnumerateArray().Select(x => x.GetDouble()).ToArray()
            : Enumerable.Repeat(TrainBrakePipeModel.NormalPressureKPa, reservoirs.Length).ToArray();
        var lastPipes = root.TryGetProperty(nameof(TrainBrakeDistributorState.LastPipePressuresKPa), out var lastPipeElement)
            ? lastPipeElement.EnumerateArray().Select(x => x.GetDouble()).ToArray()
            : Enumerable.Repeat(TrainBrakePipeModel.NormalPressureKPa, reservoirs.Length).ToArray();
        return new TrainBrakeDistributorState(reservoirs, modes, controls, lastPipes);
    }

    public override void Write(Utf8JsonWriter writer, TrainBrakeDistributorState value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName(nameof(TrainBrakeDistributorState.AuxiliaryReservoirPressuresKPa));
        JsonSerializer.Serialize(writer, value.AuxiliaryReservoirPressuresKPa, options);
        writer.WritePropertyName(nameof(TrainBrakeDistributorState.Modes));
        JsonSerializer.Serialize(writer, value.Modes, options);
        writer.WritePropertyName(nameof(TrainBrakeDistributorState.ControlPressuresKPa));
        JsonSerializer.Serialize(writer, value.ControlPressuresKPa, options);
        writer.WritePropertyName(nameof(TrainBrakeDistributorState.LastPipePressuresKPa));
        JsonSerializer.Serialize(writer, value.LastPipePressuresKPa, options);
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
        double applicationTimeConstantSeconds = 0.3,
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
            Enumerable.Repeat(TrainBrakeDistributorMode.Lap, carCount),
            Enumerable.Repeat(TrainBrakePipeModel.NormalPressureKPa, carCount),
            Enumerable.Repeat(TrainBrakePipeModel.NormalPressureKPa, carCount));
    }

    public (TrainBrakeDistributorState Distributor, TrainBrakeCylinderState Cylinders) Advance(
        TrainBrakeDistributorState state,
        TrainBrakePipeState pipe,
        TrainBrakeCylinderState cylinders,
        double elapsedSeconds,
        IReadOnlyList<bool>? enabledByCar = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(pipe);
        ArgumentNullException.ThrowIfNull(cylinders);
        var count = state.Modes.Count;
        if (pipe.PressuresKPa.Count != count || cylinders.PressuresKPa.Count != count ||
            (enabledByCar is not null && enabledByCar.Count != count))
            throw new ArgumentException("Pipe, distributor, cylinder, and brake-profile state must have the same vehicle count.");
        if (pipe.PressuresKPa.Any(p => !double.IsFinite(p) || p < 0 || p > TrainBrakePipeModel.NormalPressureKPa) ||
            cylinders.PressuresKPa.Any(p => !double.IsFinite(p) || p < 0 || p > TrainBrakeCylinderModel.MaximumCylinderPressureKPa))
            throw new ArgumentException("Pneumatic pressures are outside the supported range.");
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (elapsedSeconds == 0) return (state, cylinders);

        var reservoirs = new double[count];
        var controls = new double[count];
        var lastPipes = new double[count];
        var modes = new TrainBrakeDistributorMode[count];
        var cylinderPressures = new double[count];
        for (var i = 0; i < count; i++)
        {
            var pipePressure = pipe.PressuresKPa[i];
            var reservoir = state.AuxiliaryReservoirPressuresKPa[i];
            var control = state.ControlPressuresKPa[i];
            var cylinder = cylinders.PressuresKPa[i];

            // Unsupported brake families must not be silently simulated as an
            // air distributor. Keep their cylinder inactive until a matching
            // brake-system implementation is available.
            if (enabledByCar is not null && !enabledByCar[i])
            {
                reservoirs[i] = reservoir;
                controls[i] = control;
                lastPipes[i] = pipePressure;
                modes[i] = TrainBrakeDistributorMode.Lap;
                cylinderPressures[i] = 0;
                continue;
            }

            // A rise in pipe pressure means the driver is releasing the brake.
            // Detect that direction before comparing with the stored application
            // reference: during recharge the pipe can remain well below that
            // reference for several seconds, but the distributor must already
            // begin exhausting the cylinder.
            var pipeIsRecharging = pipePressure > state.LastPipePressuresKPa[i] + 0.5;
            var pipeIsReducing = pipePressure < state.LastPipePressuresKPa[i] - 0.01;

            // A renewed pipe reduction takes priority over a distributor that
            // was previously releasing. Continue an in-progress application
            // while the pipe is below its reference and is not recharging.
            var applicationDemand = pipePressure < control - MinimumApplicationDifferentialKPa;
            if (applicationDemand &&
                (pipeIsReducing ||
                 (!pipeIsRecharging && state.Modes[i] == TrainBrakeDistributorMode.Apply)))
            {
                var requestedCylinderPressure = Math.Clamp(
                    (control - pipePressure) * 0.76,
                    0,
                    TrainBrakeCylinderModel.MaximumCylinderPressureKPa);
                // The auxiliary reservoir is the air source. The cylinder
                // target cannot exceed the pressure still available there.
                var target = Math.Min(requestedCylinderPressure, reservoir);
                var nextCylinder = Relax(cylinder, target, elapsedSeconds, applicationTimeConstantSeconds);
                var transferredPressure = Math.Max(0, nextCylinder - cylinder);
                reservoirs[i] = Math.Max(0, reservoir - transferredPressure);
                cylinderPressures[i] = nextCylinder;
                modes[i] = Math.Abs(target - nextCylinder) <= 1
                    ? TrainBrakeDistributorMode.Lap
                    : TrainBrakeDistributorMode.Apply;
                // Keep the reference pressure while applying so slow service
                // reductions accumulate against one stable target.
                controls[i] = control;
            }
            else if (pipeIsRecharging ||
                     (state.Modes[i] == TrainBrakeDistributorMode.Release && cylinder > 0.5))
            {
                cylinderPressures[i] = Relax(cylinder, 0, elapsedSeconds, releaseTimeConstantSeconds);
                reservoirs[i] = Relax(reservoir, pipePressure, elapsedSeconds, reservoirRechargeTimeConstantSeconds);
                modes[i] = cylinderPressures[i] <= 0.5
                    ? TrainBrakeDistributorMode.Lap
                    : TrainBrakeDistributorMode.Release;
                controls[i] = pipePressure;
            }
            else
            {
                cylinderPressures[i] = cylinder;
                reservoirs[i] = reservoir;
                modes[i] = TrainBrakeDistributorMode.Lap;
                controls[i] = control;
            }
            lastPipes[i] = pipePressure;
        }

        return (
            new TrainBrakeDistributorState(reservoirs, modes, controls, lastPipes),
            new TrainBrakeCylinderState(cylinderPressures));
    }

    private static double Relax(double current, double target, double elapsed, double tau) =>
        Math.Clamp(target + (current - target) * Math.Exp(-elapsed / tau), 0, TrainBrakePipeModel.NormalPressureKPa);
}

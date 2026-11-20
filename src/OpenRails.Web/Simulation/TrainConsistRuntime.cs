using OpenRails.Web.Parsing;

namespace ORTS.Common;

/// <summary>
/// Runtime adapter that turns parsed locomotive and wagon definitions into
/// the portable consist integrator. This is a first longitudinal simulation
/// path; track traversal, detailed brake plumbing, wheel slip, and coupler slack
/// are intentionally separate future milestones.
/// </summary>
public sealed class TrainConsistRuntime
{
    private readonly EngineMetadata engine;
    private readonly IReadOnlyList<WagonMetadata> wagons;
    private readonly double[] positions;
    private readonly double[] masses;
    private readonly TrainCarForceState[] forces;
    private readonly double maximumSpeedMetersPerSecond;

    public double SpeedMetersPerSecond { get; private set; }
    public double LastNetForceNewtons { get; private set; }
    public int VehicleCount => positions.Length;
    public double[] VehiclePositionsMeters => (double[])positions.Clone();

    private TrainConsistRuntime(
        EngineMetadata engine,
        IReadOnlyList<WagonMetadata> wagons,
        double[] positions,
        double[] masses,
        double maximumSpeedMetersPerSecond,
        double initialSpeedMetersPerSecond)
    {
        this.engine = engine;
        this.wagons = wagons;
        this.positions = positions;
        this.masses = masses;
        this.maximumSpeedMetersPerSecond = maximumSpeedMetersPerSecond;
        SpeedMetersPerSecond = initialSpeedMetersPerSecond;
        forces = new TrainCarForceState[positions.Length];
    }

    public static TrainConsistRuntime Create(
        EngineMetadata engine,
        IReadOnlyList<WagonMetadata> wagons,
        double leadPositionMeters = 0,
        double initialSpeedMetersPerSecond = 0)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(wagons);
        RequirePositive(engine.MassKilograms, "The locomotive definition must provide a positive Mass.");
        RequirePositive(engine.LengthMeters, "The locomotive definition must provide a positive Size length.");
        RequirePositive(engine.MaximumSpeedMetersPerSecond, "The locomotive definition must provide a positive MaxVelocity.");
        RequireFinite(leadPositionMeters, nameof(leadPositionMeters));
        RequireFinite(initialSpeedMetersPerSecond, nameof(initialSpeedMetersPerSecond));

        if (Math.Abs(initialSpeedMetersPerSecond) > engine.MaximumSpeedMetersPerSecond!.Value)
            throw new ArgumentOutOfRangeException(nameof(initialSpeedMetersPerSecond), "Initial speed exceeds the locomotive maximum speed.");

        var count = wagons.Count + 1;
        var positions = new double[count];
        var masses = new double[count];
        positions[0] = leadPositionMeters;
        masses[0] = engine.MassKilograms!.Value;

        var previousHalfLength = engine.LengthMeters!.Value / 2d;
        for (var i = 0; i < wagons.Count; i++)
        {
            var wagon = wagons[i] ?? throw new ArgumentException("Wagon entries cannot be null.", nameof(wagons));
            RequirePositive(wagon.MassKilograms, $"Wagon {i} must provide a positive Mass.");
            RequirePositive(wagon.LengthMeters, $"Wagon {i} must provide a positive Size length.");
            var halfLength = wagon.LengthMeters!.Value / 2d;
            positions[i + 1] = positions[i] - previousHalfLength - halfLength;
            masses[i + 1] = wagon.MassKilograms!.Value;
            previousHalfLength = halfLength;
        }

        return new TrainConsistRuntime(
            engine,
            wagons,
            positions,
            masses,
            engine.MaximumSpeedMetersPerSecond.Value,
            initialSpeedMetersPerSecond);
    }

    /// <summary>
    /// Advances the consist for one time slice. Direction is +1 forward or -1 reverse.
    /// Throttle and brake are normalized control fractions in the range 0..1.
    /// </summary>
    public double Step(
        double throttleFraction,
        double brakeFraction,
        int direction,
        double elapsedSeconds)
    {
        if (direction is not (1 or -1))
            throw new ArgumentOutOfRangeException(nameof(direction), "Direction must be +1 or -1.");
        RequireFinite(elapsedSeconds, nameof(elapsedSeconds));
        if (elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

        var maximumTractiveForce = engine.MaximumTractiveForceNewtons ?? 0;
        var availablePower = (engine.MaximumPowerWatts ?? 0) * engine.TransmissionEfficiency;
        var traction = LocomotiveEffortCalculator.CalculateTractiveForce(
            throttleFraction,
            maximumTractiveForce,
            availablePower,
            SpeedMetersPerSecond) * direction;

        var maximumDynamicBrake = engine.MaximumDynamicBrakeForceNewtons ?? 0;
        var maximumDynamicBrakePower = maximumDynamicBrake * maximumSpeedMetersPerSecond;
        var locomotiveBrake = LocomotiveEffortCalculator.CalculateBrakeEffort(
            brakeFraction,
            maximumDynamicBrake,
            maximumDynamicBrakePower,
            engine.MaximumBrakeForceNewtons ?? 0,
            SpeedMetersPerSecond);

        forces[0] = new TrainCarForceState(
            traction, 0, 0,
            locomotiveBrake.DynamicBrakeForceNewtons + locomotiveBrake.FrictionBrakeForceNewtons,
            0, 0, 0);

        for (var i = 0; i < wagons.Count; i++)
        {
            var coachBrake = brakeFraction * (wagons[i].MaximumBrakeForceNewtons ?? 0);
            forces[i + 1] = new TrainCarForceState(0, 0, 0, coachBrake, 0, 0, 0);
        }

        LastNetForceNewtons = TrainConsistIntegrator.Advance(
            positions,
            masses,
            forces,
            ref SpeedMetersPerSecond,
            elapsedSeconds,
            maximumSpeedMetersPerSecond);

        return LastNetForceNewtons;
    }

    private static void RequirePositive(float? value, string message)
    {
        if (!value.HasValue || !float.IsFinite(value.Value) || value.Value <= 0)
            throw new ArgumentException(message);
    }

    private static void RequireFinite(double value, string name)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(name, "Value must be finite.");
    }
}

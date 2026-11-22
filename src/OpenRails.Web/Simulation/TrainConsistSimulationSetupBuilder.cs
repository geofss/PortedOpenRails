using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

public sealed record TrainConsistSimulationSetup(
    TrainConsistSimulationSession Session,
    TrainConsistMotionState InitialState,
    IReadOnlyList<double> MaximumBrakeForceNewtonsByCar,
    IReadOnlyList<string> Diagnostics);

/// <summary>
/// Builds a browser-ready fixed-step consist session from the parsed original
/// consist and rolling-stock metadata. Cars remain in consist order; EOT devices
/// are excluded because they are not powered/braked vehicle bodies.
/// </summary>
public sealed class TrainConsistSimulationSetupBuilder
{
    public TrainConsistSimulationSetup Build(
        ConsistMetadata consist,
        IReadOnlyList<ConsistCarAssetReference> carAssets,
        double routeLengthMeters,
        double startDistanceMeters = 0,
        double initialSpeedMetersPerSecond = 0)
    {
        ArgumentNullException.ThrowIfNull(consist);
        ArgumentNullException.ThrowIfNull(carAssets);
        if (!double.IsFinite(routeLengthMeters) || routeLengthMeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(routeLengthMeters));
        if (!double.IsFinite(startDistanceMeters) || startDistanceMeters < 0 ||
            !double.IsFinite(initialSpeedMetersPerSecond))
            throw new ArgumentOutOfRangeException(nameof(startDistanceMeters));

        var cars = consist.Cars.Where(car => !car.IsEndOfTrain).ToArray();
        if (cars.Length == 0)
            throw new ArgumentException("The selected consist contains no vehicle bodies.", nameof(consist));

        var states = new List<TrainCarMotionState>(cars.Length);
        var traction = new List<LocomotiveTractionMetadata?>(cars.Length);
        var brakeRatings = new List<double>(cars.Length);
        var diagnostics = new List<string>();
        var cursor = startDistanceMeters;

        foreach (var car in cars)
        {
            var asset = carAssets.FirstOrDefault(candidate =>
                candidate.ConsistName == consist.Name && candidate.Uid == car.Uid);
            var metadata = asset?.Metadata;
            var mass = metadata?.MassKg ?? 0;
            if (!float.IsFinite(mass) || mass <= 0)
            {
                mass = 10_000;
                diagnostics.Add($"{car.Name}: valid mass metadata unavailable; using 10,000 kg fallback.");
            }

            var length = metadata?.LengthM ?? 0;
            if (!float.IsFinite(length) || length <= 0)
            {
                length = 10;
                diagnostics.Add($"{car.Name}: valid vehicle length unavailable; using 10 m fallback.");
            }

            if (car.IsEngine && asset?.TractionMetadata is null)
                diagnostics.Add($"{car.Name}: locomotive traction metadata is unavailable; this vehicle will not generate motive force.");

            var maximumBrakeForce = metadata?.MaxBrakeForceN ?? 0;
            if (!float.IsFinite(maximumBrakeForce) || maximumBrakeForce <= 0)
            {
                maximumBrakeForce = mass * 9.80665f * 0.1f;
                diagnostics.Add($"{car.Name}: rated brake force unavailable; using a 10% weight fallback for the prototype brake command.");
            }

            states.Add(new TrainCarMotionState(mass, initialSpeedMetersPerSecond, cursor + length / 2d));
            traction.Add(car.IsEngine ? asset?.TractionMetadata : null);
            brakeRatings.Add(maximumBrakeForce);
            cursor += length;
        }

        if (cursor > routeLengthMeters)
            diagnostics.Add($"Initial consist length extends to {cursor:N1} m, beyond the {routeLengthMeters:N1} m connected route.");

        var initialState = new TrainConsistMotionState(states);
        var session = new TrainConsistSimulationSession(
            initialState,
            traction,
            routeLengthMeters: routeLengthMeters);

        return new TrainConsistSimulationSetup(
            session,
            initialState,
            Array.AsReadOnly(brakeRatings.ToArray()),
            Array.AsReadOnly(diagnostics.ToArray()));
    }
}

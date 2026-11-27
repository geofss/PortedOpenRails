using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

public sealed record TrainConsistSimulationSetup(
    TrainConsistSimulationSession Session,
    TrainConsistMotionState InitialState,
    IReadOnlyList<double> MaximumBrakeForceNewtonsByCar,
    IReadOnlyList<double> MaximumHandBrakeForceNewtonsByCar,
    IReadOnlyList<TrainCarResistanceParameters> ResistanceParametersByCar,
    IReadOnlyList<bool> PoweredCars,
    IReadOnlyList<TrainCouplerForceLimit> CouplerForceLimits,
    IReadOnlyList<double> CouplerSlackMetersByBoundary,
    IReadOnlyList<double> CouplerStiffnessByBoundary,
    IReadOnlyList<double> CouplerDampingByBoundary,
    IReadOnlyList<RollingStockBrakeProfile> BrakeProfilesByCar,
    IReadOnlyList<string> Diagnostics);

/// <summary>
/// Builds a browser-ready fixed-step consist session from the parsed original
/// consist and rolling-stock metadata. Cars remain in consist order; EOT devices
/// are excluded because they are not powered/braked vehicle bodies.
/// </summary>
public sealed class TrainConsistSimulationSetupBuilder
{
    private static double ValidResistanceCoefficient(
        float? value, string carName, string label, List<string> diagnostics)
    {
        if (value is null)
            return 0;
        if (!float.IsFinite(value.Value) || value.Value < 0)
        {
            diagnostics.Add($"{carName}: invalid {label} coefficient ignored.");
            return 0;
        }
        return value.Value;
    }

    private static double AverageOrFallback(float? first, float? second, double fallback,
        string parameter, string frontCar, string rearCar, List<string> diagnostics)
    {
        var values = new[] { first, second }
            .Where(value => value is > 0 && float.IsFinite(value.Value))
            .Select(value => (double)value!.Value)
            .ToArray();
        if (values.Length == 0)
        {
            diagnostics.Add($"Coupler between {frontCar} and {rearCar}: {parameter} metadata unavailable; using browser fallback {fallback:N0}.");
            return fallback;
        }
        return values.Average();
    }

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
        var handbrakeRatings = new List<double>(cars.Length);
        var resistanceParameters = new List<TrainCarResistanceParameters>(cars.Length);
        var poweredCars = new List<bool>(cars.Length);
        var brakeProfiles = new List<RollingStockBrakeProfile>(cars.Length);
        var diagnostics = new List<string>();
        var cursor = startDistanceMeters;

        foreach (var car in cars)
        {
            var asset = carAssets.FirstOrDefault(candidate =>
                candidate.Uid == car.Uid && string.Equals(candidate.CarName, car.Name, StringComparison.OrdinalIgnoreCase));
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

            var brakeProfile = RollingStockBrakeProfile.FromMetadata(metadata);
            brakeProfiles.Add(brakeProfile);
            if (brakeProfile.Support == RollingStockBrakeSupport.UnsupportedOrUnverified)
                diagnostics.Add($"{car.Name}: declared brake system '{brakeProfile.DeclaredSystemType ?? "unspecified"}' / equipment '{brakeProfile.DeclaredEquipmentType ?? "unspecified"}' is not modelled by the browser pneumatic prototype; its response remains approximate.");
            else if (brakeProfile.Support == RollingStockBrakeSupport.Unspecified)
                diagnostics.Add($"{car.Name}: brake-system metadata is missing; browser pneumatic braking is disabled for this vehicle rather than assuming air-brake equipment.");

            var shoeForce = metadata?.MaxBrakeShoeForceN ?? 0;
            var shoeCount = metadata?.NumberCarBrakeShoes ?? 0;
            var derivedShoeBrakeForce = float.IsFinite(shoeForce) && shoeForce > 0 && shoeCount > 0
                ? shoeForce * shoeCount
                : 0;
            var maximumBrakeForce = metadata?.MaxBrakeForceN ?? derivedShoeBrakeForce;
            if (metadata?.MaxBrakeForceN is null && derivedShoeBrakeForce > 0)
                diagnostics.Add($"{car.Name}: maximum brake force derived from {shoeCount} declared brake shoes.");
            if (!float.IsFinite(maximumBrakeForce) || maximumBrakeForce <= 0)
            {
                maximumBrakeForce = mass * 9.80665f * 0.1f;
                diagnostics.Add($"{car.Name}: rated brake force unavailable; using a 10% weight fallback for the prototype brake command.");
            }

            var davisA = ValidResistanceCoefficient(metadata?.DavisAForceN, car.Name, "Davis A", diagnostics);
            var davisB = ValidResistanceCoefficient(metadata?.DavisBResistance, car.Name, "Davis B", diagnostics);
            var davisC = ValidResistanceCoefficient(metadata?.DavisCResistance, car.Name, "Davis C", diagnostics);
            resistanceParameters.Add(new TrainCarResistanceParameters(
                DavisA: davisA, DavisB: davisB, DavisC: davisC));

            states.Add(new TrainCarMotionState(mass, initialSpeedMetersPerSecond, cursor + length / 2d));
            traction.Add(car.IsEngine ? asset?.TractionMetadata : null);
            poweredCars.Add(car.IsEngine && asset?.TractionMetadata is not null);
            brakeRatings.Add(maximumBrakeForce);
            var maximumHandbrakeForce = metadata?.MaxHandBrakeForceN ?? 0;
            if (!float.IsFinite(maximumHandbrakeForce) || maximumHandbrakeForce < 0)
            {
                diagnostics.Add($"{car.Name}: invalid handbrake force metadata ignored.");
                maximumHandbrakeForce = 0;
            }
            handbrakeRatings.Add(maximumHandbrakeForce);
            cursor += length;
        }

        var couplerLimits = new List<TrainCouplerForceLimit>(Math.Max(0, cars.Length - 1));
        var couplerSlack = new List<double>(Math.Max(0, cars.Length - 1));
        var couplerStiffness = new List<double>(Math.Max(0, cars.Length - 1));
        var couplerDamping = new List<double>(Math.Max(0, cars.Length - 1));
        for (var i = 0; i < cars.Length - 1; i++)
        {
            var frontAsset = carAssets.FirstOrDefault(candidate =>
                candidate.Uid == cars[i].Uid && string.Equals(candidate.CarName, cars[i].Name, StringComparison.OrdinalIgnoreCase));
            var rearAsset = carAssets.FirstOrDefault(candidate =>
                candidate.Uid == cars[i + 1].Uid && string.Equals(candidate.CarName, cars[i + 1].Name, StringComparison.OrdinalIgnoreCase));
            var ratedStrengths = new[]
                {
                    frontAsset?.Metadata?.MinCouplerStrengthN,
                    rearAsset?.Metadata?.MinCouplerStrengthN
                }
                .Where(value => value is > 0 && float.IsFinite(value.Value))
                .Select(value => (double)value!.Value)
                .ToArray();
            var strength = ratedStrengths.Length == 0 ? double.MaxValue : ratedStrengths.Min();
            if (ratedStrengths.Length == 0)
                diagnostics.Add($"Coupler between {cars[i].Name} and {cars[i + 1].Name}: strength metadata unavailable; no finite force limit applied.");
            couplerLimits.Add(new TrainCouplerForceLimit(strength, strength));
            couplerSlack.Add(AverageOrFallback(frontAsset?.Metadata?.CouplerSlackMeters, rearAsset?.Metadata?.CouplerSlackMeters, 0.08, "slack", cars[i].Name, cars[i + 1].Name, diagnostics));
            couplerStiffness.Add(AverageOrFallback(frontAsset?.Metadata?.CouplerStiffnessNPerM, rearAsset?.Metadata?.CouplerStiffnessNPerM, 60_000, "stiffness", cars[i].Name, cars[i + 1].Name, diagnostics));
            couplerDamping.Add(AverageOrFallback(frontAsset?.Metadata?.CouplerDampingNsPerM, rearAsset?.Metadata?.CouplerDampingNsPerM, 20_000, "damping", cars[i].Name, cars[i + 1].Name, diagnostics));
        }

        if (cursor > routeLengthMeters + 1e-6)
            throw new ArgumentOutOfRangeException(nameof(startDistanceMeters),
                $"The consist requires {cursor - startDistanceMeters:N1} m from its start position, but only {routeLengthMeters - startDistanceMeters:N1} m remains on the connected route.");

        var initialState = new TrainConsistMotionState(states);
        var session = new TrainConsistSimulationSession(
            initialState,
            traction,
            routeLengthMeters: routeLengthMeters,
            brakeProfilesByCar: brakeProfiles);

        return new TrainConsistSimulationSetup(
            session,
            initialState,
            Array.AsReadOnly(brakeRatings.ToArray()),
            Array.AsReadOnly(handbrakeRatings.ToArray()),
            Array.AsReadOnly(resistanceParameters.ToArray()),
            Array.AsReadOnly(poweredCars.ToArray()),
            Array.AsReadOnly(couplerLimits.ToArray()),
            Array.AsReadOnly(couplerSlack.ToArray()),
            Array.AsReadOnly(couplerStiffness.ToArray()),
            Array.AsReadOnly(couplerDamping.ToArray()),
            Array.AsReadOnly(brakeProfiles.ToArray()),
            Array.AsReadOnly(diagnostics.ToArray()));
    }
}

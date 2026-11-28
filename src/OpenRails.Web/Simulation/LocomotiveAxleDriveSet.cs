namespace OpenRails.Web.Simulation;

/// <summary>Serializable fixed-step state for a locomotive's driven axles.</summary>
public sealed record LocomotiveAxleDriveSnapshot(
    IReadOnlyList<float> TractiveForceFractions,
    IReadOnlyList<float> ForceByAxle,
    IReadOnlyList<double> PreviousSpeedByAxle,
    IReadOnlyList<double> SpeedByAxle);

/// <summary>
/// Maintains the per-axle drive state used by the native MSTSLocomotive
/// UpdateDriveForce path. Axle speeds are supplied by the vehicle dynamics
/// layer; this class owns force state and locomotive-to-axle force allocation.
/// </summary>
public sealed class LocomotiveAxleDriveSet
{
    private readonly float[] tractiveForceFractions;
    private readonly float[] forceByAxle;
    private readonly double[] previousSpeedByAxle;
    private readonly double[] speedByAxle;

    public int AxleCount => tractiveForceFractions.Length;
    public IReadOnlyList<float> ForceByAxle => Array.AsReadOnly(forceByAxle);
    public double TotalDriveForceNewtons => forceByAxle.Sum(value => (double)value);

    public LocomotiveAxleDriveSet(IReadOnlyList<float> tractiveForceFractions)
    {
        ArgumentNullException.ThrowIfNull(tractiveForceFractions);
        var fractions = tractiveForceFractions.ToArray();
        if (fractions.Length == 0 ||
            fractions.Any(value => !float.IsFinite(value) || value <= 0))
            throw new ArgumentOutOfRangeException(nameof(tractiveForceFractions),
                "A locomotive must have at least one axle with a positive finite force fraction.");
        var total = fractions.Sum();
        if (Math.Abs(total - 1) > 0.001f)
            throw new ArgumentException("Driven axle force fractions must sum to one.", nameof(tractiveForceFractions));

        this.tractiveForceFractions = fractions;
        forceByAxle = new float[fractions.Length];
        previousSpeedByAxle = new double[fractions.Length];
        speedByAxle = new double[fractions.Length];
    }

    /// <summary>
    /// Updates wheel speeds while retaining the prior fixed-step speed for
    /// the native power-ramp calculation.
    /// </summary>
    public void SetAxleSpeeds(IReadOnlyList<double> axleSpeedsMetersPerSecond)
    {
        ArgumentNullException.ThrowIfNull(axleSpeedsMetersPerSecond);
        var speeds = axleSpeedsMetersPerSecond.ToArray();
        if (speeds.Length != AxleCount || speeds.Any(value => !double.IsFinite(value)))
            throw new ArgumentException("One finite wheel speed is required per driven axle.", nameof(axleSpeedsMetersPerSecond));

        Array.Copy(speedByAxle, previousSpeedByAxle, AxleCount);
        Array.Copy(speeds, speedByAxle, AxleCount);
    }

    /// <summary>
    /// Advances each axle using the upstream axle-level force/power ramp
    /// calculation. Optional per-axle targets represent individual slip control.
    /// </summary>
    public double Advance(
        double elapsedSeconds,
        float targetTotalForceNewtons,
        float maximumTotalForceNewtons,
        float forceRampUpNewtonsPerSecond = 0,
        float forceRampDownNewtonsPerSecond = 0,
        float forceRampDownToZeroNewtonsPerSecond = 0,
        float powerRampUpWattsPerSecond = 0,
        float powerRampDownWattsPerSecond = 0,
        float powerRampDownToZeroWattsPerSecond = 0,
        IReadOnlyList<float>? individualAxleTargetForceNewtons = null)
    {
        if (!float.IsFinite(targetTotalForceNewtons) ||
            !float.IsFinite(maximumTotalForceNewtons) || maximumTotalForceNewtons < 0)
            throw new ArgumentOutOfRangeException(nameof(targetTotalForceNewtons));
        if (individualAxleTargetForceNewtons is not null &&
            (individualAxleTargetForceNewtons.Count != AxleCount ||
             individualAxleTargetForceNewtons.Any(value => !float.IsFinite(value))))
            throw new ArgumentException("Individual slip-control targets must contain one finite value per driven axle.",
                nameof(individualAxleTargetForceNewtons));

        var nextForceByAxle = new float[AxleCount];
        for (var i = 0; i < AxleCount; i++)
        {
            var fraction = tractiveForceFractions[i];
            var axleTarget = individualAxleTargetForceNewtons is null
                ? targetTotalForceNewtons * fraction
                : individualAxleTargetForceNewtons[i];
            var axleMaximum = maximumTotalForceNewtons * fraction;
            nextForceByAxle[i] = LocomotiveAxleDriveRamp.Advance(
                forceByAxle[i],
                previousSpeedByAxle[i],
                speedByAxle[i],
                axleTarget,
                axleMaximum,
                fraction,
                elapsedSeconds,
                forceRampUpNewtonsPerSecond,
                forceRampDownNewtonsPerSecond,
                forceRampDownToZeroNewtonsPerSecond,
                powerRampUpWattsPerSecond,
                powerRampDownWattsPerSecond,
                powerRampDownToZeroWattsPerSecond);
        }

        Array.Copy(nextForceByAxle, forceByAxle, AxleCount);
        return TotalDriveForceNewtons;
    }

    /// <summary>
    /// Captures force and wheel-speed history. Read-only wrappers prevent a
    /// caller from mutating the live snapshot arrays through an interface cast.
    /// </summary>
    public LocomotiveAxleDriveSnapshot CaptureState() => new(
        Array.AsReadOnly(tractiveForceFractions.ToArray()),
        Array.AsReadOnly(forceByAxle.ToArray()),
        Array.AsReadOnly(previousSpeedByAxle.ToArray()),
        Array.AsReadOnly(speedByAxle.ToArray()));

    /// <summary>Restores a snapshot only when it belongs to the same axle topology.</summary>
    public void RestoreState(LocomotiveAxleDriveSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.TractiveForceFractions is null ||
            snapshot.ForceByAxle is null ||
            snapshot.PreviousSpeedByAxle is null ||
            snapshot.SpeedByAxle is null)
            throw new ArgumentException("Axle snapshot collections cannot be null.", nameof(snapshot));
        if (snapshot.TractiveForceFractions.Count != AxleCount ||
            snapshot.ForceByAxle.Count != AxleCount ||
            snapshot.PreviousSpeedByAxle.Count != AxleCount ||
            snapshot.SpeedByAxle.Count != AxleCount)
            throw new ArgumentException("Axle snapshot dimensions must match the configured driven-axle topology.", nameof(snapshot));
        for (var i = 0; i < AxleCount; i++)
        {
            if (!float.IsFinite(snapshot.TractiveForceFractions[i]) ||
                Math.Abs(snapshot.TractiveForceFractions[i] - tractiveForceFractions[i]) > 0.000001f ||
                !float.IsFinite(snapshot.ForceByAxle[i]) ||
                !double.IsFinite(snapshot.PreviousSpeedByAxle[i]) ||
                !double.IsFinite(snapshot.SpeedByAxle[i]))
                throw new ArgumentException("Axle snapshot contains invalid state or a different force allocation.", nameof(snapshot));
        }
        for (var i = 0; i < AxleCount; i++)
        {
            forceByAxle[i] = snapshot.ForceByAxle[i];
            previousSpeedByAxle[i] = snapshot.PreviousSpeedByAxle[i];
            speedByAxle[i] = snapshot.SpeedByAxle[i];
        }
    }

    public void Reset()
    {
        Array.Clear(forceByAxle);
        Array.Clear(previousSpeedByAxle);
        Array.Clear(speedByAxle);
    }
}

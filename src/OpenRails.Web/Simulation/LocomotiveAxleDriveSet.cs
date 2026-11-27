namespace OpenRails.Web.Simulation;

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
        if (tractiveForceFractions.Count == 0 ||
            tractiveForceFractions.Any(value => !float.IsFinite(value) || value <= 0))
            throw new ArgumentOutOfRangeException(nameof(tractiveForceFractions),
                "A locomotive must have at least one axle with a positive finite force fraction.");
        var total = tractiveForceFractions.Sum();
        if (Math.Abs(total - 1) > 0.001f)
            throw new ArgumentException("Driven axle force fractions must sum to one.", nameof(tractiveForceFractions));

        this.tractiveForceFractions = tractiveForceFractions.ToArray();
        forceByAxle = new float[tractiveForceFractions.Count];
        previousSpeedByAxle = new double[tractiveForceFractions.Count];
        speedByAxle = new double[tractiveForceFractions.Count];
    }

    /// <summary>
    /// Updates wheel speeds while retaining the prior fixed-step speed for
    /// the native power-ramp calculation.
    /// </summary>
    public void SetAxleSpeeds(IReadOnlyList<double> axleSpeedsMetersPerSecond)
    {
        ArgumentNullException.ThrowIfNull(axleSpeedsMetersPerSecond);
        if (axleSpeedsMetersPerSecond.Count != AxleCount ||
            axleSpeedsMetersPerSecond.Any(value => !double.IsFinite(value)))
            throw new ArgumentException("One finite wheel speed is required per driven axle.", nameof(axleSpeedsMetersPerSecond));

        Array.Copy(speedByAxle, previousSpeedByAxle, AxleCount);
        for (var i = 0; i < AxleCount; i++)
            speedByAxle[i] = axleSpeedsMetersPerSecond[i];
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

        for (var i = 0; i < AxleCount; i++)
        {
            var fraction = tractiveForceFractions[i];
            var axleTarget = individualAxleTargetForceNewtons is null
                ? targetTotalForceNewtons * fraction
                : individualAxleTargetForceNewtons[i];
            var axleMaximum = maximumTotalForceNewtons * fraction;
            forceByAxle[i] = LocomotiveAxleDriveRamp.Advance(
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

        return TotalDriveForceNewtons;
    }

    public void Reset()
    {
        Array.Clear(forceByAxle);
        Array.Clear(previousSpeedByAxle);
        Array.Clear(speedByAxle);
    }
}

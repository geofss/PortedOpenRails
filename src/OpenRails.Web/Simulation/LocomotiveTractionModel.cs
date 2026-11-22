using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

public enum LocomotiveThrottleResponse
{
    Linear,
    Quadratic
}

/// <summary>
/// Browser-independent traction-force evaluator based on the upstream
/// MSTSLocomotive.GetTargetForce rules. Force curves are parsed by the upstream
/// InterpolatorDiesel2D implementation; axle ramping, power-supply limits,
/// adhesion, wheel slip, and locomotive-specific controllers remain separate.
/// </summary>
public sealed class LocomotiveTractionModel
{
    private readonly LocomotiveTractionMetadata metadata;

    public LocomotiveTractionModel(LocomotiveTractionMetadata metadata)
    {
        this.metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    public float CalculateTractiveForce(
        float throttlePercent,
        float speedMetersPerSecond,
        LocomotiveThrottleResponse throttleResponse = LocomotiveThrottleResponse.Linear,
        float powerReduction = 0,
        float averageForceNewtons = 0)
    {
        if (!float.IsFinite(throttlePercent) || !float.IsFinite(speedMetersPerSecond) ||
            !float.IsFinite(powerReduction) || !float.IsFinite(averageForceNewtons))
            throw new ArgumentOutOfRangeException(nameof(throttlePercent));

        var throttle = Math.Clamp(throttlePercent / 100f, 0, 1);
        var speed = Math.Abs(speedMetersPerSecond);
        if (throttle <= 0)
            return 0;

        float force;
        if (metadata.TractionForceCurves is not null)
        {
            force = metadata.TractionForceCurves.Get(throttle, speed);
            if (force < 0 && !metadata.TractionForceCurves.AcceptsNegativeValues())
                force = 0;
        }
        else
        {
            if (speed > metadata.MaxSpeedMetersPerSecond)
                force = 0;
            else if (speed > metadata.MaxSpeedMetersPerSecond - 0.05f)
                force = 20 * (metadata.MaxSpeedMetersPerSecond - speed) * metadata.MaxForceNewtons;
            else
                force = metadata.MaxForceNewtons;
            force *= throttle;
        }

        var powerThrottle = throttleResponse == LocomotiveThrottleResponse.Quadratic
            ? throttle * throttle
            : throttle;
        var availablePower = metadata.MaxPowerWatts * powerThrottle;

        if (metadata.UnloadingSpeedMetersPerSecond > 0 &&
            speed > metadata.UnloadingSpeedMetersPerSecond)
        {
            var decay = 1f - (speed - metadata.UnloadingSpeedMetersPerSecond) /
                metadata.UnloadingSpeedMetersPerSecond;
            availablePower *= Math.Clamp(decay, 0, 1);
        }

        if (metadata.MaxPowerWatts > 0 && speed > 0 && force * speed > availablePower)
            force = availablePower / speed;

        force *= 1 - Math.Clamp(powerReduction, 0, 1);

        if (metadata.MaxForceNewtons > 0 &&
            metadata.MaxContinuousForceNewtons > 0 &&
            metadata.MaxForceNewtons != metadata.MaxContinuousForceNewtons)
        {
            var denominator = 0.25f * metadata.MaxContinuousForceNewtons;
            var heatFactor = denominator > 0
                ? Math.Clamp((averageForceNewtons - 0.75f * metadata.MaxContinuousForceNewtons) / denominator, 0, 1)
                : 0;
            var heatLimitedForce = metadata.MaxForceNewtons *
                (1 - ((metadata.MaxForceNewtons - metadata.MaxContinuousForceNewtons) /
                    metadata.MaxForceNewtons) * heatFactor);
            if (force > heatLimitedForce)
                force = heatLimitedForce;
        }

        return force;
    }
}

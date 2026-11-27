namespace OpenRails.Web.Simulation;

/// <summary>
/// Browser-safe port of the force/power slew calculation in upstream
/// PowerTransmissions/Axle.UpdateForceWithRamp. The caller owns axle state,
/// drive type, slip-control target selection, and applying the resulting force.
/// </summary>
public static class LocomotiveAxleDriveRamp
{
    public static float Advance(
        float currentForceNewtons,
        double previousAxleSpeedMetersPerSecond,
        double axleSpeedMetersPerSecond,
        float targetForceNewtons,
        float maximumForceNewtons,
        float tractiveForceFraction,
        double elapsedSeconds,
        float forceRampUpNewtonsPerSecond = 0,
        float forceRampDownNewtonsPerSecond = 0,
        float forceRampDownToZeroNewtonsPerSecond = 0,
        float powerRampUpWattsPerSecond = 0,
        float powerRampDownWattsPerSecond = 0,
        float powerRampDownToZeroWattsPerSecond = 0)
    {
        var values = new[] { currentForceNewtons, targetForceNewtons, maximumForceNewtons,
            tractiveForceFraction, forceRampUpNewtonsPerSecond, forceRampDownNewtonsPerSecond,
            forceRampDownToZeroNewtonsPerSecond, powerRampUpWattsPerSecond,
            powerRampDownWattsPerSecond, powerRampDownToZeroWattsPerSecond };
        if (values.Any(value => !float.IsFinite(value)) ||
            !double.IsFinite(previousAxleSpeedMetersPerSecond) ||
            !double.IsFinite(axleSpeedMetersPerSecond) ||
            !double.IsFinite(elapsedSeconds) || elapsedSeconds < 0 ||
            maximumForceNewtons < 0 || tractiveForceFraction < 0 ||
            values.Skip(4).Any(value => value < 0))
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds),
                "Axle ramp inputs must be finite; time, force limits, fractions, and rates must be non-negative.");

        targetForceNewtons = Math.Clamp(targetForceNewtons, -maximumForceNewtons, maximumForceNewtons);
        currentForceNewtons = Math.Clamp(currentForceNewtons, -maximumForceNewtons, maximumForceNewtons);
        if (elapsedSeconds == 0)
            return currentForceNewtons;

        var toZero = Math.Sign(currentForceNewtons) != Math.Sign(targetForceNewtons);
        if (axleSpeedMetersPerSecond != 0)
        {
            var power = (double)currentForceNewtons * previousAxleSpeedMetersPerSecond;
            var targetPower = (double)targetForceNewtons * axleSpeedMetersPerSecond;
            if (!double.IsFinite(power) || !double.IsFinite(targetPower))
                throw new ArgumentOutOfRangeException(nameof(axleSpeedMetersPerSecond),
                    "Axle power calculation exceeded the finite simulation range.");
            var powerUp = (targetPower > power) ^ (power < 0);
            var powerRate = powerUp ? powerRampUpWattsPerSecond :
                toZero ? powerRampDownToZeroWattsPerSecond : powerRampDownWattsPerSecond;
            if (powerRate > 0)
            {
                var maximumPowerChange = (double)powerRate * elapsedSeconds * tractiveForceFraction;
                if (!double.IsFinite(maximumPowerChange))
                    throw new ArgumentOutOfRangeException(nameof(elapsedSeconds),
                        "Axle power ramp exceeded the finite simulation range.");
                targetPower = Math.Clamp(targetPower,
                    power - maximumPowerChange, power + maximumPowerChange);
                var adjustedForce = targetPower / axleSpeedMetersPerSecond;
                if (!double.IsFinite(adjustedForce) || adjustedForce > float.MaxValue || adjustedForce < -float.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(axleSpeedMetersPerSecond),
                        "Power-limited axle force exceeded the finite simulation range.");
                targetForceNewtons = (float)adjustedForce;
            }
        }

        var rampUp = (targetForceNewtons > currentForceNewtons) ^ (currentForceNewtons < 0);
        var forceRate = rampUp ? forceRampUpNewtonsPerSecond :
            toZero ? forceRampDownToZeroNewtonsPerSecond : forceRampDownNewtonsPerSecond;
        if (forceRate <= 0)
            return targetForceNewtons;

        var maximumForceChange = (double)forceRate * elapsedSeconds * tractiveForceFraction;
        if (!double.IsFinite(maximumForceChange))
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds),
                "Axle force ramp exceeded the finite simulation range.");
        var nextForce = Math.Clamp((double)targetForceNewtons,
            currentForceNewtons - maximumForceChange,
            currentForceNewtons + maximumForceChange);
        if (!double.IsFinite(nextForce) || nextForce > float.MaxValue || nextForce < -float.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds),
                "Axle force state exceeded the finite simulation range.");
        return (float)nextForce;
    }
}

using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

/// <summary>
/// Applies the asymmetric ORTS locomotive tractive-force slew rates.
/// A zero configured rate means the corresponding transition is immediate.
/// </summary>
public static class LocomotiveTractionRamp
{
    public static float Advance(
        float currentForceNewtons,
        float targetForceNewtons,
        double elapsedSeconds,
        LocomotiveTractionMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (!float.IsFinite(currentForceNewtons) || !float.IsFinite(targetForceNewtons) ||
            !double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (elapsedSeconds == 0 || currentForceNewtons == targetForceNewtons)
            return currentForceNewtons;

        var increasingMagnitude = Math.Abs(targetForceNewtons) > Math.Abs(currentForceNewtons);
        float rate;
        if (targetForceNewtons == 0 || Math.Sign(currentForceNewtons) != Math.Sign(targetForceNewtons))
            rate = metadata.TractionForceRampDownToZeroNewtonsPerSecond;
        else if (increasingMagnitude)
            rate = metadata.TractionForceRampUpNewtonsPerSecond;
        else
            rate = metadata.TractionForceRampDownNewtonsPerSecond;

        if (!float.IsFinite(rate) || rate < 0)
            throw new ArgumentOutOfRangeException(nameof(metadata), "Traction ramp rates must be finite and non-negative.");
        if (rate == 0)
            return targetForceNewtons;

        var maximumChange = rate * (float)elapsedSeconds;
        if (Math.Sign(currentForceNewtons) != Math.Sign(targetForceNewtons) && currentForceNewtons != 0)
        {
            var magnitudeAfterRampDown = Math.Max(0, Math.Abs(currentForceNewtons) - maximumChange);
            return Math.Sign(currentForceNewtons) * magnitudeAfterRampDown;
        }

        var delta = targetForceNewtons - currentForceNewtons;
        return Math.Abs(delta) <= maximumChange
            ? targetForceNewtons
            : currentForceNewtons + Math.Sign(delta) * maximumChange;
    }
}

namespace OpenRails.Web.Simulation;

/// <summary>
/// Converts a normalized brake command and a vehicle's rated brake force into
/// a signed retarding force. This simple model is a portable foundation; brake
/// pipe propagation, friction/air dynamics, fade, and locomotive brake logic
/// are intentionally not represented.
/// </summary>
public sealed class TrainCarBrakeModel
{
    public double CalculateBrakeRetardForceNewtons(
        double speedMetersPerSecond,
        double brakePercent,
        double maximumBrakeForceNewtons)
    {
        if (!double.IsFinite(speedMetersPerSecond) ||
            !double.IsFinite(brakePercent) ||
            !double.IsFinite(maximumBrakeForceNewtons))
            throw new ArgumentOutOfRangeException(nameof(speedMetersPerSecond),
                "Speed, brake command, and brake rating must be finite.");
        if (maximumBrakeForceNewtons < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumBrakeForceNewtons));

        var command = Math.Clamp(brakePercent, 0, 100) / 100d;
        if (command <= 0 || maximumBrakeForceNewtons <= 0 ||
            Math.Abs(speedMetersPerSecond) <= 1e-9)
            return 0;

        return Math.Sign(speedMetersPerSecond) * command * maximumBrakeForceNewtons;
    }
}

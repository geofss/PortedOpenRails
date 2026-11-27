namespace OpenRails.Web.Simulation;

/// <summary>
/// Computes a bounded spring-damper drawbar force after coupler slack is taken up.
/// Positive extension/relative velocity produces a positive force on the rear car.
/// This model is a per-boundary force primitive; consist integration and break
/// handling must supply the boundary kinematics and remain separate.
/// </summary>
public sealed class TrainCouplerSlackModel
{
    public double CalculateForceOnRearCar(
        double relativeDisplacementMeters,
        double relativeVelocityMetersPerSecond,
        double slackMeters,
        double stiffnessNewtonsPerMeter,
        double dampingNewtonSecondsPerMeter,
        double maximumForceNewtons) =>
        CalculateForceOnRearCar(relativeDisplacementMeters, relativeVelocityMetersPerSecond,
            slackMeters, stiffnessNewtonsPerMeter, dampingNewtonSecondsPerMeter,
            maximumForceNewtons, maximumForceNewtons);

    public double CalculateForceOnRearCar(
        double relativeDisplacementMeters,
        double relativeVelocityMetersPerSecond,
        double slackMeters,
        double stiffnessNewtonsPerMeter,
        double dampingNewtonSecondsPerMeter,
        double maximumCompressionNewtons,
        double maximumTensionNewtons)
    {
        if (!double.IsFinite(relativeDisplacementMeters))
            throw new ArgumentOutOfRangeException(nameof(relativeDisplacementMeters));
        if (!double.IsFinite(relativeVelocityMetersPerSecond))
            throw new ArgumentOutOfRangeException(nameof(relativeVelocityMetersPerSecond));
        if (!double.IsFinite(slackMeters) || slackMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(slackMeters));
        if (!double.IsFinite(stiffnessNewtonsPerMeter) || stiffnessNewtonsPerMeter < 0)
            throw new ArgumentOutOfRangeException(nameof(stiffnessNewtonsPerMeter));
        if (!double.IsFinite(dampingNewtonSecondsPerMeter) || dampingNewtonSecondsPerMeter < 0)
            throw new ArgumentOutOfRangeException(nameof(dampingNewtonSecondsPerMeter));
        if (!double.IsFinite(maximumCompressionNewtons) || maximumCompressionNewtons < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumCompressionNewtons));
        if (!double.IsFinite(maximumTensionNewtons) || maximumTensionNewtons < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumTensionNewtons));

        var extensionBeyondSlack = Math.Sign(relativeDisplacementMeters) *
            Math.Max(0, Math.Abs(relativeDisplacementMeters) - slackMeters);
        if (extensionBeyondSlack == 0)
            return 0;

        var force = stiffnessNewtonsPerMeter * extensionBeyondSlack +
            dampingNewtonSecondsPerMeter * relativeVelocityMetersPerSecond;
        if (double.IsNaN(force))
            throw new ArgumentOutOfRangeException(nameof(relativeDisplacementMeters),
                "Coupler spring and damping forces produced an indeterminate result.");
        return Math.Clamp(force, -maximumCompressionNewtons, maximumTensionNewtons);
    }
}

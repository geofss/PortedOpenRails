namespace OpenRails.Web.Simulation;

/// <summary>Quasi-static wheel-rail adhesion ceiling; not a wheel-slip controller.</summary>
public sealed class TrainCarAdhesionModel
{
    public const double DefaultAdhesionCoefficient = 0.25;
    public const double StandardGravityMetersPerSecondSquared = 9.80665;

    public double LimitTractiveForce(double requestedForceNewtons, double massKg,
        double adhesionCoefficient = DefaultAdhesionCoefficient)
    {
        if (!double.IsFinite(requestedForceNewtons) || !double.IsFinite(massKg) ||
            !double.IsFinite(adhesionCoefficient))
            throw new ArgumentOutOfRangeException(nameof(requestedForceNewtons),
                "Tractive force, vehicle mass, and adhesion coefficient must be finite.");
        if (massKg <= 0) throw new ArgumentOutOfRangeException(nameof(massKg));
        if (adhesionCoefficient < 0 || adhesionCoefficient > 1)
            throw new ArgumentOutOfRangeException(nameof(adhesionCoefficient));
        var maximumForce = adhesionCoefficient * massKg * StandardGravityMetersPerSecondSquared;
        if (!double.IsFinite(maximumForce))
            throw new ArgumentOutOfRangeException(nameof(massKg),
                "Calculated wheel-rail adhesion limit exceeded the finite simulation range.");
        return Math.Clamp(requestedForceNewtons, -maximumForce, maximumForce);
    }
}

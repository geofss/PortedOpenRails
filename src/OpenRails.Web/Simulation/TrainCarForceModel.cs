namespace OpenRails.Web.Simulation;

/// <summary>
/// Signed driving forces and non-negative longitudinal resistance terms for one
/// rolling-stock vehicle. Force generation remains owned by the corresponding
/// locomotive, brake, adhesion, and environment subsystems.
/// </summary>
public sealed record TrainCarForceBreakdown(
    double MotiveForceNewtons,
    double GravityForceNewtons,
    double RollingFrictionForceNewtons,
    double BrakeRetardForceNewtons,
    double CurveResistanceForceNewtons,
    double WindResistanceForceNewtons,
    double TunnelResistanceForceNewtons)
{
    public double TotalResistanceNewtons =>
        RollingFrictionForceNewtons + BrakeRetardForceNewtons +
        CurveResistanceForceNewtons + WindResistanceForceNewtons +
        TunnelResistanceForceNewtons;
}

/// <summary>
/// Combines already-computed Open Rails-style driving and resistance forces into
/// one signed longitudinal force. It does not calculate the underlying force curves.
/// </summary>
public sealed class TrainCarForceModel
{
    public double CalculateNetLongitudinalForce(
        TrainCarMotionState state,
        TrainCarForceBreakdown forces)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(forces);
        if (!double.IsFinite(state.SpeedMetersPerSecond))
            throw new ArgumentOutOfRangeException(nameof(state));
        var values = new[]
        {
            forces.MotiveForceNewtons,
            forces.GravityForceNewtons,
            forces.RollingFrictionForceNewtons,
            forces.BrakeRetardForceNewtons,
            forces.CurveResistanceForceNewtons,
            forces.WindResistanceForceNewtons,
            forces.TunnelResistanceForceNewtons
        };
        if (values.Any(value => !double.IsFinite(value)))
            throw new ArgumentOutOfRangeException(nameof(forces), "All force values must be finite.");
        if (values.Skip(2).Any(value => value < 0))
            throw new ArgumentOutOfRangeException(nameof(forces), "Resistance forces must be non-negative.");

        var drivingForce = forces.MotiveForceNewtons + forces.GravityForceNewtons;
        var resistance = forces.TotalResistanceNewtons;
        var direction = Math.Abs(state.SpeedMetersPerSecond) > 1e-9
            ? Math.Sign(state.SpeedMetersPerSecond)
            : Math.Sign(drivingForce);

        if (direction == 0 || Math.Abs(drivingForce) <= resistance)
            return 0;

        return drivingForce - direction * resistance;
    }
}

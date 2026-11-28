namespace OpenRails.Web.Simulation;

/// <summary>
/// Configurable longitudinal resistance inputs for a vehicle. Positive gradient
/// means downhill in the train's forward direction; wind speed is signed in that
/// same direction. Davis coefficients use A + B|v| + C v² in newtons.
/// </summary>
public sealed record TrainCarResistanceParameters(
    double RollingResistanceCoefficient = 0.0015,
    double DavisA = 0,
    double DavisB = 0,
    double DavisC = 0,
    double AerodynamicDragAreaSquareMeters = 6,
    double WindSpeedMetersPerSecond = 0,
    double GradientRadians = 0,
    double CurveResistanceNewtons = 0,
    double TunnelResistanceNewtons = 0);

/// <summary>
/// Computes basic Davis, rolling, aerodynamic, grade, curve, and tunnel forces.
/// Route-specific grade/curve and vehicle-specific coefficients can be supplied
/// as the parsed route and rolling-stock pipelines become more complete.
/// </summary>
public sealed class TrainCarResistanceModel
{
    private const double GravityMetersPerSecondSquared = 9.80665;
    private const double AirDensityKgPerCubicMeter = 1.225;

    public TrainCarResistanceForces Calculate(
        TrainCarMotionState state,
        TrainCarResistanceParameters? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        parameters ??= new TrainCarResistanceParameters();
        if (!double.IsFinite(state.MassKg) || state.MassKg <= 0 ||
            !double.IsFinite(state.SpeedMetersPerSecond))
            throw new ArgumentOutOfRangeException(nameof(state));

        var values = new[]
        {
            parameters.RollingResistanceCoefficient,
            parameters.DavisA,
            parameters.DavisB,
            parameters.DavisC,
            parameters.AerodynamicDragAreaSquareMeters,
            parameters.WindSpeedMetersPerSecond,
            parameters.GradientRadians,
            parameters.CurveResistanceNewtons,
            parameters.TunnelResistanceNewtons
        };
        if (values.Any(value => !double.IsFinite(value)) ||
            parameters.RollingResistanceCoefficient < 0 ||
            parameters.DavisA < 0 || parameters.DavisB < 0 || parameters.DavisC < 0 ||
            parameters.AerodynamicDragAreaSquareMeters < 0 ||
            Math.Abs(parameters.GradientRadians) >= Math.PI / 2 ||
            parameters.CurveResistanceNewtons < 0 || parameters.TunnelResistanceNewtons < 0)
            throw new ArgumentOutOfRangeException(nameof(parameters),
                "Resistance coefficients must be finite and non-negative; gradient must remain between -90 and 90 degrees.");

        var speed = Math.Abs(state.SpeedMetersPerSecond);
        var rolling = state.MassKg * GravityMetersPerSecondSquared *
            parameters.RollingResistanceCoefficient +
            parameters.DavisA + parameters.DavisB * speed + parameters.DavisC * speed * speed;
        var relativeWind = state.SpeedMetersPerSecond - parameters.WindSpeedMetersPerSecond;
        var wind = 0.5 * AirDensityKgPerCubicMeter *
            parameters.AerodynamicDragAreaSquareMeters * relativeWind * relativeWind;
        var gravity = state.MassKg * GravityMetersPerSecondSquared *
            Math.Sin(parameters.GradientRadians);

        if (!double.IsFinite(rolling) || !double.IsFinite(wind) || !double.IsFinite(gravity))
            throw new ArgumentOutOfRangeException(nameof(state),
                "Calculated rolling, aerodynamic, or grade force exceeded the finite simulation range.");

        return new TrainCarResistanceForces(
            gravity,
            rolling,
            0,
            parameters.CurveResistanceNewtons,
            wind,
            parameters.TunnelResistanceNewtons);
    }
}

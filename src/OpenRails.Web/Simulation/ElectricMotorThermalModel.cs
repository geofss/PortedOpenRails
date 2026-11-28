namespace OpenRails.Web.Simulation;

/// <summary>
/// Browser-side thermal state for the base Open Rails ElectricMotor transmission
/// model. It ports the upstream transmission-loss equation and thermal ODE;
/// motor-specific torque curves and electrical control remain separate systems.
/// </summary>
public sealed class ElectricMotorThermalModel
{
    public double TemperatureKelvin { get; private set; }

    public ElectricMotorThermalModel(double initialTemperatureKelvin = 273)
    {
        if (!double.IsFinite(initialTemperatureKelvin) || initialTemperatureKelvin <= 0)
            throw new ArgumentOutOfRangeException(nameof(initialTemperatureKelvin));
        TemperatureKelvin = initialTemperatureKelvin;
    }

    public double Advance(
        double elapsedSeconds,
        double axleDriveForceNewtons,
        double axleSpeedMetersPerSecond,
        double wheelRadiusMeters,
        double transmissionRatio,
        double transmissionEfficiency,
        double coolingPowerWatts,
        double thermalCoefficientJoulesPerSquareMeterSecondKelvin,
        double surfaceAreaSquareMeters,
        double specificHeatCapacityJoulesPerKilogramKelvin,
        double motorMassKilograms)
    {
        var values = new[] { elapsedSeconds, axleDriveForceNewtons, axleSpeedMetersPerSecond,
            wheelRadiusMeters, transmissionRatio, transmissionEfficiency, coolingPowerWatts,
            thermalCoefficientJoulesPerSquareMeterSecondKelvin, surfaceAreaSquareMeters,
            specificHeatCapacityJoulesPerKilogramKelvin, motorMassKilograms };
        if (values.Any(value => !double.IsFinite(value)) ||
            elapsedSeconds < 0 || wheelRadiusMeters <= 0 || transmissionRatio <= 0 ||
            transmissionEfficiency <= 0 || transmissionEfficiency > 1 ||
            coolingPowerWatts < 0 || thermalCoefficientJoulesPerSquareMeterSecondKelvin <= 0 ||
            surfaceAreaSquareMeters <= 0 || specificHeatCapacityJoulesPerKilogramKelvin <= 0 ||
            motorMassKilograms <= 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds),
                "Motor thermal inputs must be finite and physical parameters must be positive.");

        if (elapsedSeconds == 0)
            return TemperatureKelvin;

        // Algebraically equivalent to ElectricMotor.Update's transmission-loss expression.
        var mechanicalPowerWatts = axleDriveForceNewtons * axleSpeedMetersPerSecond;
        var transmissionLossesWatts = mechanicalPowerWatts *
            (1 - transmissionEfficiency) /
            (transmissionEfficiency * transmissionRatio * transmissionRatio);

        var heatTransferArea = thermalCoefficientJoulesPerSquareMeterSecondKelvin *
            surfaceAreaSquareMeters;
        var steadyTemperature = (transmissionLossesWatts - coolingPowerWatts) / heatTransferArea;
        var thermalTimeConstant = specificHeatCapacityJoulesPerKilogramKelvin * motorMassKilograms;

        if (!double.IsFinite(mechanicalPowerWatts) ||
            !double.IsFinite(transmissionLossesWatts) ||
            !double.IsFinite(heatTransferArea) || heatTransferArea <= 0 ||
            !double.IsFinite(steadyTemperature) ||
            !double.IsFinite(thermalTimeConstant) || thermalTimeConstant <= 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds),
                "Calculated motor power or thermal constants exceeded the finite simulation range.");

        // Exact integration of the first-order ODE used by the upstream base motor.
        // This avoids frame-rate-dependent Euler error while retaining its physical equation.
        var decay = Math.Exp(-elapsedSeconds / thermalTimeConstant);
        var nextTemperature = steadyTemperature +
            (TemperatureKelvin - steadyTemperature) * decay;
        if (!double.IsFinite(nextTemperature) || nextTemperature <= 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds),
                "Calculated motor temperature must remain finite and above absolute zero.");

        TemperatureKelvin = nextTemperature;
        return TemperatureKelvin;
    }
}

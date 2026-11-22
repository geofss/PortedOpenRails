using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

/// <summary>
/// Driver-command facade for a parsed consist. It maps throttle only to cars
/// with locomotive traction metadata, applies brake commands to each vehicle,
/// and derives common train acceleration from the available external forces.
/// </summary>
public sealed class BrowserTrainDrivingController
{
    private readonly TrainConsistSimulationSetup setup;
    private readonly double rollingResistanceCoefficient;
    private readonly double aerodynamicDragAreaSquareMeters;

    public TrainConsistMotionState State => setup.Session.State;
    public double SimulationTimeSeconds => setup.Session.SimulationTimeSeconds;
    public bool IsPaused => setup.Session.IsPaused;
    public float ThrottlePercent { get; private set; }
    public double BrakePercent { get; private set; }

    public BrowserTrainDrivingController(
        TrainConsistSimulationSetup setup,
        double rollingResistanceCoefficient = 0.0015,
        double aerodynamicDragAreaSquareMeters = 6)
    {
        this.setup = setup ?? throw new ArgumentNullException(nameof(setup));
        if (!double.IsFinite(rollingResistanceCoefficient) || rollingResistanceCoefficient < 0)
            throw new ArgumentOutOfRangeException(nameof(rollingResistanceCoefficient));
        if (!double.IsFinite(aerodynamicDragAreaSquareMeters) || aerodynamicDragAreaSquareMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(aerodynamicDragAreaSquareMeters));
        if (setup.PoweredCars.Count != setup.InitialState.Cars.Count ||
            setup.MaximumBrakeForceNewtonsByCar.Count != setup.InitialState.Cars.Count)
            throw new ArgumentException("Simulation setup control mappings must match the vehicle count.", nameof(setup));

        this.rollingResistanceCoefficient = rollingResistanceCoefficient;
        this.aerodynamicDragAreaSquareMeters = aerodynamicDragAreaSquareMeters;
    }

    public void SetThrottle(float percent)
    {
        if (!float.IsFinite(percent))
            throw new ArgumentOutOfRangeException(nameof(percent));
        ThrottlePercent = Math.Clamp(percent, 0, 100);
    }

    public void SetBrake(double percent)
    {
        if (!double.IsFinite(percent))
            throw new ArgumentOutOfRangeException(nameof(percent));
        BrakePercent = Math.Clamp(percent, 0, 100);
    }

    public int Advance(double elapsedFrameSeconds)
    {
        var throttle = setup.PoweredCars
            .Select(powered => powered ? ThrottlePercent : 0f)
            .ToArray();
        var brakes = Enumerable.Repeat(BrakePercent, State.Cars.Count).ToArray();

        return setup.Session.AdvanceWithAutomaticCouplers(
            elapsedFrameSeconds,
            throttle,
            brakes,
            setup.MaximumBrakeForceNewtonsByCar,
            (_, car) =>
            {
                var speed = Math.Abs(car.SpeedMetersPerSecond);
                var rolling = car.MassKg * 9.80665 * rollingResistanceCoefficient;
                var wind = 0.5 * 1.225 * aerodynamicDragAreaSquareMeters * speed * speed;
                return new TrainCarResistanceForces(0, rolling, 0, 0, wind, 0);
            });
    }

    public void Pause() => setup.Session.Pause();
    public void Resume() => setup.Session.Resume();
    public void Reset() => setup.Session.Reset(setup.InitialState);
}

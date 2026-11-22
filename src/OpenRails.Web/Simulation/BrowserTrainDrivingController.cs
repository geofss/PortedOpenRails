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
    private readonly TrainCarResistanceModel resistanceModel = new();
    private readonly IReadOnlyList<TrainCarResistanceParameters> resistanceParametersByCar;
    private readonly TrackNetworkRouteProfile? routeProfile;
    private readonly TrainBrakePipeModel brakePipeModel = new();
    private TrainBrakePipeState brakePipeState;

    public TrainConsistMotionState State => setup.Session.State;
    public double SimulationTimeSeconds => setup.Session.SimulationTimeSeconds;
    public bool IsPaused => setup.Session.IsPaused;
    public float ThrottlePercent { get; private set; }
    public double BrakePercent { get; private set; }
    public TrainBrakePipeState BrakePipeState => brakePipeState;

    public BrowserTrainDrivingController(
        TrainConsistSimulationSetup setup,
        double rollingResistanceCoefficient = 0.0015,
        double aerodynamicDragAreaSquareMeters = 6,
        TrackNetworkRoute? route = null)
    {
        this.setup = setup ?? throw new ArgumentNullException(nameof(setup));
        if (!double.IsFinite(rollingResistanceCoefficient) || rollingResistanceCoefficient < 0)
            throw new ArgumentOutOfRangeException(nameof(rollingResistanceCoefficient));
        if (!double.IsFinite(aerodynamicDragAreaSquareMeters) || aerodynamicDragAreaSquareMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(aerodynamicDragAreaSquareMeters));
        if (setup.PoweredCars.Count != setup.InitialState.Cars.Count ||
            setup.MaximumBrakeForceNewtonsByCar.Count != setup.InitialState.Cars.Count ||
            setup.ResistanceParametersByCar.Count != setup.InitialState.Cars.Count)
            throw new ArgumentException("Simulation setup control mappings must match the vehicle count.", nameof(setup));

        resistanceParametersByCar = Array.AsReadOnly(setup.ResistanceParametersByCar.Select(parameters =>
            parameters with
            {
                RollingResistanceCoefficient = rollingResistanceCoefficient,
                AerodynamicDragAreaSquareMeters = aerodynamicDragAreaSquareMeters
            }).ToArray());
        routeProfile = route is null ? null : TrackNetworkRouteProfile.Build(route);
        brakePipeState = brakePipeModel.CreateInitialState(setup.InitialState.Cars.Count);
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
        brakePipeState = brakePipeModel.Advance(brakePipeState, BrakePercent, Math.Clamp(elapsedFrameSeconds, 0, 0.25));
        var brakes = brakePipeModel.GetBrakePercentByCar(brakePipeState);

        return setup.Session.AdvanceWithAutomaticCouplers(
            elapsedFrameSeconds,
            throttle,
            brakes,
            setup.MaximumBrakeForceNewtonsByCar,
            (index, car) => resistanceModel.Calculate(car, routeProfile is null
                ? resistanceParametersByCar[index]
                : resistanceParametersByCar[index] with { GradientRadians = routeProfile.Sample(car.DistanceMeters).GradeRadians }),
            setup.CouplerForceLimits);
    }

    public void Pause() => setup.Session.Pause();
    public void Resume() => setup.Session.Resume();
    public void Reset()
    {
        setup.Session.Reset(setup.InitialState);
        brakePipeState = brakePipeModel.CreateInitialState(setup.InitialState.Cars.Count);
        ThrottlePercent = 0;
        BrakePercent = 0;
    }
}

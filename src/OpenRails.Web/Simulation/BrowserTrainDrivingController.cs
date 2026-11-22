using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

/// <summary>
/// Driver-command facade for a parsed consist. It maps throttle only to cars
/// with locomotive traction metadata, applies brake commands to each vehicle,
/// and calculates transient coupler forces from adjacent-car displacement and velocity.
/// </summary>
public sealed class BrowserTrainDrivingController
{
    private readonly TrainConsistSimulationSetup setup;
    private readonly TrainCarResistanceModel resistanceModel = new();
    private readonly IReadOnlyList<TrainCarResistanceParameters> resistanceParametersByCar;
    private readonly TrackNetworkRouteProfile? routeProfile;
    private readonly TrainConsistCouplerDynamics couplerDynamics = new();
    private readonly IReadOnlyList<double> couplerRestGapsMeters;
    private readonly IReadOnlyList<double> couplerSlackMeters;
    private readonly IReadOnlyList<double> couplerStiffness;
    private readonly IReadOnlyList<double> couplerDamping;
    private readonly IReadOnlyList<double> couplerMaximumForces;
    private readonly TrainBrakePipeModel brakePipeModel = new();
    private readonly TrainBrakeCylinderModel brakeCylinderModel = new();
    private TrainBrakePipeState brakePipeState;
    private TrainBrakeCylinderState brakeCylinderState;

    public TrainConsistMotionState State => setup.Session.State;
    public double SimulationTimeSeconds => setup.Session.SimulationTimeSeconds;
    public bool IsPaused => setup.Session.IsPaused;
    public float ThrottlePercent { get; private set; }
    public double BrakePercent { get; private set; }
    public TrainBrakePipeState BrakePipeState => brakePipeState;
    public TrainBrakeCylinderState BrakeCylinderState => brakeCylinderState;
    public IReadOnlyList<double> CouplerForceTelemetryNewtons { get; private set; } = Array.Empty<double>();
    public IReadOnlyList<double> RouteGradePercentByCar => routeProfile is null
        ? Array.Empty<double>()
        : Array.AsReadOnly(State.Cars.Select(car =>
            Math.Tan(routeProfile.Sample(car.DistanceMeters).GradeRadians) * 100).ToArray());
    public double? LeadCarRouteGradePercent => RouteGradePercentByCar.Count == 0
        ? null
        : RouteGradePercentByCar[0];

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
                RollingResistanceCoefficient = parameters.DavisA > 0 || parameters.DavisB > 0
                    ? 0
                    : rollingResistanceCoefficient,
                AerodynamicDragAreaSquareMeters = parameters.DavisC > 0
                    ? 0
                    : aerodynamicDragAreaSquareMeters
            }).ToArray());
        routeProfile = route is null ? null : TrackNetworkRouteProfile.Build(route);
        var initialCars = setup.InitialState.Cars;
        couplerRestGapsMeters = Array.AsReadOnly(Enumerable.Range(0, Math.Max(0, initialCars.Count - 1))
            .Select(i => initialCars[i + 1].DistanceMeters - initialCars[i].DistanceMeters).ToArray());
        // Prototype values pending parsing native ORTS coupler slack and spring/damper parameters.
        couplerSlackMeters = Array.AsReadOnly(Enumerable.Repeat(0.08d, couplerRestGapsMeters.Count).ToArray());
        couplerStiffness = Array.AsReadOnly(Enumerable.Repeat(60_000d, couplerRestGapsMeters.Count).ToArray());
        couplerDamping = Array.AsReadOnly(Enumerable.Repeat(20_000d, couplerRestGapsMeters.Count).ToArray());
        couplerMaximumForces = Array.AsReadOnly(setup.CouplerForceLimits.Select(limit =>
            Math.Min(limit.MaximumTensionNewtons, limit.MaximumCompressionNewtons)).ToArray());
        brakePipeState = brakePipeModel.CreateInitialState(setup.InitialState.Cars.Count);
        brakeCylinderState = brakeCylinderModel.CreateInitialState(setup.InitialState.Cars.Count);
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
        var pipeBrakeCommands = brakePipeModel.GetBrakePercentByCar(brakePipeState);
        brakeCylinderState = brakeCylinderModel.Advance(
            brakeCylinderState, pipeBrakeCommands, Math.Clamp(elapsedFrameSeconds, 0, 0.25));
        var brakes = brakeCylinderModel.GetBrakePercentByCar(brakeCylinderState);

        var dynamicCouplerForces = couplerDynamics.CalculatePerCarForces(
            setup.Session.State, couplerRestGapsMeters, couplerSlackMeters,
            couplerStiffness, couplerDamping, couplerMaximumForces);
        CouplerForceTelemetryNewtons = dynamicCouplerForces;
        return setup.Session.AdvanceWithBrakeCommands(
            elapsedFrameSeconds,
            throttle,
            brakes,
            setup.MaximumBrakeForceNewtonsByCar,
            (index, car) => resistanceModel.Calculate(car, routeProfile is null
                ? resistanceParametersByCar[index]
                : resistanceParametersByCar[index] with { GradientRadians = routeProfile.Sample(car.DistanceMeters).GradeRadians }),
            dynamicCouplerForces);
    }

    public void Pause() => setup.Session.Pause();
    public void Resume() => setup.Session.Resume();
    public void Reset()
    {
        setup.Session.Reset(setup.InitialState);
        brakePipeState = brakePipeModel.CreateInitialState(setup.InitialState.Cars.Count);
        brakeCylinderState = brakeCylinderModel.CreateInitialState(setup.InitialState.Cars.Count);
        ThrottlePercent = 0;
        BrakePercent = 0;
        CouplerForceTelemetryNewtons = Array.AsReadOnly(new double[Math.Max(0, setup.InitialState.Cars.Count - 1)]);
    }
}

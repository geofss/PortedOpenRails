using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

/// <summary>
/// Driver-command facade for a parsed consist. It maps throttle only to cars
/// with locomotive traction metadata, applies brake commands to each vehicle,
/// and calculates transient coupler forces from adjacent-car displacement and velocity.
/// </summary>
public enum TrainReverserPosition
{
    Reverse = -1,
    Neutral = 0,
    Forward = 1
}

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
    private readonly IReadOnlyList<double> couplerMaximumCompressionForces;
    private readonly IReadOnlyList<double> couplerMaximumTensionForces;
    private readonly float[] throttleCommandBuffer;
    private readonly double[] zeroBrakeCommandBuffer;
    private readonly double[] zeroCouplerForceBuffer;
    private readonly TrainBrakePipeModel brakePipeModel = new();
    private readonly TrainBrakeCylinderModel brakeCylinderModel = new();
    private TrainBrakePipeState brakePipeState;
    private TrainBrakeCylinderState brakeCylinderState;

    public TrainConsistMotionState State => setup.Session.State;
    public double SimulationTimeSeconds => setup.Session.SimulationTimeSeconds;
    public double DroppedTimeSeconds => setup.Session.DroppedTimeSeconds;
    public bool IsPaused => setup.Session.IsPaused;
    public float ThrottlePercent { get; private set; }
    public double BrakePercent { get; private set; }
    public TrainReverserPosition ReverserPosition { get; private set; } = TrainReverserPosition.Forward;
    public bool CanChangeReverser => ThrottlePercent <= 0.01f &&
        State.Cars.All(car => Math.Abs(car.SpeedMetersPerSecond) <= 0.1);
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
        throttleCommandBuffer = new float[initialCars.Count];
        zeroBrakeCommandBuffer = new double[initialCars.Count];
        zeroCouplerForceBuffer = new double[initialCars.Count];
        couplerRestGapsMeters = Array.AsReadOnly(Enumerable.Range(0, Math.Max(0, initialCars.Count - 1))
            .Select(i => initialCars[i + 1].DistanceMeters - initialCars[i].DistanceMeters).ToArray());
        // Prototype values pending parsing native ORTS coupler slack and spring/damper parameters.
        couplerSlackMeters = Array.AsReadOnly(Enumerable.Repeat(0.08d, couplerRestGapsMeters.Count).ToArray());
        couplerStiffness = Array.AsReadOnly(Enumerable.Repeat(60_000d, couplerRestGapsMeters.Count).ToArray());
        couplerDamping = Array.AsReadOnly(Enumerable.Repeat(20_000d, couplerRestGapsMeters.Count).ToArray());
        couplerMaximumCompressionForces = Array.AsReadOnly(setup.CouplerForceLimits
            .Select(limit => limit.MaximumCompressionNewtons).ToArray());
        couplerMaximumTensionForces = Array.AsReadOnly(setup.CouplerForceLimits
            .Select(limit => limit.MaximumTensionNewtons).ToArray());
        brakePipeState = brakePipeModel.CreateInitialState(setup.InitialState.Cars.Count);
        brakeCylinderState = brakeCylinderModel.CreateInitialState(setup.InitialState.Cars.Count);
        CouplerForceTelemetryNewtons = Array.AsReadOnly(
            new double[Math.Max(0, setup.InitialState.Cars.Count - 1)]);
    }

    public void SetThrottle(float percent)
    {
        if (!float.IsFinite(percent))
            throw new ArgumentOutOfRangeException(nameof(percent));
        ThrottlePercent = Math.Clamp(percent, 0, 100);
    }

    public void SetReverser(TrainReverserPosition position)
    {
        if (!Enum.IsDefined(position))
            throw new ArgumentOutOfRangeException(nameof(position));
        if (!CanChangeReverser && position != ReverserPosition)
            throw new InvalidOperationException("The reverser can only be changed when the consist is stopped.");
        ReverserPosition = position;
    }

    public void SetBrake(double percent)
    {
        if (!double.IsFinite(percent))
            throw new ArgumentOutOfRangeException(nameof(percent));
        BrakePercent = Math.Clamp(percent, 0, 100);
    }

    /// <summary>Immediately commands zero traction and full service braking.</summary>
    public void ApplyEmergencyStop()
    {
        ThrottlePercent = 0;
        BrakePercent = 100;
    }

    public int Advance(double elapsedFrameSeconds)
    {
        if (!double.IsFinite(elapsedFrameSeconds) || elapsedFrameSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedFrameSeconds));
        // A paused session must freeze every subsystem, not just vehicle motion.
        if (IsPaused || elapsedFrameSeconds == 0)
            return 0;

        for (var i = 0; i < throttleCommandBuffer.Length; i++)
            throttleCommandBuffer[i] = setup.PoweredCars[i] ? ThrottlePercent : 0f;

        return setup.Session.AdvanceWithBrakeCommands(
            elapsedFrameSeconds,
            throttleCommandBuffer,
            zeroBrakeCommandBuffer,
            setup.MaximumBrakeForceNewtonsByCar,
            (index, car) => resistanceModel.Calculate(car, routeProfile is null
                ? resistanceParametersByCar[index]
                : resistanceParametersByCar[index] with { GradientRadians = routeProfile.Sample(car.DistanceMeters).GradeRadians }),
            zeroCouplerForceBuffer,
            beforeSimulationStep: stepSeconds =>
            {
                brakePipeState = brakePipeModel.Advance(brakePipeState, BrakePercent, stepSeconds);
                var pipeBrakeCommands = brakePipeModel.GetBrakePercentByCar(brakePipeState);
                brakeCylinderState = brakeCylinderModel.Advance(
                    brakeCylinderState, pipeBrakeCommands, stepSeconds);
            },
            brakePercentProvider: () => brakeCylinderModel.GetBrakePercentByCar(brakeCylinderState),
            couplerForceProvider: state =>
            {
                var forces = couplerDynamics.CalculatePerCarForces(
                    state, couplerRestGapsMeters, couplerSlackMeters,
                    couplerStiffness, couplerDamping,
                    couplerMaximumCompressionForces, couplerMaximumTensionForces);
                CouplerForceTelemetryNewtons = couplerDynamics.CalculatePerBoundaryForcesOnRearCars(
                    state, couplerRestGapsMeters, couplerSlackMeters,
                    couplerStiffness, couplerDamping,
                    couplerMaximumCompressionForces, couplerMaximumTensionForces);
                return forces;
            },
            tractionDirection: (int)ReverserPosition);
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
        ReverserPosition = TrainReverserPosition.Forward;
        CouplerForceTelemetryNewtons = Array.AsReadOnly(new double[Math.Max(0, setup.InitialState.Cars.Count - 1)]);
    }
}

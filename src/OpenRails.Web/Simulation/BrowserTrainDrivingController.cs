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

public sealed record BrowserTrainDrivingControllerSnapshot(
    TrainConsistSimulationSnapshot SessionState,
    float ThrottlePercent,
    double BrakePercent,
    double HandbrakePercent,
    TrainReverserPosition ReverserPosition,
    bool IsEmergencyStopApplied,
    bool LeftDoorsOpen,
    bool RightDoorsOpen,
    IReadOnlyList<double> CouplerForceTelemetryNewtons);


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
    private readonly double[] zeroCouplerForceBuffer;
    private readonly double[] additionalBrakeForceBuffer;

    public TrainConsistMotionState State => setup.Session.State;
    public double SimulationTimeSeconds => setup.Session.SimulationTimeSeconds;
    public double DroppedTimeSeconds => setup.Session.DroppedTimeSeconds;
    public bool IsPaused => setup.Session.IsPaused;
    public bool IsEmergencyStopApplied { get; private set; }
    public bool LeftDoorsOpen { get; private set; }
    public bool RightDoorsOpen { get; private set; }
    public bool CanOpenDoors => ThrottlePercent <= 0.01f &&
        State.Cars.All(car => Math.Abs(car.SpeedMetersPerSecond) <= 0.1);
    public float ThrottlePercent { get; private set; }
    public double BrakePercent { get; private set; }
    public double HandbrakePercent { get; private set; }
    public TrainReverserPosition ReverserPosition { get; private set; } = TrainReverserPosition.Forward;
    public bool CanChangeReverser => ThrottlePercent <= 0.01f &&
        State.Cars.All(car => Math.Abs(car.SpeedMetersPerSecond) <= 0.1);
    public TrainBrakePipeState BrakePipeState => setup.Session.BrakePipeState;
    public TrainBrakeCylinderState BrakeCylinderState => setup.Session.BrakeCylinderState;
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
            setup.MaximumHandBrakeForceNewtonsByCar.Count != setup.InitialState.Cars.Count ||
            setup.ResistanceParametersByCar.Count != setup.InitialState.Cars.Count ||
            setup.CouplerSlackMetersByBoundary.Count != Math.Max(0, setup.InitialState.Cars.Count - 1) ||
            setup.CouplerStiffnessByBoundary.Count != Math.Max(0, setup.InitialState.Cars.Count - 1) ||
            setup.CouplerDampingByBoundary.Count != Math.Max(0, setup.InitialState.Cars.Count - 1))
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
        zeroCouplerForceBuffer = new double[initialCars.Count];
        additionalBrakeForceBuffer = new double[initialCars.Count];
        couplerRestGapsMeters = Array.AsReadOnly(Enumerable.Range(0, Math.Max(0, initialCars.Count - 1))
            .Select(i => initialCars[i + 1].DistanceMeters - initialCars[i].DistanceMeters).ToArray());
        couplerSlackMeters = setup.CouplerSlackMetersByBoundary;
        couplerStiffness = setup.CouplerStiffnessByBoundary;
        couplerDamping = setup.CouplerDampingByBoundary;
        couplerMaximumCompressionForces = Array.AsReadOnly(setup.CouplerForceLimits
            .Select(limit => limit.MaximumCompressionNewtons).ToArray());
        couplerMaximumTensionForces = Array.AsReadOnly(setup.CouplerForceLimits
            .Select(limit => limit.MaximumTensionNewtons).ToArray());
        CouplerForceTelemetryNewtons = Array.AsReadOnly(
            new double[Math.Max(0, setup.InitialState.Cars.Count - 1)]);
    }

    public void SetThrottle(float percent)
    {
        if (!float.IsFinite(percent))
            throw new ArgumentOutOfRangeException(nameof(percent));
        if (!IsEmergencyStopApplied)
            ThrottlePercent = LeftDoorsOpen || RightDoorsOpen
                ? 0
                : Math.Clamp(percent, 0, 100);
    }

    /// <summary>Controls passenger-door sides; opening is interlocked while moving or applying traction.</summary>
    public void SetDoors(bool leftOpen, bool rightOpen)
    {
        if ((leftOpen && !LeftDoorsOpen) || (rightOpen && !RightDoorsOpen))
        {
            if (!CanOpenDoors)
                throw new InvalidOperationException("Doors can only be opened when the consist is stopped and throttle is zero.");
        }

        LeftDoorsOpen = leftOpen;
        RightDoorsOpen = rightOpen;
        if (LeftDoorsOpen || RightDoorsOpen)
            ThrottlePercent = 0;
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
        if (!IsEmergencyStopApplied)
            BrakePercent = Math.Clamp(percent, 0, 100);
    }

    /// <summary>Applies each vehicle's independently rated mechanical handbrake.</summary>
    public void SetHandbrake(double percent)
    {
        if (!double.IsFinite(percent))
            throw new ArgumentOutOfRangeException(nameof(percent));
        HandbrakePercent = Math.Clamp(percent, 0, 100);
    }

    /// <summary>Immediately cuts traction, latches the stop, and commands emergency brake-pipe venting.</summary>
    public void ApplyEmergencyStop()
    {
        ThrottlePercent = 0;
        BrakePercent = 100;
        IsEmergencyStopApplied = true;
    }

    /// <summary>Releases the emergency-stop latch; normal commands may then be accepted.</summary>
    public void ReleaseEmergencyStop()
    {
        IsEmergencyStopApplied = false;
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
        {
            throttleCommandBuffer[i] = setup.PoweredCars[i] ? ThrottlePercent : 0f;
            additionalBrakeForceBuffer[i] = setup.MaximumHandBrakeForceNewtonsByCar[i] * HandbrakePercent / 100d;
        }

        return setup.Session.AdvanceWithPneumaticBrakes(
            elapsedFrameSeconds,
            throttleCommandBuffer,
            BrakePercent,
            setup.MaximumBrakeForceNewtonsByCar,
            (index, car) => resistanceModel.Calculate(car, routeProfile is null
                ? resistanceParametersByCar[index]
                : resistanceParametersByCar[index] with { GradientRadians = routeProfile.Sample(car.DistanceMeters).GradeRadians }),
            zeroCouplerForceBuffer,
            emergency: IsEmergencyStopApplied,
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
            tractionDirection: (int)ReverserPosition,
            additionalBrakeForceNewtonsByCar: additionalBrakeForceBuffer);
    }

    /// <summary>Captures simulation and driver-command state as one restorable snapshot.</summary>
    public BrowserTrainDrivingControllerSnapshot CaptureState() =>
        new(
            setup.Session.CaptureState(),
            ThrottlePercent,
            BrakePercent,
            HandbrakePercent,
            ReverserPosition,
            IsEmergencyStopApplied,
            LeftDoorsOpen,
            RightDoorsOpen,
            CouplerForceTelemetryNewtons.ToArray());

    /// <summary>Validates the complete snapshot before restoring session or controller state.</summary>
    public void RestoreState(BrowserTrainDrivingControllerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.SessionState);
        ArgumentNullException.ThrowIfNull(snapshot.SessionState.MotionState);
        ArgumentNullException.ThrowIfNull(snapshot.SessionState.ClockState);
        ArgumentNullException.ThrowIfNull(snapshot.CouplerForceTelemetryNewtons);
        if (snapshot.SessionState.MotionState.Cars.Count != setup.InitialState.Cars.Count)
            throw new ArgumentException("Snapshot consist size must match the active simulation setup.", nameof(snapshot));
        if (!float.IsFinite(snapshot.ThrottlePercent) ||
            snapshot.ThrottlePercent < 0 || snapshot.ThrottlePercent > 100)
            throw new ArgumentException("Snapshot throttle must be finite and between 0 and 100.", nameof(snapshot));
        if (!double.IsFinite(snapshot.BrakePercent) ||
            snapshot.BrakePercent < 0 || snapshot.BrakePercent > 100)
            throw new ArgumentException("Snapshot brake command must be finite and between 0 and 100.", nameof(snapshot));
        if (!double.IsFinite(snapshot.HandbrakePercent) || snapshot.HandbrakePercent < 0 || snapshot.HandbrakePercent > 100)
            throw new ArgumentException("Snapshot handbrake command must be finite and between 0 and 100 percent.", nameof(snapshot));
        if (!Enum.IsDefined(snapshot.ReverserPosition))
            throw new ArgumentException("Snapshot reverser position is invalid.", nameof(snapshot));
        if (snapshot.IsEmergencyStopApplied &&
            (snapshot.ThrottlePercent != 0 || snapshot.BrakePercent != 100))
            throw new ArgumentException("An emergency-stop snapshot must have zero throttle and full brake.", nameof(snapshot));
        if ((snapshot.LeftDoorsOpen || snapshot.RightDoorsOpen) && snapshot.ThrottlePercent > 0.01f)
            throw new ArgumentException("A snapshot cannot apply traction while passenger doors are open.", nameof(snapshot));
        if ((snapshot.LeftDoorsOpen || snapshot.RightDoorsOpen) &&
            snapshot.SessionState.MotionState.Cars.Any(car => Math.Abs(car.SpeedMetersPerSecond) > 0.1))
            throw new ArgumentException("A snapshot cannot have passenger doors open while the consist is moving.", nameof(snapshot));
        if (snapshot.CouplerForceTelemetryNewtons.Count != Math.Max(0, setup.InitialState.Cars.Count - 1) ||
            snapshot.CouplerForceTelemetryNewtons.Any(force => !double.IsFinite(force)))
            throw new ArgumentException("Snapshot coupler telemetry must contain one finite value per car boundary.", nameof(snapshot));

        // Session restore validates its motion, clock, and pneumatic state before mutation.
        setup.Session.RestoreState(snapshot.SessionState);
        ThrottlePercent = snapshot.ThrottlePercent;
        BrakePercent = snapshot.BrakePercent;
        HandbrakePercent = snapshot.HandbrakePercent;
        ReverserPosition = snapshot.ReverserPosition;
        IsEmergencyStopApplied = snapshot.IsEmergencyStopApplied;
        LeftDoorsOpen = snapshot.LeftDoorsOpen;
        RightDoorsOpen = snapshot.RightDoorsOpen;
        CouplerForceTelemetryNewtons = Array.AsReadOnly(snapshot.CouplerForceTelemetryNewtons.ToArray());
    }

    public void Pause() => setup.Session.Pause();
    public void Resume() => setup.Session.Resume();
    public void Reset()
    {
        setup.Session.Reset(setup.InitialState);
        ThrottlePercent = 0;
        BrakePercent = 0;
        HandbrakePercent = 0;
        ReverserPosition = TrainReverserPosition.Forward;
        IsEmergencyStopApplied = false;
        LeftDoorsOpen = false;
        RightDoorsOpen = false;
        CouplerForceTelemetryNewtons = Array.AsReadOnly(new double[Math.Max(0, setup.InitialState.Cars.Count - 1)]);
    }
}

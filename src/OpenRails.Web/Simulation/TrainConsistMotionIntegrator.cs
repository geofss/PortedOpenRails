namespace OpenRails.Web.Simulation;

/// <summary>
/// Integrates a consist's per-car longitudinal states using supplied per-car
/// force breakdowns and signed coupler forces. Coupler force generation and
/// slack/break logic are intentionally separate, matching the upstream Train
/// architecture where TrainCar forces are combined before UpdateCarSpeeds.
/// </summary>
public sealed class TrainConsistMotionIntegrator
{
    private readonly TrainCarForceModel forceModel = new();
    private readonly TrainCarMotionIntegrator motionIntegrator = new();

    public TrainConsistMotionState Step(
        TrainConsistMotionState state,
        IReadOnlyList<TrainCarForceBreakdown> carForces,
        IReadOnlyList<double> couplerForcesNewtons,
        double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(carForces);
        ArgumentNullException.ThrowIfNull(couplerForcesNewtons);
        if (state.Cars.Count != carForces.Count ||
            state.Cars.Count != couplerForcesNewtons.Count)
            throw new ArgumentException("Each consist car must have one force breakdown and one signed coupler force.");
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

        var nextCars = new TrainCarMotionState[state.Cars.Count];
        for (var i = 0; i < state.Cars.Count; i++)
        {
            var couplerForce = couplerForcesNewtons[i];
            if (!double.IsFinite(couplerForce))
                throw new ArgumentOutOfRangeException(nameof(couplerForcesNewtons));
            var netForce = forceModel.CalculateNetLongitudinalForce(state.Cars[i], carForces[i]) +
                couplerForce;
            nextCars[i] = motionIntegrator.Step(state.Cars[i], netForce, elapsedSeconds).State;
        }

        return new TrainConsistMotionState(nextCars);
    }
}

public sealed record TrainConsistMotionState(IReadOnlyList<TrainCarMotionState> Cars)
{
    public TrainConsistMotionState(IEnumerable<TrainCarMotionState> cars)
        : this(Array.AsReadOnly(cars?.ToArray() ?? throw new ArgumentNullException(nameof(cars))))
    {
    }
}

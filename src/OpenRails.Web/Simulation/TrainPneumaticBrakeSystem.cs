namespace OpenRails.Web.Simulation;

/// <summary>
/// Couples brake-pipe propagation to per-vehicle brake-cylinder response.
/// The state advances only when the owning fixed-step simulation accepts a tick.
/// </summary>
public sealed class TrainPneumaticBrakeSystem
{
    private readonly TrainBrakePipeModel pipeModel;
    private readonly TrainBrakeCylinderModel cylinderModel;

    public TrainBrakePipeState PipeState { get; private set; }
    public TrainBrakeCylinderState CylinderState { get; private set; }

    public TrainPneumaticBrakeSystem(
        int carCount,
        TrainBrakePipeModel? pipeModel = null,
        TrainBrakeCylinderModel? cylinderModel = null)
    {
        this.pipeModel = pipeModel ?? new TrainBrakePipeModel();
        this.cylinderModel = cylinderModel ?? new TrainBrakeCylinderModel();
        PipeState = this.pipeModel.CreateInitialState(carCount);
        CylinderState = this.cylinderModel.CreateInitialState(carCount);
    }

    public IReadOnlyList<double> BrakePercentByCar =>
        cylinderModel.GetBrakePercentByCar(CylinderState);

    public void Advance(double brakeCommandPercent, double elapsedSeconds, bool emergency = false)
    {
        var nextPipe = pipeModel.Advance(PipeState, brakeCommandPercent, elapsedSeconds, emergency);
        var pipeBrakePercent = pipeModel.GetBrakePercentByCar(nextPipe);
        var nextCylinders = cylinderModel.Advance(CylinderState, pipeBrakePercent, elapsedSeconds);
        PipeState = nextPipe;
        CylinderState = nextCylinders;
    }

    public void ValidateSnapshot(TrainBrakePipeState pipeState, TrainBrakeCylinderState cylinderState)
    {
        ArgumentNullException.ThrowIfNull(pipeState);
        ArgumentNullException.ThrowIfNull(cylinderState);
        var carCount = PipeState.PressuresKPa.Count;
        if (pipeState.PressuresKPa.Count != carCount ||
            cylinderState.PressuresKPa.Count != carCount ||
            pipeState.PressuresKPa.Any(value => !double.IsFinite(value) ||
                value < TrainBrakePipeModel.EmergencyPressureKPa ||
                value > TrainBrakePipeModel.NormalPressureKPa) ||
            cylinderState.PressuresKPa.Any(value => !double.IsFinite(value) ||
                value < 0 || value > TrainBrakeCylinderModel.MaximumCylinderPressureKPa))
            throw new ArgumentException("Pneumatic snapshot pressures must be valid and match the consist size.");
    }

    public void RestoreState(TrainBrakePipeState pipeState, TrainBrakeCylinderState cylinderState)
    {
        ValidateSnapshot(pipeState, cylinderState);
        PipeState = new TrainBrakePipeState(pipeState.PressuresKPa);
        CylinderState = new TrainBrakeCylinderState(cylinderState.PressuresKPa);
    }

    public void Reset()
    {
        PipeState = pipeModel.CreateInitialState(PipeState.PressuresKPa.Count);
        CylinderState = cylinderModel.CreateInitialState(CylinderState.PressuresKPa.Count);
    }
}

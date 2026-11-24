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

    public void Reset()
    {
        PipeState = pipeModel.CreateInitialState(PipeState.PressuresKPa.Count);
        CylinderState = cylinderModel.CreateInitialState(CylinderState.PressuresKPa.Count);
    }
}

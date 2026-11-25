namespace OpenRails.Web.Simulation;

/// <summary>
/// Couples brake-pipe propagation to per-vehicle auxiliary reservoirs, distributors, and brake cylinders.
/// The state advances only when the owning fixed-step simulation accepts a tick.
/// </summary>
public sealed class TrainPneumaticBrakeSystem
{
    private readonly TrainBrakePipeModel pipeModel;
    private readonly TrainBrakeDistributorModel distributorModel;
    private readonly TrainBrakeCylinderModel cylinderModel;
    private readonly IReadOnlyList<bool>? distributorEnabledByCar;

    public TrainBrakePipeState PipeState { get; private set; }
    public TrainBrakeCylinderState CylinderState { get; private set; }
    public TrainBrakeDistributorState DistributorState { get; private set; }

    public TrainPneumaticBrakeSystem(
        int carCount,
        TrainBrakePipeModel? pipeModel = null,
        TrainBrakeCylinderModel? cylinderModel = null,
        TrainBrakeDistributorModel? distributorModel = null,
        IReadOnlyList<RollingStockBrakeProfile>? brakeProfilesByCar = null)
    {
        if (brakeProfilesByCar is not null && brakeProfilesByCar.Count != carCount)
            throw new ArgumentException("Brake profiles must match the pneumatic consist size.", nameof(brakeProfilesByCar));
        distributorEnabledByCar = brakeProfilesByCar?.Select(profile =>
            profile.Support != RollingStockBrakeSupport.UnsupportedOrUnverified).ToArray();
        this.pipeModel = pipeModel ?? new TrainBrakePipeModel();
        this.distributorModel = distributorModel ?? new TrainBrakeDistributorModel();
        this.cylinderModel = cylinderModel ?? new TrainBrakeCylinderModel();
        PipeState = this.pipeModel.CreateInitialState(carCount);
        DistributorState = this.distributorModel.CreateInitialState(carCount);
        CylinderState = this.cylinderModel.CreateInitialState(carCount);
    }

    public IReadOnlyList<double> BrakePercentByCar =>
        cylinderModel.GetBrakePercentByCar(CylinderState);

    public void Advance(double brakeCommandPercent, double elapsedSeconds, bool emergency = false)
    {
        var nextPipe = pipeModel.Advance(PipeState, brakeCommandPercent, elapsedSeconds, emergency);
        var (nextDistributor, nextCylinders) = distributorModel.Advance(
            DistributorState, nextPipe, CylinderState, elapsedSeconds, distributorEnabledByCar);
        PipeState = nextPipe;
        DistributorState = nextDistributor;
        CylinderState = nextCylinders;
    }

    public void ValidateSnapshot(TrainBrakePipeState pipeState, TrainBrakeCylinderState cylinderState, TrainBrakeDistributorState? distributorState = null)
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
                value < 0 || value > TrainBrakeCylinderModel.MaximumCylinderPressureKPa) ||
            (distributorState is not null && (distributorState.Modes.Count != carCount ||
                distributorState.AuxiliaryReservoirPressuresKPa.Count != carCount ||
                distributorState.ControlPressuresKPa.Count != carCount ||
                distributorState.LastPipePressuresKPa.Count != carCount)))
            throw new ArgumentException("Pneumatic snapshot pressures must be valid and match the consist size.");
    }

    public void RestoreState(TrainBrakePipeState pipeState, TrainBrakeCylinderState cylinderState, TrainBrakeDistributorState? distributorState = null)
    {
        ValidateSnapshot(pipeState, cylinderState, distributorState);
        PipeState = new TrainBrakePipeState(pipeState.PressuresKPa);
        CylinderState = new TrainBrakeCylinderState(cylinderState.PressuresKPa);
        DistributorState = distributorState is null
            ? distributorModel.CreateInitialState(PipeState.PressuresKPa.Count)
            : new TrainBrakeDistributorState(distributorState.AuxiliaryReservoirPressuresKPa, distributorState.Modes, distributorState.ControlPressuresKPa, distributorState.LastPipePressuresKPa);
    }

    public void Reset()
    {
        PipeState = pipeModel.CreateInitialState(PipeState.PressuresKPa.Count);
        CylinderState = cylinderModel.CreateInitialState(CylinderState.PressuresKPa.Count);
        DistributorState = distributorModel.CreateInitialState(PipeState.PressuresKPa.Count);
    }
}

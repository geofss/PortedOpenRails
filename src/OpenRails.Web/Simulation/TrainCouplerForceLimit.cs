namespace OpenRails.Web.Simulation;

/// <summary>Per-coupler force ratings in Newtons, expressed as positive magnitudes.</summary>
public sealed record TrainCouplerForceLimit(double MaximumCompressionNewtons, double MaximumTensionNewtons)
{
    public void Validate()
    {
        if (!double.IsFinite(MaximumCompressionNewtons) || MaximumCompressionNewtons < 0 ||
            !double.IsFinite(MaximumTensionNewtons) || MaximumTensionNewtons < 0)
            throw new ArgumentOutOfRangeException(nameof(TrainCouplerForceLimit),
                "Coupler compression and tension limits must be finite and non-negative.");
    }

    public bool Allows(double signedBoundaryForceNewtons) =>
        signedBoundaryForceNewtons >= -MaximumTensionNewtons &&
        signedBoundaryForceNewtons <= MaximumCompressionNewtons;
}

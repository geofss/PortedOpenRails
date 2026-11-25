namespace OpenRails.Web.Simulation;

/// <summary>
/// Describes how much of a vehicle's declared MSTS/Open Rails brake metadata
/// the current browser pneumatic prototype can safely represent.
/// </summary>
public enum RollingStockBrakeSupport
{
    Unspecified,
    PrototypeAirDistributor,
    UnsupportedOrUnverified
}

public sealed record RollingStockBrakeProfile(
    string? DeclaredSystemType,
    string? DeclaredEquipmentType,
    RollingStockBrakeSupport Support)
{
    public static RollingStockBrakeProfile FromMetadata(Parsing.RollingStockMetadata? metadata)
    {
        if (metadata is null ||
            (string.IsNullOrWhiteSpace(metadata.BrakeSystemType) &&
             string.IsNullOrWhiteSpace(metadata.BrakeEquipmentType)))
            return new(null, null, RollingStockBrakeSupport.Unspecified);

        var system = Normalize(metadata.BrakeSystemType);
        var equipment = Normalize(metadata.BrakeEquipmentType);

        // The current browser model is only a simplified air-pipe/distributor
        // approximation. Do not silently treat vacuum, EP/ECP, manual, SME,
        // or unknown declarations as equivalent.
        var supportedSystem = system is "air_single_pipe" or "air_twin_pipe";
        // Equipment-less declarations are ambiguous, and the browser model
        // does not yet implement equipment-specific valve behaviour. Only
        // explicit, verified basic air-distributor declarations opt in.
        var supportedEquipment = equipment is "distributor" or "triple_valve";
        var support = supportedSystem && supportedEquipment
            ? RollingStockBrakeSupport.PrototypeAirDistributor
            : RollingStockBrakeSupport.UnsupportedOrUnverified;

        return new(metadata.BrakeSystemType, metadata.BrakeEquipmentType, support);
    }

    private static string Normalize(string? value) =>
        (value ?? string.Empty).Trim().Trim('(', ')').Replace(" ", "_").ToLowerInvariant();
}

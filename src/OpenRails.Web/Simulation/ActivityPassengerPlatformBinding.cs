using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

/// <summary>
/// Resolves ACT Player_Traffic stop PlatformStartID values against the native
/// TDB TrItem table, matching Activity's PlatformItem and linked-platform checks.
/// This binds timetable data only; runtime position tracking still needs native
/// platform geometry and train-stop notifications.
/// </summary>
public static class ActivityPassengerPlatformBinding
{
    public static ActivityPassengerPlatformBindingResult Bind(
        ActivityPassengerServiceDefinition service, TrackDatabase database)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(database);

        var bindings = new List<ActivityPassengerPlatformStopBinding>(service.Stops.Count);
        var diagnostics = new List<string>();
        foreach (var stop in service.Stops)
        {
            if (stop.PlatformStartId < 0 || stop.PlatformStartId >= database.TrackItemCount)
            {
                diagnostics.Add($"Activity platform item {stop.PlatformStartId} is outside the TDB TrItem table.");
                continue;
            }

            var platform = database.TrackItems[stop.PlatformStartId];
            if (platform is null || !string.Equals(platform.Type, "platform", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add($"Activity PlatformStartID {stop.PlatformStartId} does not resolve to a platform TrItem.");
                continue;
            }

            if (platform.LinkedItemId is not { } linkedId ||
                linkedId >= database.TrackItemCount ||
                database.TrackItems[(int)linkedId] is not { Type: "platform" } linkedPlatform)
            {
                diagnostics.Add($"Platform TrItem {platform.Index} has no valid linked platform TrItem.");
                continue;
            }

            bindings.Add(new ActivityPassengerPlatformStopBinding(stop, platform, linkedPlatform));
        }

        return new ActivityPassengerPlatformBindingResult(service, bindings.AsReadOnly(), diagnostics.AsReadOnly());
    }
}

public sealed record ActivityPassengerPlatformStopBinding(
    ActivityPassengerStopSchedule Schedule,
    TrackItem PlatformStart,
    TrackItem PlatformLinked);

public sealed record ActivityPassengerPlatformBindingResult(
    ActivityPassengerServiceDefinition Service,
    IReadOnlyList<ActivityPassengerPlatformStopBinding> Stops,
    IReadOnlyList<string> Diagnostics)
{
    public bool IsFullyBound => Stops.Count == Service.Stops.Count && Diagnostics.Count == 0;
}

using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

/// <summary>
/// Runtime binding produced after resolving a native timetable train's consist,
/// path and route reservations against the loaded route package.
/// </summary>
public sealed record NativeTimetableServiceRegistration(
    AiServiceInterlockingSession Session,
    IReadOnlyList<uint> RouteCircuitIds,
    IReadOnlyDictionary<uint, uint> RequiredJunctionBranches,
    AiTimetableTrainRuntimeBinding? RuntimeBinding = null);

public sealed record NativeTimetableDispatchIssue(string Train, string Message);

public sealed record NativeTimetableDispatcherBuildResult(
    AiTimetableDispatcher Dispatcher,
    IReadOnlyList<string> RegisteredServiceIds,
    IReadOnlyList<NativeTimetableDispatchIssue> Issues,
    IReadOnlyList<AiTimetableTrainRuntimeBinding> RuntimeBindings);

/// <summary>
/// Converts parsed native timetable columns into dispatcher registrations. The
/// caller supplies route/consist resolution, while this boundary enforces native
/// service identity, departure-time and reservation metadata before registration.
/// </summary>
public sealed class NativeTimetableDispatcherBuilder
{
    public NativeTimetableDispatcherBuildResult Build(
        NativeTimetableDefinition timetable,
        Func<NativeTimetableTrainDefinition, NativeTimetableServiceRegistration> resolveService)
    {
        ArgumentNullException.ThrowIfNull(timetable);
        ArgumentNullException.ThrowIfNull(resolveService);

        var dispatcher = new AiTimetableDispatcher();
        var registered = new List<string>();
        var issues = new List<NativeTimetableDispatchIssue>();
        var runtimeBindings = new List<AiTimetableTrainRuntimeBinding>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var train in timetable.Trains)
        {
            if (string.IsNullOrWhiteSpace(train.Train))
            {
                issues.Add(new NativeTimetableDispatchIssue("<unnamed>", "Native timetable contains an empty service name."));
                continue;
            }
            if (!seen.Add(train.Train))
            {
                issues.Add(new NativeTimetableDispatchIssue(train.Train, "Duplicate native timetable service name."));
                continue;
            }
            if (!train.TryGetDepartureSeconds(out var departureSeconds))
            {
                issues.Add(new NativeTimetableDispatchIssue(train.Train,
                    $"Invalid or missing native start time '{train.StartTime}'."));
                continue;
            }
            if (string.IsNullOrWhiteSpace(train.Path) || string.IsNullOrWhiteSpace(train.Consist))
            {
                issues.Add(new NativeTimetableDispatchIssue(train.Train,
                    "Native service is missing its path or consist binding."));
                continue;
            }

            try
            {
                var registration = resolveService(train);
                ArgumentNullException.ThrowIfNull(registration);
                ArgumentNullException.ThrowIfNull(registration.Session);
                ArgumentNullException.ThrowIfNull(registration.RouteCircuitIds);
                ArgumentNullException.ThrowIfNull(registration.RequiredJunctionBranches);
                if (registration.RouteCircuitIds.Count == 0)
                    throw new InvalidDataException("Resolved native service has no route track circuits.");
                if (registration.RuntimeBinding is { } runtimeBinding &&
                    (!runtimeBinding.ServiceId.Equals(train.Train, StringComparison.OrdinalIgnoreCase) ||
                     !ReferenceEquals(runtimeBinding.Session, registration.Session)))
                    throw new InvalidDataException("Native timetable runtime binding must match the registered service identity and interlocking session.");

                dispatcher.AddService(train.Train, departureSeconds, registration.Session,
                    registration.RouteCircuitIds, registration.RequiredJunctionBranches);
                registered.Add(train.Train);
                if (registration.RuntimeBinding is { } binding)
                    runtimeBindings.Add(binding);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidDataException or
                InvalidOperationException or KeyNotFoundException)
            {
                issues.Add(new NativeTimetableDispatchIssue(train.Train, exception.Message));
            }
        }

        return new NativeTimetableDispatcherBuildResult(
            dispatcher,
            Array.AsReadOnly(registered.ToArray()),
            Array.AsReadOnly(issues.ToArray()),
            Array.AsReadOnly(runtimeBindings.ToArray()));
    }
}

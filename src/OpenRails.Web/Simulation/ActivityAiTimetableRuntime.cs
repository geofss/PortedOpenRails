using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

/// <summary>
/// Runs native activity event progression and AI timetable dispatch from the
/// same fixed-step simulation clock. Activity completion prevents later AI
/// departures; pending activity outcomes remain available for UI acknowledgement.
/// </summary>
public sealed class ActivityAiTimetableRuntime
{
    private readonly ActivityExecutionSession activity;
    private readonly AiTimetableDispatcher timetable;

    public ActivityAiTimetableRuntime(ActivityEventSet activityDefinition, AiTimetableDispatcher timetable)
    {
        activity = new ActivityExecutionSession(activityDefinition ??
            throw new ArgumentNullException(nameof(activityDefinition)));
        this.timetable = timetable ?? throw new ArgumentNullException(nameof(timetable));
    }

    public bool IsEnded => activity.IsEnded;
    public ActivityEventExecutionResult? PendingEvent => activity.PendingEvent;
    public IReadOnlyList<string> UnsupportedEvents => activity.UnsupportedEvents;

    public ActivityAiTimetableTickResult Advance(double deltaSeconds, ActivityPlayerState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var activityEvent = activity.Advance(deltaSeconds, player);
        var dispatched = activity.IsEnded || activityEvent is not null
            ? Array.Empty<string>()
            : timetable.Advance(deltaSeconds);
        return new ActivityAiTimetableTickResult(activity.ElapsedSeconds,
            activityEvent, Array.AsReadOnly(dispatched.ToArray()), activity.IsEnded);
    }

    public ActivityEventExecutionResult? AcknowledgeActivityEvent() => activity.Acknowledge();
}

public sealed record ActivityAiTimetableTickResult(
    double ActivityElapsedSeconds,
    ActivityEventExecutionResult? ActivityEvent,
    IReadOnlyList<string> DispatchedServices,
    bool ActivityEnded);

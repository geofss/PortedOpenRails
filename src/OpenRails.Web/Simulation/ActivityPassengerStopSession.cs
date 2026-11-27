namespace OpenRails.Web.Simulation;

/// <summary>
/// Browser-side state machine for the timetable-mode passenger stop lifecycle
/// in Open Rails ActivityTaskPassengerStopAt.NotifyEvent.
/// </summary>
public sealed class ActivityPassengerStopSession
{
    private readonly double scheduledArrivalSeconds;
    private readonly double scheduledDepartureSeconds;
    private readonly double platformMinimumWaitingSeconds;
    private readonly bool isLastTask;
    private readonly double? assumedActualArrivalSeconds;
    private ActivityPassengerStopTimingResult? timing;
    private bool arrived;
    private bool mayDepart;

    public double? ActualArrivalSeconds { get; private set; }
    public double? ActualDepartureSeconds { get; private set; }
    public bool? IsCompleted { get; private set; }
    public bool Arrived => arrived;
    public bool MayDepart => mayDepart;
    public double? BoardingEndClockSeconds => timing?.BoardingEndClockSeconds;
    public string DisplayMessage { get; private set; } = "";

    public ActivityPassengerStopSession(double scheduledArrivalSeconds,
        double scheduledDepartureSeconds, double platformMinimumWaitingSeconds,
        bool isLastTask = false, double? assumedActualArrivalSeconds = null)
    {
        if (!double.IsFinite(scheduledArrivalSeconds) ||
            !double.IsFinite(scheduledDepartureSeconds) ||
            !double.IsFinite(platformMinimumWaitingSeconds) ||
            platformMinimumWaitingSeconds < 0 ||
            assumedActualArrivalSeconds.HasValue && !double.IsFinite(assumedActualArrivalSeconds.Value))
            throw new ArgumentOutOfRangeException(nameof(scheduledArrivalSeconds));

        this.scheduledArrivalSeconds = scheduledArrivalSeconds;
        this.scheduledDepartureSeconds = scheduledDepartureSeconds;
        this.platformMinimumWaitingSeconds = platformMinimumWaitingSeconds;
        this.isLastTask = isLastTask;
        this.assumedActualArrivalSeconds = assumedActualArrivalSeconds;
    }

    /// <summary>Processes the native TrainStop notification.</summary>
    public ActivityPassengerStopState OnTrainStop(double clockTimeSeconds, bool isAtStation)
    {
        ValidateClock(clockTimeSeconds);
        if (isAtStation && !arrived)
        {
            ActualArrivalSeconds = assumedActualArrivalSeconds ?? clockTimeSeconds;
            timing = ActivityPassengerStopTiming.Calculate(
                scheduledArrivalSeconds, scheduledDepartureSeconds,
                ActualArrivalSeconds.Value, platformMinimumWaitingSeconds);
            arrived = true;
            DisplayMessage = "";
        }
        return Snapshot(clockTimeSeconds);
    }

    /// <summary>
    /// Processes the native Timer notification. A nearby restrictive signal
    /// prevents departure until it clears or permission is granted.
    /// </summary>
    public ActivityPassengerStopState OnTimer(double clockTimeSeconds,
        double? distanceToNextSignalMeters = null,
        bool nextSignalIsStop = false,
        bool signalPermissionGranted = false)
    {
        ValidateClock(clockTimeSeconds);
        if (distanceToNextSignalMeters.HasValue &&
            (!double.IsFinite(distanceToNextSignalMeters.Value) || distanceToNextSignalMeters.Value < 0))
            throw new ArgumentOutOfRangeException(nameof(distanceToNextSignalMeters));
        if (!arrived || timing is null)
            return Snapshot(clockTimeSeconds);

        var remaining = timing.RemainingSeconds(clockTimeSeconds);
        if (remaining > 0)
        {
            DisplayMessage = $"Passenger boarding completes in {remaining / 60:D2}:{remaining % 60:D2}";
        }
        else if (!mayDepart)
        {
            var signalBlocksDeparture = distanceToNextSignalMeters is >= 0 and < 300 &&
                nextSignalIsStop && !signalPermissionGranted;
            if (signalBlocksDeparture)
            {
                DisplayMessage = "Passenger boarding completed. Waiting for signal ahead to clear.";
            }
            else
            {
                mayDepart = true;
                DisplayMessage = "Passenger boarding completed. You may depart now.";
            }

            // Native ActivityTaskPassengerStopAt marks the final task complete
            // once boarding has elapsed, even if a nearby signal still blocks departure.
            if (isLastTask)
                IsCompleted = true;
        }
        return Snapshot(clockTimeSeconds);
    }

    /// <summary>Processes the native TrainStart notification.</summary>
    public ActivityPassengerStopState OnTrainStart(double clockTimeSeconds)
    {
        ValidateClock(clockTimeSeconds);
        if (arrived)
        {
            ActualDepartureSeconds = clockTimeSeconds;
            IsCompleted = mayDepart;
        }
        return Snapshot(clockTimeSeconds);
    }

    /// <summary>Marks a scheduled stop missed, matching the native Timer path.</summary>
    public ActivityPassengerStopState OnMissedStation(double clockTimeSeconds)
    {
        ValidateClock(clockTimeSeconds);
        if (!arrived)
            IsCompleted = false;
        return Snapshot(clockTimeSeconds);
    }

    private ActivityPassengerStopState Snapshot(double clockTimeSeconds)
    {
        var remaining = timing?.RemainingSeconds(clockTimeSeconds) ?? 0;
        return new ActivityPassengerStopState(arrived, mayDepart, IsCompleted,
            ActualArrivalSeconds, ActualDepartureSeconds, timing?.BoardingEndClockSeconds,
            remaining, DisplayMessage);
    }

    private static void ValidateClock(double clockTimeSeconds)
    {
        if (!double.IsFinite(clockTimeSeconds))
            throw new ArgumentOutOfRangeException(nameof(clockTimeSeconds));
    }
}

public sealed record ActivityPassengerStopState(bool Arrived, bool MayDepart,
    bool? IsCompleted, double? ActualArrivalSeconds, double? ActualDepartureSeconds,
    double? BoardingEndClockSeconds, int RemainingSeconds, string DisplayMessage);

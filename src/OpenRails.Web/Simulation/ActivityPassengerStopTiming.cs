namespace OpenRails.Web.Simulation;

/// <summary>
/// Calculates the passenger boarding window using the timetable-mode rules in
/// Open Rails ActivityTaskPassengerStopAt.NotifyEvent(TrainStop).
/// Times are simulation-clock seconds relative to midnight.
/// </summary>
public static class ActivityPassengerStopTiming
{
    public static ActivityPassengerStopTimingResult Calculate(
        double scheduledArrivalSeconds,
        double scheduledDepartureSeconds,
        double actualArrivalSeconds,
        double platformMinimumWaitingSeconds)
    {
        if (!double.IsFinite(scheduledArrivalSeconds) ||
            !double.IsFinite(scheduledDepartureSeconds) ||
            !double.IsFinite(actualArrivalSeconds) ||
            !double.IsFinite(platformMinimumWaitingSeconds) ||
            platformMinimumWaitingSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(platformMinimumWaitingSeconds),
                "Activity passenger-stop times must be finite and minimum waiting time non-negative.");

        var plannedBoardingSeconds = scheduledDepartureSeconds - scheduledArrivalSeconds;
        var punctualBoardingSeconds = scheduledDepartureSeconds - actualArrivalSeconds;
        if (!double.IsFinite(plannedBoardingSeconds) || !double.IsFinite(punctualBoardingSeconds))
            throw new ArgumentOutOfRangeException(nameof(scheduledDepartureSeconds),
                "Derived passenger-stop schedule intervals must remain finite.");
        var expectedBoardingSeconds = plannedBoardingSeconds > 0
            ? plannedBoardingSeconds
            : platformMinimumWaitingSeconds;

        var boardingSeconds = punctualBoardingSeconds;
        if (punctualBoardingSeconds < expectedBoardingSeconds)
        {
            // A deliberately tight schedule keeps its planned dwell. A generous
            // schedule preserves the platform minimum even after a late arrival.
            boardingSeconds = plannedBoardingSeconds > 0 &&
                plannedBoardingSeconds < platformMinimumWaitingSeconds
                ? plannedBoardingSeconds
                : Math.Max(punctualBoardingSeconds, platformMinimumWaitingSeconds);
        }

        // Native code starts with ClockTime + BoardingS and subtracts the time
        // elapsed since ActArrive. This is algebraically ActArrive + BoardingS.
        var boardingEndSeconds = actualArrivalSeconds + boardingSeconds;
        if (!double.IsFinite(boardingEndSeconds))
            throw new ArgumentOutOfRangeException(nameof(actualArrivalSeconds),
                "Passenger-stop boarding end time exceeded the finite activity clock range.");
        return new ActivityPassengerStopTimingResult(
            plannedBoardingSeconds, punctualBoardingSeconds, boardingSeconds,
            boardingEndSeconds);
    }
}

public sealed record ActivityPassengerStopTimingResult(
    double PlannedBoardingSeconds,
    double PunctualBoardingSeconds,
    double BoardingDurationSeconds,
    double BoardingEndClockSeconds)
{
    public int RemainingSeconds(double clockTimeSeconds)
    {
        if (!double.IsFinite(clockTimeSeconds))
            throw new ArgumentOutOfRangeException(nameof(clockTimeSeconds));
        return checked((int)Math.Ceiling(BoardingEndClockSeconds - clockTimeSeconds));
    }

    public bool MayDepart(double clockTimeSeconds) => RemainingSeconds(clockTimeSeconds) <= 0;
}

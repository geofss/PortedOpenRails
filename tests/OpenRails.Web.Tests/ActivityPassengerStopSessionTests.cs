using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class ActivityPassengerStopSessionTests
{
    [Fact]
    public void RequiresArrivalAtTheScheduledPlatformBeforeBoardingStarts()
    {
        var stop = new ActivityPassengerStopSession(100, 160, 30);

        Assert.False(stop.OnTrainStop(100, isAtStation: false).Arrived);
        var arrived = stop.OnTrainStop(100, isAtStation: true);

        Assert.True(arrived.Arrived);
        Assert.Equal(160, arrived.BoardingEndClockSeconds);
    }

    [Fact]
    public void DisplaysBoardingCountdownThenAllowsDeparture()
    {
        var stop = new ActivityPassengerStopSession(100, 160, 30);
        stop.OnTrainStop(100, isAtStation: true);

        var waiting = stop.OnTimer(159.2);
        Assert.Equal(1, waiting.RemainingSeconds);
        Assert.False(waiting.MayDepart);
        Assert.Contains("00:01", waiting.DisplayMessage);

        var ready = stop.OnTimer(160);
        Assert.True(ready.MayDepart);
        Assert.Contains("You may depart", ready.DisplayMessage);
    }

    [Fact]
    public void RestrictiveSignalWithinThreeHundredMetersBlocksDeparture()
    {
        var stop = new ActivityPassengerStopSession(100, 130, 30);
        stop.OnTrainStop(100, isAtStation: true);

        var blocked = stop.OnTimer(130, distanceToNextSignalMeters: 299,
            nextSignalIsStop: true, signalPermissionGranted: false);
        Assert.False(blocked.MayDepart);
        Assert.Contains("Waiting for signal", blocked.DisplayMessage);

        var permitted = stop.OnTimer(130, distanceToNextSignalMeters: 299,
            nextSignalIsStop: true, signalPermissionGranted: true);
        Assert.True(permitted.MayDepart);
    }

    [Fact]
    public void SignalAtThreeHundredMetersDoesNotBlockDeparture()
    {
        var stop = new ActivityPassengerStopSession(100, 130, 30);
        stop.OnTrainStop(100, isAtStation: true);

        Assert.True(stop.OnTimer(130, distanceToNextSignalMeters: 300,
            nextSignalIsStop: true).MayDepart);
    }

    [Fact]
    public void FinalTaskCompletesAfterBoardingEvenWhenSignalStillBlocksDeparture()
    {
        var stop = new ActivityPassengerStopSession(100, 130, 30, isLastTask: true);
        stop.OnTrainStop(100, isAtStation: true);

        var state = stop.OnTimer(130, distanceToNextSignalMeters: 200,
            nextSignalIsStop: true);

        Assert.False(state.MayDepart);
        Assert.True(state.IsCompleted);
    }

    [Fact]
    public void TrainStartRecordsWhetherDepartureWasPermitted()
    {
        var early = new ActivityPassengerStopSession(100, 160, 30);
        early.OnTrainStop(100, isAtStation: true);
        Assert.False(early.OnTrainStart(110).IsCompleted);

        var onTime = new ActivityPassengerStopSession(100, 130, 30);
        onTime.OnTrainStop(100, isAtStation: true);
        onTime.OnTimer(130);
        var departed = onTime.OnTrainStart(131);
        Assert.True(departed.IsCompleted);
        Assert.Equal(131, departed.ActualDepartureSeconds);
    }

    [Fact]
    public void UsesScheduledArrivalWhenActivityStartsAfterScheduledArrival()
    {
        var stop = new ActivityPassengerStopSession(100, 160, 30,
            assumedActualArrivalSeconds: 100);

        var state = stop.OnTrainStop(150, isAtStation: true);

        Assert.Equal(100, state.ActualArrivalSeconds);
        Assert.Equal(160, state.BoardingEndClockSeconds);
        Assert.True(stop.OnTimer(160).MayDepart);
    }

    [Fact]
    public void MissedStationCompletesTaskAsUnsuccessful()
    {
        var stop = new ActivityPassengerStopSession(100, 160, 30);

        Assert.False(stop.OnMissedStation(200).IsCompleted);
    }
    [Fact]
    public void SignalBlockIsReevaluatedWhenSignalClearsWithoutPermissionChange()
    {
        var stop = new ActivityPassengerStopSession(100, 130, 30);
        stop.OnTrainStop(100, isAtStation: true);

        var blocked = stop.OnTimer(130, distanceToNextSignalMeters: 299,
            nextSignalIsStop: true, signalPermissionGranted: false);
        Assert.False(blocked.MayDepart);

        var cleared = stop.OnTimer(130, distanceToNextSignalMeters: 301,
            nextSignalIsStop: true, signalPermissionGranted: false);
        Assert.True(cleared.MayDepart);
        Assert.Contains("You may depart", cleared.DisplayMessage);
    }

    [Fact]
    public void RepeatedTrainStopNotificationsDoNotResetBoardingWindow()
    {
        var stop = new ActivityPassengerStopSession(100, 160, 30);
        var first = stop.OnTrainStop(100, isAtStation: true);
        var duplicate = stop.OnTrainStop(125, isAtStation: true);

        Assert.Equal(first.BoardingEndClockSeconds, duplicate.BoardingEndClockSeconds);
        Assert.Equal(100, duplicate.ActualArrivalSeconds);
        Assert.Equal(35, stop.OnTimer(125).RemainingSeconds);
    }

}

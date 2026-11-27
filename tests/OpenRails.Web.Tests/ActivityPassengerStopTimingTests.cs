using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class ActivityPassengerStopTimingTests
{
    [Fact]
    public void KeepsScheduledDwellWhenTrainArrivesOnTime()
    {
        var result = ActivityPassengerStopTiming.Calculate(3600, 3780, 3600, 60);

        Assert.Equal(180, result.PlannedBoardingSeconds);
        Assert.Equal(180, result.BoardingDurationSeconds);
        Assert.Equal(3780, result.BoardingEndClockSeconds);
    }

    [Fact]
    public void KeepsTightPlannedDwellBelowPlatformMinimum()
    {
        var result = ActivityPassengerStopTiming.Calculate(3600, 3630, 3600, 60);

        Assert.Equal(30, result.BoardingDurationSeconds);
        Assert.Equal(3630, result.BoardingEndClockSeconds);
    }

    [Fact]
    public void PreservesPlatformMinimumAfterLateArrivalOnGenerousSchedule()
    {
        var result = ActivityPassengerStopTiming.Calculate(3600, 3780, 3750, 60);

        Assert.Equal(30, result.PunctualBoardingSeconds);
        Assert.Equal(60, result.BoardingDurationSeconds);
        Assert.Equal(3810, result.BoardingEndClockSeconds);
    }

    [Fact]
    public void UsesPlatformMinimumWhenScheduleHasNoPositiveDwell()
    {
        var result = ActivityPassengerStopTiming.Calculate(3600, 3600, 3600, 45);

        Assert.Equal(45, result.BoardingDurationSeconds);
        Assert.Equal(3645, result.BoardingEndClockSeconds);
    }

    [Fact]
    public void CalculatesNativeCeilingCountdownAndDeparturePermission()
    {
        var result = ActivityPassengerStopTiming.Calculate(100, 160, 100, 30);

        Assert.Equal(1, result.RemainingSeconds(159.2));
        Assert.False(result.MayDepart(159.2));
        Assert.Equal(0, result.RemainingSeconds(160));
        Assert.True(result.MayDepart(160));
    }

    [Fact]
    public void RejectsNonFiniteTimesAndNegativePlatformMinimum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ActivityPassengerStopTiming.Calculate(double.NaN, 10, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ActivityPassengerStopTiming.Calculate(0, 10, 0, -1));
    }
}

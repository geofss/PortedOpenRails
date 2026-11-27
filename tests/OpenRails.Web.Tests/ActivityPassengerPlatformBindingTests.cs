using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class ActivityPassengerPlatformBindingTests
{
    [Fact]
    public void ResolvesNativePlatformItemAndLinkedPlatformForEachScheduleStop()
    {
        var service = new ActivityPassengerServiceDefinition("Service", 36000,
            new[] { new ActivityPassengerStopSchedule(100, 130, 500, 0) });
        var database = CreateDatabase(
            Platform(0, linked: 1), Platform(1, linked: 0));

        var result = ActivityPassengerPlatformBinding.Bind(service, database);

        Assert.True(result.IsFullyBound);
        Assert.Equal(1, result.Stops.Count);
        Assert.Equal(0, result.Stops[0].PlatformStart.Index);
        Assert.Equal(1, result.Stops[0].PlatformLinked.Index);
    }

    [Fact]
    public void RejectsNonPlatformTrItem()
    {
        var service = Service(0);
        var database = CreateDatabase(Item(0, "signal", null));

        var result = ActivityPassengerPlatformBinding.Bind(service, database);

        Assert.False(result.IsFullyBound);
        Assert.Empty(result.Stops);
        Assert.Contains(result.Diagnostics, message => message.Contains("does not resolve to a platform"));
    }

    [Fact]
    public void RejectsMissingLinkedPlatformItem()
    {
        var service = Service(0);
        var database = CreateDatabase(Platform(0, linked: 5));

        var result = ActivityPassengerPlatformBinding.Bind(service, database);

        Assert.False(result.IsFullyBound);
        Assert.Contains(result.Diagnostics, message => message.Contains("linked platform"));
    }

    [Fact]
    public void RetainsValidStopsAndDiagnosesInvalidStopId()
    {
        var service = new ActivityPassengerServiceDefinition("Service", 0,
            new[] { new ActivityPassengerStopSchedule(0, 30, 0, 0),
                    new ActivityPassengerStopSchedule(60, 90, 100, 99) });
        var database = CreateDatabase(Platform(0, linked: 1), Platform(1, linked: 0));

        var result = ActivityPassengerPlatformBinding.Bind(service, database);

        Assert.Single(result.Stops);
        Assert.False(result.IsFullyBound);
        Assert.Contains(result.Diagnostics, message => message.Contains("outside the TDB"));
    }

    private static ActivityPassengerServiceDefinition Service(int platformId) =>
        new("Service", 0, new[] { new ActivityPassengerStopSchedule(0, 30, 0, platformId) });

    private static TrackDatabase CreateDatabase(params TrackItem?[] items) =>
        new(new TrackNode?[1], items, Array.Empty<string>());

    private static TrackItem Platform(int index, uint linked) =>
        Item(index, "platform", linked);

    private static TrackItem Item(int index, string type, uint? linked) =>
        new(index, type, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "", "", "", "", linked, 60, 0);
}

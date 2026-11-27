using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class AiTimetableDispatcherTests
{
    private static SignalInterlockingController CreateInterlocking() => new(
        new uint[] { 10, 11, 12 },
        new Dictionary<uint, IReadOnlyCollection<uint>> { [100] = new uint[] { 11, 12 } });

    [Fact]
    public void DispatchesServicesAtScheduledSimulationTime()
    {
        var interlocking = CreateInterlocking();
        var session = new AiServiceInterlockingSession("AI-TT-1", interlocking);
        var timetable = new AiTimetableDispatcher();
        timetable.AddService("Service-1", 30, session, new uint[] { 10, 11 },
            new Dictionary<uint, uint> { [100] = 11 });

        Assert.Empty(timetable.Advance(29));
        Assert.False(session.IsDispatched);
        Assert.Equal(new[] { "Service-1" }, timetable.Advance(1));
        Assert.True(session.IsDispatched);
        Assert.Empty(timetable.Advance(10));
    }

    [Fact]
    public void DispatchesServiceUsingParsedNativeTimetableStartTime()
    {
        var interlocking = CreateInterlocking();
        var session = new AiServiceInterlockingSession("AI-NATIVE-TT", interlocking);
        var timetable = new AiTimetableDispatcher();
        var nativeService = new NativeTimetableTrainDefinition(1, "Express")
        {
            Path = "Main",
            Consist = "Express.con",
            StartTime = "08:00"
        };

        timetable.AddService(nativeService, session, new uint[] { 10, 11 },
            new Dictionary<uint, uint> { [100] = 11 });

        Assert.Empty(timetable.Advance(8 * 60 * 60 - 1));
        Assert.Equal(new[] { "Express" }, timetable.Advance(1));
        Assert.True(session.IsDispatched);
    }

    [Fact]
    public void RejectsNativeTimetableServiceWithInvalidStartTime()
    {
        var timetable = new AiTimetableDispatcher();
        var session = new AiServiceInterlockingSession("AI-BAD-TT", CreateInterlocking());
        var nativeService = new NativeTimetableTrainDefinition(1, "Bad service")
        {
            StartTime = "not-a-time"
        };

        Assert.Throws<InvalidDataException>(() => timetable.AddService(nativeService,
            session, new uint[] { 10 }, new Dictionary<uint, uint>()));
    }

    [Fact]
    public void RetriesBlockedDepartureUntilInterlockingRouteBecomesAvailable()
    {
        var interlocking = CreateInterlocking();
        Assert.True(interlocking.TryReserveRoute("PLAYER", new uint[] { 11 },
            new Dictionary<uint, uint> { [100] = 11 }));
        var session = new AiServiceInterlockingSession("AI-TT-1", interlocking);
        var timetable = new AiTimetableDispatcher();
        timetable.AddService("Service-1", 0, session, new uint[] { 12 },
            new Dictionary<uint, uint> { [100] = 12 });

        Assert.Empty(timetable.Advance(0));
        Assert.Contains("Service-1", timetable.PendingServices());
        Assert.True(interlocking.TryReleaseRoute("PLAYER"));
        Assert.Equal(new[] { "Service-1" }, timetable.Advance(1));
        Assert.True(session.IsDispatched);
    }
}

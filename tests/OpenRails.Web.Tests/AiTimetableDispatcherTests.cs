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

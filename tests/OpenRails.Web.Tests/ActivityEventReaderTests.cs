using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class ActivityEventReaderTests
{
    [Fact]
    public void RejectsMissingOutcomeReferencesBeforeSessionCanMutate()
    {
        var definition = new ActivityEventSet("activity.act", new[]
        {
            new ActivityEventDefinition(1, "First", ActivityEventKind.Time,
                1, 0, true, 0, 0, 0, 0, 0, 0, "", "", "",
                new ActivityEventOutcomes(new[] { 99 }, Array.Empty<int>(),
                    Array.Empty<int>(), Array.Empty<int>(), false, null, null))
        });

        var error = Assert.Throws<InvalidDataException>(() =>
            new ActivityExecutionSession(definition));

        Assert.Contains("event 1", error.Message);
        Assert.Contains("missing event ID 99", error.Message);
    }

    [Fact]
    public void ValidatesEveryOutcomeReferenceEvenWhenEarlierReferencesExist()
    {
        var definition = new ActivityEventSet("activity.act", new[]
        {
            new ActivityEventDefinition(1, "First", ActivityEventKind.Time,
                1, 0, true, 0, 0, 0, 0, 0, 0, "", "", "",
                new ActivityEventOutcomes(new[] { 2 }, new[] { 2 },
                    new[] { 2 }, new[] { 404 }, false, null, null)),
            new ActivityEventDefinition(2, "Second", ActivityEventKind.Time,
                0, 10, true, 0, 0, 0, 0, 0, 0, "", "", "", ActivityEventOutcomes.Empty)
        });

        var error = Assert.Throws<InvalidDataException>(() =>
            new ActivityExecutionSession(definition));

        Assert.Contains("missing event ID 404", error.Message);
    }

    [Fact]
    public void ReversibleActionCanTriggerAgainOnlyAfterConditionClears()
    {
        var definition = new ActivityEventSet("activity.act", new[]
        {
            new ActivityEventDefinition(50, "Reversible speed check", ActivityEventKind.Action,
                1, 0, true, 0, 0, 0, 0, 0, 10, "ReachSpeed", "", "",
                ActivityEventOutcomes.Empty, Reversible: true)
        });
        var session = new ActivityExecutionSession(definition);

        Assert.Equal(50, session.Advance(0, new ActivityPlayerState(12))?.EventId);
        session.Acknowledge();
        Assert.Null(session.Advance(0, new ActivityPlayerState(12)));
        Assert.Null(session.Advance(0, new ActivityPlayerState(0)));
        Assert.Equal(50, session.Advance(0, new ActivityPlayerState(12))?.EventId);
    }

    [Fact]
    public void ReadsBothNativeSpellingsOfReversibleActionFlag()
    {
        using var store = CreateStore();
        var events = new ActivityEventReader().Read(store, "ROUTES/SCE/ACTIVITIES/Events.act");

        Assert.True(events.Events.Single(item => item.Id == 50).Reversible);
        Assert.True(events.Events.Single(item => item.Id == 51).Reversible);
    }

    [Fact]
    public void AllStopsActionWaitsForEveryNativePathWaitingPoint()
    {
        var definition = new ActivityEventSet("activity.act", new[]
        {
            new ActivityEventDefinition(4, "All stations", ActivityEventKind.Action,
                1, 0, true, 0, 0, 0, 0, 0, 0, "AllStops", "", "", ActivityEventOutcomes.Empty)
        });
        var session = new ActivityExecutionSession(definition);

        Assert.Empty(session.UnsupportedEvents);
        Assert.Null(session.Advance(1, new ActivityPlayerState(0, AllStopsCompleted: false)));
        var triggered = session.Advance(1, new ActivityPlayerState(0, AllStopsCompleted: true));

        Assert.NotNull(triggered);
        Assert.Equal(4, triggered.EventId);
    }

    [Fact]
    public void ReadsNativeTimeEventsAndActivationOutcomes()
    {
        using var store = CreateStore();
        var events = new ActivityEventReader().Read(store, "ROUTES/SCE/ACTIVITIES/Events.act");
        Assert.Equal(6, events.Events.Count);
        var start = events.Events.Single(item => item.Id == 10);
        Assert.Equal(ActivityEventKind.Time, start.Kind);
        Assert.Equal(300, start.TriggerSeconds);
        Assert.Contains(20, start.Outcomes.ActivateEventIds);
        Assert.Equal("Station call", events.Events.Single(item => item.Id == 20).Name);
    }

    [Fact]
    public void ExecutesTimeEventsAndAppliesActivationGraph()
    {
        using var store = CreateStore();
        var session = new ActivityExecutionSession(
            new ActivityEventReader().Read(store, "ROUTES/SCE/ACTIVITIES/Events.act"));
        Assert.Null(session.Advance(299, new ActivityPlayerState(0)));
        Assert.Equal(10, session.Advance(1, new ActivityPlayerState(0))?.EventId);
        session.Acknowledge();
        Assert.Null(session.Advance(299, new ActivityPlayerState(0)));
        Assert.Equal(20, session.Advance(1, new ActivityPlayerState(0))?.EventId);
        Assert.True(session.Acknowledge()?.ActivityEnded);
    }

    [Fact]
    public void ReadsAndExecutesNativeAllStopsActionEvent()
    {
        using var store = CreateStore();
        var events = new ActivityEventReader().Read(store, "ROUTES/SCE/ACTIVITIES/Events.act");
        var allStops = Assert.Single(events.Events.Where(item => item.Id == 40));
        Assert.Equal("AllStops", allStops.ActionType);
        var session = new ActivityExecutionSession(events);

        Assert.Null(session.Advance(0, new ActivityPlayerState(0, AllStopsCompleted: false)));
        Assert.Equal(40, session.Advance(0, new ActivityPlayerState(0, AllStopsCompleted: true))?.EventId);
    }

    [Fact]
    public void ReachSpeedActionTriggersAtNativeSpeedThreshold()
    {
        using var store = CreateStore();
        var session = new ActivityExecutionSession(
            new ActivityEventReader().Read(store, "ROUTES/SCE/ACTIVITIES/Events.act"));
        Assert.Equal(30, session.Advance(0, new ActivityPlayerState(15))?.EventId);
    }

    private static ZipContentStore CreateStore()
    {
        const string content = """
            SIMISA@@@@@@@@@@JINX0t1t______
            Tr_Activity (
              Tr_Activity_Header ( RouteID ( SCE ) Name ( Events ) )
              Tr_Activity_File (
                Events (
                  EventCategoryTime (
                    EventTypeTime ( )
                    ID ( 10 ) Name ( "Start announcement" ) Activation_Level ( 1 )
                    Time ( 300 )
                    Outcomes ( ActivateEvent ( 20 ) DisplayMessage ( "Next event enabled" ) )
                  )
                  EventCategoryTime (
                    ID ( 20 ) Name ( "Station call" ) Activation_Level ( 0 )
                    Time ( 600 )
                    Outcomes ( ActivitySuccess ( ) DisplayMessage ( "Activity complete" ) )
                  )
                  EventCategoryLocation (
                    EventTypeLocation ( )
                    ID ( 60 ) Name ( "Location event" ) Activation_Level ( 0 )
                    TriggerOnStop ( 1 ) Location ( 1 2 100 200 25 )
                    Outcomes ( )
                  )
                  EventCategoryAction (
                    ID ( 30 ) Name ( "Reach speed" ) Activation_Level ( 1 )
                    EventTypeReachSpeed ( ) Speed ( 15 )
                    Outcomes ( )
                  )
                  EventCategoryAction (
                    ID ( 40 ) Name ( "All stops" ) Activation_Level ( 1 )
                    EventTypeAllStops ( )
                    Outcomes ( )
                  )
                  EventCategoryAction (
                    ID ( 50 ) Name ( "Reversible misspelling" ) Activation_Level ( 1 )
                    EventTypeReachSpeed ( ) Speed ( 10 ) Reversable_Event ( )
                    Outcomes ( )
                  )
                  EventCategoryAction (
                    ID ( 51 ) Name ( "Reversible spelling" ) Activation_Level ( 1 )
                    EventTypeReachSpeed ( ) Speed ( 10 ) Reversible_Event ( )
                    Outcomes ( )
                  )
                )
              )
            )
            """;
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("ROUTES/SCE/ACTIVITIES/Events.act");
            using var stream = entry.Open();
            stream.Write(Encoding.UTF8.GetBytes(content));
        }
        memory.Position = 0;
        return new ZipContentStore(memory);
    }
}

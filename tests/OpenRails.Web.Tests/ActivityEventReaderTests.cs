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
    public void NativeNonReversibleEventsRemainOneShotAfterAnotherOutcomeReactivatesThem()
    {
        var definition = new ActivityEventSet("activity.act", new[]
        {
            new ActivityEventDefinition(1, "First", ActivityEventKind.Time,
                1, 0, true, 0, 0, 0, 0, 0, 0, "", "", "",
                new ActivityEventOutcomes(new[] { 2 }, Array.Empty<int>(),
                    Array.Empty<int>(), Array.Empty<int>(), false, null, null)),
            new ActivityEventDefinition(2, "Second", ActivityEventKind.Time,
                0, 0, true, 0, 0, 0, 0, 0, 0, "", "", "",
                new ActivityEventOutcomes(new[] { 1 }, Array.Empty<int>(),
                    Array.Empty<int>(), Array.Empty<int>(), false, null, null))
        });
        var session = new ActivityExecutionSession(definition);

        Assert.Equal(1, session.Advance(0, new ActivityPlayerState(0))?.EventId);
        session.Acknowledge();
        Assert.Equal(2, session.Advance(0, new ActivityPlayerState(0))?.EventId);
        session.Acknowledge();
        Assert.Null(session.Advance(0, new ActivityPlayerState(0)));
    }

    [Fact]
    public void NativeReversibleEventsApplyOutcomesOnlyOnFirstTrigger()
    {
        var definition = new ActivityEventSet("activity.act", new[]
        {
            new ActivityEventDefinition(1, "Reversible", ActivityEventKind.Action,
                1, 0, true, 0, 0, 0, 0, 0, 10, "ReachSpeed", "", "",
                new ActivityEventOutcomes(new[] { 2 }, Array.Empty<int>(),
                    Array.Empty<int>(), Array.Empty<int>(), false, null, null), Reversible: true),
            new ActivityEventDefinition(2, "Activated once", ActivityEventKind.Time,
                0, 0, true, 0, 0, 0, 0, 0, 0, "", "", "", ActivityEventOutcomes.Empty)
        });
        var session = new ActivityExecutionSession(definition);

        Assert.Equal(1, session.Advance(0, new ActivityPlayerState(12))?.EventId);
        session.Acknowledge();
        Assert.Equal(2, session.Advance(0, new ActivityPlayerState(12))?.EventId);
        session.Acknowledge();
        Assert.Null(session.Advance(0, new ActivityPlayerState(0)));
        Assert.Equal(1, session.Advance(0, new ActivityPlayerState(12))?.EventId);
        session.Acknowledge();
        Assert.Null(session.Advance(0, new ActivityPlayerState(12)));
    }

    [Fact]
    public void ReadsNativeWagonWorkOrderIdsAndSidingReferences()
    {
        using var store = CreateStore();
        var action = Assert.Single(new ActivityEventReader()
            .Read(store, "ROUTES/SCE/ACTIVITIES/Events.act").Events
            .Where(item => item.Id == 70));

        Assert.Equal(6u, action.SidingItemId);
        var wagon = Assert.Single(action.WagonRequirements!);
        Assert.Equal(65538u, wagon.UniqueId);
        Assert.Equal(5u, wagon.SidingItemId);
        Assert.Equal("1 - 2", wagon.CarId);
        Assert.Equal("Brake van", wagon.Description);
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
    public void ReadsNativePickUpPassengersActionAndKeepsItsIntentionalNoOpSemantics()
    {
        using var store = CreateStore();
        var events = new ActivityEventReader().Read(store, "ROUTES/SCE/ACTIVITIES/Events.act");
        var action = Assert.Single(events.Events, item => item.Id == 52);

        Assert.Equal("PickUpPassengers", action.ActionType);
        Assert.DoesNotContain(new ActivityExecutionSession(events).UnsupportedEvents,
            item => item.Contains("52", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadsNativeTimeEventsAndActivationOutcomes()
    {
        using var store = CreateStore();
        var events = new ActivityEventReader().Read(store, "ROUTES/SCE/ACTIVITIES/Events.act");
        Assert.Equal(9, events.Events.Count);
        var start = events.Events.Single(item => item.Id == 10);
        Assert.Equal(ActivityEventKind.Time, start.Kind);
        Assert.Equal(300, start.TriggerSeconds);
        Assert.Contains(20, start.Outcomes.ActivateEventIds);
        Assert.Equal("Station call", events.Events.Single(item => item.Id == 20).Name);
    }

    [Fact]
    public void NativeAllStopsUsesLastPassengerTaskCompletionPresence()
    {
        var session = new ActivityExecutionSession(new ActivityEventSet("activity.act", new[]
        {
            new ActivityEventDefinition(84, "All passenger stops", ActivityEventKind.Action,
                1, 0, true, 0, 0, 0, 0, 0, 0, "AllStops", "", "",
                ActivityEventOutcomes.Empty)
        }));

        Assert.Null(session.Advance(0, new ActivityPlayerState(0,
            AllStopsCompleted: true, PassengerTaskCount: 2,
            LastPassengerTaskCompleted: null)));
        Assert.Equal(84, session.Advance(0, new ActivityPlayerState(0,
            PassengerTaskCount: 2, LastPassengerTaskCompleted: false))?.EventId);
    }

    [Fact]
    public void NativeAssembleTrainMatchesExactConsistInEitherDirection()
    {
        var session = new ActivityExecutionSession(new ActivityEventSet("activity.act", new[]
        {
            WagonAction(80, "AssembleTrain", new[] { "1 - 2", "1 - 3" })
        }));

        Assert.Equal(80, session.Advance(0, new ActivityPlayerState(0,
            OtherTrains: new[] { new ActivityTrainState(new[] { "1 - 3", "1 - 2" }, false, false) }))?.EventId);
    }

    [Fact]
    public void NativeAssembleTrainAtLocationRequiresPlayerTrainInsideSiding()
    {
        var session = new ActivityExecutionSession(new ActivityEventSet("activity.act", new[]
        {
            WagonAction(81, "AssembleTrainAtLocation", new[] { "1 - 2" })
        }));
        var consists = new[] { new ActivityTrainState(new[] { "1 - 2" }, false, false) };

        Assert.Null(session.Advance(0, new ActivityPlayerState(0, OtherTrains: consists)));
        Assert.Equal(81, session.Advance(0, new ActivityPlayerState(0,
            OtherTrains: consists, PlayerTrainAtSiding: true))?.EventId);
    }

    [Fact]
    public void NativeDropOffRequiresListedWagonsInStaticConsistAtSiding()
    {
        var session = new ActivityExecutionSession(new ActivityEventSet("activity.act", new[]
        {
            WagonAction(82, "DropOffWagonsAtLocation", new[] { "1 - 2", "1 - 3" })
        }));

        Assert.Null(session.Advance(0, new ActivityPlayerState(0, OtherTrains: new[]
        {
            new ActivityTrainState(new[] { "1 - 2", "1 - 3" }, false, true)
        })));
        Assert.Equal(82, session.Advance(0, new ActivityPlayerState(0, OtherTrains: new[]
        {
            new ActivityTrainState(new[] { "1 - 9", "1 - 2", "1 - 3" }, true, true)
        }))?.EventId);
    }

    [Fact]
    public void NativePickUpPassengersActionIsRecognizedAsNativeNoOp()
    {
        var session = new ActivityExecutionSession(new ActivityEventSet("activity.act", new[]
        {
            new ActivityEventDefinition(85, "Pick up passengers", ActivityEventKind.Action,
                1, 0, true, 0, 0, 0, 0, 0, 0, "PickUpPassengers", "", "",
                ActivityEventOutcomes.Empty)
        }));

        // Open Rails' EventCategoryActionWrapper has an intentionally empty
        // PickUpPassengers case; it is recognized, but does not trigger.
        Assert.Empty(session.UnsupportedEvents);
        Assert.Null(session.Advance(0, new ActivityPlayerState(0,
            PassengerTaskCount: 1, LastPassengerTaskCompleted: true)));
    }

    [Fact]
    public void NativePickUpWagonsRequiresEveryListedCarAndLowSpeed()
    {
        var session = new ActivityExecutionSession(new ActivityEventSet("activity.act", new[]
        {
            WagonAction(83, "PickUpWagons", new[] { "1 - 2", "1 - 3" })
        }));

        Assert.Null(session.Advance(0, new ActivityPlayerState(1.1,
            PlayerConsistCarIds: new[] { "1 - 2", "1 - 3" })));
        Assert.Null(session.Advance(0, new ActivityPlayerState(0,
            PlayerConsistCarIds: new[] { "1 - 2" })));
        Assert.Equal(83, session.Advance(0, new ActivityPlayerState(1,
            PlayerConsistCarIds: new[] { "1 - 9", "1 - 2", "1 - 3" }))?.EventId);
    }

    private static ActivityEventDefinition WagonAction(int id, string action,
        IReadOnlyList<string> carIds) => new(
        id, action, ActivityEventKind.Action, 1, 0, true, 0, 0, 0, 0, 0, 0,
        action, "", "", ActivityEventOutcomes.Empty,
        WagonRequirements: carIds.Select((carId, index) =>
            new ActivityWagonRequirement((uint)(index + 1), (uint)(index + 10), "", carId)).ToArray());

    [Fact]
    public void NativeActivityCompletionTakesEffectWhenEventTriggersNotWhenWindowIsAcknowledged()
    {
        var definition = new ActivityEventSet("activity.act", new[]
        {
            new ActivityEventDefinition(9, "Finish", ActivityEventKind.Time,
                1, 0, true, 0, 0, 0, 0, 0, 0, "", "", "",
                new ActivityEventOutcomes(Array.Empty<int>(), Array.Empty<int>(),
                    Array.Empty<int>(), Array.Empty<int>(), true, null, "Completed"))
        });
        var session = new ActivityExecutionSession(definition);

        var pending = session.Advance(0, new ActivityPlayerState(0));

        Assert.True(session.IsEnded);
        Assert.True(pending?.ActivityEnded);
        Assert.True(pending?.ActivitySucceeded);
        Assert.Equal("Completed", pending?.Message);
        Assert.NotNull(session.PendingEvent);
        Assert.Equal(pending, session.Acknowledge());
        Assert.Null(session.PendingEvent);
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
                    ID ( 70 ) Name ( "Wagon work order" ) Activation_Level ( 0 )
                    EventTypePickUpWagons ( )
                    Wagon_List ( UID ( 65538 ) Description ( "Brake van" ) SidingItem ( 5 ) )
                    SidingItem ( 6 ) Outcomes ( )
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
                  EventCategoryAction (
                    ID ( 52 ) Name ( "Pick up passengers" ) Activation_Level ( 1 )
                    EventTypePickUpPassengers ( )
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

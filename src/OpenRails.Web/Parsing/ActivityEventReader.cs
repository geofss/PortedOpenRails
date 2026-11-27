using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

public sealed class ActivityEventReader
{
    public ActivityEventSet Read(IVirtualContentStore store, string path)
    {
        ArgumentNullException.ThrowIfNull(store);
        var virtualPath = VirtualPath.Normalize(path);
        lock (StfReaderAccess.SyncRoot)
        {
            STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(store);
            using var reader = new STFReader(virtualPath, useTree: false);
            ActivityEventSet? result = null;
            reader.ParseFile(new[]
            {
                new STFReader.TokenProcessor("tr_activity", () =>
                {
                    reader.MustMatch("(");
                    reader.ParseBlock(new[]
                    {
                        new STFReader.TokenProcessor("tr_activity_file", () =>
                        {
                            reader.MustMatch("(");
                            reader.ParseBlock(new[]
                            {
                                new STFReader.TokenProcessor("events", () => result = ReadEvents(reader, virtualPath))
                            });
                        })
                    });
                })
            });
            return result ?? new ActivityEventSet(virtualPath, Array.Empty<ActivityEventDefinition>());
        }
    }

    private static ActivityEventSet ReadEvents(STFReader reader, string sourcePath)
    {
        var events = new List<ActivityEventDefinition>();
        reader.MustMatch("(");
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("eventcategorytime", () => events.Add(ReadTimeEvent(reader))),
            new STFReader.TokenProcessor("eventcategorylocation", () => events.Add(ReadLocationEvent(reader))),
            new STFReader.TokenProcessor("eventcategoryaction", () => events.Add(ReadActionEvent(reader)))
        });
        return new ActivityEventSet(sourcePath, Array.AsReadOnly(events.ToArray()));
    }

    private static ActivityEventDefinition ReadTimeEvent(STFReader reader)
    {
        var value = new MutableEvent(ActivityEventKind.Time);
        reader.MustMatch("(");
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("id", () => value.Id = reader.ReadIntBlock(-1)),
            new STFReader.TokenProcessor("name", () => value.Name = reader.ReadStringBlock("")),
            new STFReader.TokenProcessor("activation_level", () => value.ActivationLevel = reader.ReadIntBlock(0)),
            new STFReader.TokenProcessor("time", () => value.TriggerSeconds = reader.ReadFloatBlock(STFReader.UNITS.Time, 0)),
            new STFReader.TokenProcessor("outcomes", () => value.Outcomes = ReadOutcomes(reader)),
            new STFReader.TokenProcessor("texttodisplayoncompletioniftriggered", () => value.SuccessMessage = reader.ReadStringBlock("")),
            new STFReader.TokenProcessor("texttodisplayoncompletionifnottriggered", () => value.FailureMessage = reader.ReadStringBlock(""))
        });
        return value.ToDefinition();
    }

    private static ActivityEventDefinition ReadLocationEvent(STFReader reader)
    {
        var value = new MutableEvent(ActivityEventKind.Location);
        reader.MustMatch("(");
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("id", () => value.Id = reader.ReadIntBlock(-1)),
            new STFReader.TokenProcessor("name", () => value.Name = reader.ReadStringBlock("")),
            new STFReader.TokenProcessor("activation_level", () => value.ActivationLevel = reader.ReadIntBlock(0)),
            new STFReader.TokenProcessor("triggeronstop", () => value.TriggerOnStop = reader.ReadBoolBlock(true)),
            new STFReader.TokenProcessor("location", () =>
            {
                reader.MustMatch("(");
                value.TileX = reader.ReadInt(null);
                value.TileZ = reader.ReadInt(null);
                value.X = reader.ReadFloat(STFReader.UNITS.None, null);
                value.Z = reader.ReadFloat(STFReader.UNITS.None, null);
                value.RadiusMeters = reader.ReadFloat(STFReader.UNITS.Distance, null);
                reader.MustMatch(")");
            }),
            new STFReader.TokenProcessor("outcomes", () => value.Outcomes = ReadOutcomes(reader)),
            new STFReader.TokenProcessor("texttodisplayoncompletioniftriggered", () => value.SuccessMessage = reader.ReadStringBlock("")),
            new STFReader.TokenProcessor("texttodisplayoncompletionifnottriggered", () => value.FailureMessage = reader.ReadStringBlock(""))
        });
        return value.ToDefinition();
    }

    private static ActivityEventDefinition ReadActionEvent(STFReader reader)
    {
        var value = new MutableEvent(ActivityEventKind.Action);
        reader.MustMatch("(");
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("id", () => value.Id = reader.ReadIntBlock(-1)),
            new STFReader.TokenProcessor("name", () => value.Name = reader.ReadStringBlock("")),
            new STFReader.TokenProcessor("activation_level", () => value.ActivationLevel = reader.ReadIntBlock(0)),
            new STFReader.TokenProcessor("speed", () => value.TriggerSpeedMetersPerSecond = reader.ReadFloatBlock(STFReader.UNITS.Speed, 0)),
            new STFReader.TokenProcessor("eventtypereachspeed", () => { reader.MustMatch("("); reader.MustMatch(")"); value.ActionType = "ReachSpeed"; }),
            new STFReader.TokenProcessor("eventtypeallstops", () => { reader.MustMatch("("); reader.MustMatch(")"); value.ActionType = "AllStops"; }),
            new STFReader.TokenProcessor("eventtypeassembletrain", () => { reader.MustMatch("("); reader.MustMatch(")"); value.ActionType = "AssembleTrain"; }),
            new STFReader.TokenProcessor("eventtypeassembletrainatlocation", () => { reader.MustMatch("("); reader.MustMatch(")"); value.ActionType = "AssembleTrainAtLocation"; }),
            new STFReader.TokenProcessor("eventtypedropoffwagonsatlocation", () => { reader.MustMatch("("); reader.MustMatch(")"); value.ActionType = "DropOffWagonsAtLocation"; }),
            new STFReader.TokenProcessor("eventtypepickuppassengers", () => { reader.MustMatch("("); reader.MustMatch(")"); value.ActionType = "PickUpPassengers"; }),
            new STFReader.TokenProcessor("eventtypepickupwagons", () => { reader.MustMatch("("); reader.MustMatch(")"); value.ActionType = "PickUpWagons"; }),
            new STFReader.TokenProcessor("outcomes", () => value.Outcomes = ReadOutcomes(reader)),
            new STFReader.TokenProcessor("texttodisplayoncompletioniftriggered", () => value.SuccessMessage = reader.ReadStringBlock("")),
            new STFReader.TokenProcessor("texttodisplayoncompletionifnottriggered", () => value.FailureMessage = reader.ReadStringBlock(""))
        });
        return value.ToDefinition();
    }

    private static ActivityEventOutcomes ReadOutcomes(STFReader reader)
    {
        var activate = new List<int>();
        var restore = new List<int>();
        var decrement = new List<int>();
        var increment = new List<int>();
        var success = false;
        string? failure = null;
        string? message = null;
        reader.MustMatch("(");
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("activitysuccess", () => { reader.MustMatch("("); reader.MustMatch(")"); success = true; }),
            new STFReader.TokenProcessor("activityfail", () => failure = reader.ReadStringBlock("")),
            new STFReader.TokenProcessor("activateevent", () => activate.Add(reader.ReadIntBlock(-1))),
            new STFReader.TokenProcessor("restoreactlevel", () => restore.Add(reader.ReadIntBlock(-1))),
            new STFReader.TokenProcessor("decactlevel", () => decrement.Add(reader.ReadIntBlock(-1))),
            new STFReader.TokenProcessor("incactlevel", () => increment.Add(reader.ReadIntBlock(-1))),
            new STFReader.TokenProcessor("displaymessage", () => message = reader.ReadStringBlock(""))
        });
        return new ActivityEventOutcomes(activate, restore, decrement, increment, success, failure, message);
    }

    private sealed class MutableEvent(ActivityEventKind kind)
    {
        public int Id = -1;
        public string Name = "";
        public int ActivationLevel;
        public double TriggerSeconds;
        public bool TriggerOnStop = true;
        public int TileX, TileZ;
        public float X, Z, RadiusMeters;
        public float TriggerSpeedMetersPerSecond;
        public string ActionType = "";
        public string SuccessMessage = "";
        public string FailureMessage = "";
        public ActivityEventOutcomes Outcomes = ActivityEventOutcomes.Empty;
        public ActivityEventDefinition ToDefinition() => new(
            Id, Name, kind, ActivationLevel, TriggerSeconds, TriggerOnStop,
            TileX, TileZ, X, Z, RadiusMeters, TriggerSpeedMetersPerSecond,
            ActionType, SuccessMessage, FailureMessage, Outcomes);
    }
}

public enum ActivityEventKind { Time, Location, Action }

public sealed record ActivityEventOutcomes(
    IReadOnlyList<int> ActivateEventIds,
    IReadOnlyList<int> RestoreActivationLevelEventIds,
    IReadOnlyList<int> DecrementActivationLevelEventIds,
    IReadOnlyList<int> IncrementActivationLevelEventIds,
    bool ActivitySuccess,
    string? ActivityFailure,
    string? DisplayMessage)
{
    public static ActivityEventOutcomes Empty { get; } =
        new(Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), false, null, null);
}

public sealed record ActivityEventDefinition(
    int Id, string Name, ActivityEventKind Kind, int InitialActivationLevel,
    double TriggerSeconds, bool TriggerOnStop, int TileX, int TileZ, float X, float Z,
    float RadiusMeters, float TriggerSpeedMetersPerSecond, string ActionType,
    string SuccessMessage, string FailureMessage, ActivityEventOutcomes Outcomes);

public sealed record ActivityEventSet(string SourcePath, IReadOnlyList<ActivityEventDefinition> Events);
public sealed record ActivityPlayerState(double SpeedMetersPerSecond, int? TileX = null,
    int? TileZ = null, double? X = null, double? Z = null,
    bool StationStopReached = false, bool AllStopsCompleted = false);

public sealed record ActivityEventExecutionResult(int EventId, string EventName,
    bool Triggered, string Message, bool ActivityEnded, bool ActivitySucceeded);

public sealed class ActivityExecutionSession
{
    private readonly List<RuntimeEvent> events;
    private ActivityEventExecutionResult? pending;
    private bool ended;

    public double ElapsedSeconds { get; private set; }
    public bool IsEnded => ended;
    public ActivityEventExecutionResult? PendingEvent => pending;
    public ActivityEventExecutionResult? LastAcknowledgedEvent { get; private set; }
    public IReadOnlyList<string> UnsupportedEvents { get; }

    public ActivityExecutionSession(ActivityEventSet definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        events = definition.Events.Select(item => new RuntimeEvent(item)).ToList();
        if (events.Select(item => item.Definition.Id).Distinct().Count() != events.Count)
            throw new InvalidDataException("Activity event IDs must be unique.");

        // Validate the complete outcome graph before execution begins. Applying an
        // outcome must never partially mutate activation levels and then fail because
        // a later reference points to a missing event.
        var eventIds = events.Select(item => item.Definition.Id).ToHashSet();
        foreach (var item in events)
        {
            var outcomes = item.Definition.Outcomes ??
                throw new InvalidDataException($"Activity event {item.Definition.Id} has no outcomes block.");
            var references = outcomes.ActivateEventIds
                .Concat(outcomes.RestoreActivationLevelEventIds)
                .Concat(outcomes.DecrementActivationLevelEventIds)
                .Concat(outcomes.IncrementActivationLevelEventIds);
            foreach (var targetId in references)
            {
                if (!eventIds.Contains(targetId))
                    throw new InvalidDataException(
                        $"Activity event {item.Definition.Id} outcome references missing event ID {targetId}.");
            }
        }

        UnsupportedEvents = Array.AsReadOnly(events
            .Where(item => item.Definition.Kind == ActivityEventKind.Action &&
                item.Definition.ActionType is not ("ReachSpeed" or "AllStops"))
            .Select(item => $"Event {item.Definition.Id} ({item.Definition.ActionType}): station or service action integration is required.")
            .ToArray());
    }

    public ActivityEventExecutionResult? Advance(double elapsedSeconds, ActivityPlayerState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0 ||
            !double.IsFinite(player.SpeedMetersPerSecond))
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (ended || pending is not null)
            return pending;
        ElapsedSeconds += elapsedSeconds;

        foreach (var item in events)
        {
            if (item.ActivationLevel <= 0 || item.Triggered || !IsTriggered(item.Definition, player))
                continue;
            item.Triggered = true;
            pending = new ActivityEventExecutionResult(item.Definition.Id, item.Definition.Name,
                true, item.Definition.Outcomes.DisplayMessage ?? item.Definition.SuccessMessage, false, false);
            return pending;
        }
        return null;
    }

    public ActivityEventExecutionResult? Acknowledge()
    {
        if (pending is null || ended)
            return pending;
        var current = events.First(item => item.Definition.Id == pending.EventId);
        var outcomes = current.Definition.Outcomes;
        if (!outcomes.ActivitySuccess && string.IsNullOrWhiteSpace(outcomes.ActivityFailure))
            current.ActivationLevel = 0;

        foreach (var id in outcomes.ActivateEventIds) Find(id).ActivationLevel = 1;
        foreach (var id in outcomes.RestoreActivationLevelEventIds)
        {
            var target = Find(id);
            target.ActivationLevel = target.Definition.InitialActivationLevel;
        }
        foreach (var id in outcomes.DecrementActivationLevelEventIds) Find(id).ActivationLevel--;
        foreach (var id in outcomes.IncrementActivationLevelEventIds) Find(id).ActivationLevel++;

        ended = outcomes.ActivitySuccess || !string.IsNullOrWhiteSpace(outcomes.ActivityFailure);
        var acknowledged = pending with
        {
            Message = outcomes.ActivityFailure ?? pending.Message,
            ActivityEnded = ended,
            ActivitySucceeded = outcomes.ActivitySuccess
        };
        LastAcknowledgedEvent = acknowledged;
        pending = null;
        return acknowledged;
    }

    private RuntimeEvent Find(int id) => events.FirstOrDefault(item => item.Definition.Id == id) ??
        throw new InvalidDataException($"Activity outcome references missing event ID {id}.");

    private bool IsTriggered(ActivityEventDefinition item, ActivityPlayerState player) => item.Kind switch
    {
        ActivityEventKind.Time => ElapsedSeconds >= item.TriggerSeconds,
        ActivityEventKind.Location => player.TileX == item.TileX && player.TileZ == item.TileZ &&
            player.X.HasValue && player.Z.HasValue &&
            (!item.TriggerOnStop || Math.Abs(player.SpeedMetersPerSecond) <= 0.032) &&
            Math.Sqrt(Math.Pow(player.X.Value - item.X, 2) + Math.Pow(player.Z.Value - item.Z, 2)) <= item.RadiusMeters,
        ActivityEventKind.Action when item.ActionType == "ReachSpeed" =>
            Math.Abs(player.SpeedMetersPerSecond) >= item.TriggerSpeedMetersPerSecond,
        ActivityEventKind.Action when item.ActionType == "AllStops" => player.AllStopsCompleted,
        _ => false
    };

    private sealed class RuntimeEvent(ActivityEventDefinition definition)
    {
        public ActivityEventDefinition Definition { get; } = definition;
        public int ActivationLevel { get; set; } = definition.InitialActivationLevel;
        public bool Triggered { get; set; }
    }
}

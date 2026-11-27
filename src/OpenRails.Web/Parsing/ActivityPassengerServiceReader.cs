using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Reads the native Player_Service_Definition / Player_Traffic_Definition
/// timetable from an activity file, preserving Open Rails' rule that a stop
/// record is emitted when PlatformStartID is encountered.
/// </summary>
public sealed class ActivityPassengerServiceReader
{
    public ActivityPassengerServiceDefinition? Read(IVirtualContentStore store, string path)
    {
        ArgumentNullException.ThrowIfNull(store);
        var virtualPath = VirtualPath.Normalize(path);
        lock (StfReaderAccess.SyncRoot)
        {
            STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(store);
            using var reader = new STFReader(virtualPath, useTree: false);
            ActivityPassengerServiceDefinition? result = null;
            reader.ParseFile(new[]
            {
                new STFReader.TokenProcessor("tr_activity", () => result = ReadActivity(reader))
            });
            return result;
        }
    }

    private static ActivityPassengerServiceDefinition? ReadActivity(STFReader reader)
    {
        ActivityPassengerServiceDefinition? result = null;
        reader.MustMatch("(");
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("tr_activity_file", () => result = ReadActivityFile(reader))
        });
        return result;
    }

    private static ActivityPassengerServiceDefinition? ReadActivityFile(STFReader reader)
    {
        ActivityPassengerServiceDefinition? result = null;
        reader.MustMatch("(");
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("player_service_definition", () => result = ReadPlayerService(reader))
        });
        return result;
    }

    private static ActivityPassengerServiceDefinition ReadPlayerService(STFReader reader)
    {
        reader.MustMatch("(");
        var serviceName = reader.ReadString();
        ActivityPassengerServiceDefinition? result = null;
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("player_traffic_definition",
                () => result = ReadPlayerTraffic(reader, serviceName))
        });
        return result ?? new ActivityPassengerServiceDefinition(serviceName, 0, Array.Empty<ActivityPassengerStopSchedule>());
    }

    private static ActivityPassengerServiceDefinition ReadPlayerTraffic(STFReader reader, string serviceName)
    {
        reader.MustMatch("(");
        var startTimeSeconds = (int)reader.ReadFloat(STFReader.UNITS.Time, null);
        var arrivalSeconds = 0d;
        var departureSeconds = 0d;
        var distanceDownPathMeters = 0f;
        var skipCount = 0;
        var stops = new List<ActivityPassengerStopSchedule>();
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("arrivaltime",
                () => arrivalSeconds = reader.ReadFloatBlock(STFReader.UNITS.Time, 0f)),
            new STFReader.TokenProcessor("departtime",
                () => departureSeconds = reader.ReadFloatBlock(STFReader.UNITS.Time, 0f)),
            new STFReader.TokenProcessor("skipcount", () => skipCount = reader.ReadIntBlock(0)),
            new STFReader.TokenProcessor("distancedownpath",
                () => distanceDownPathMeters = reader.ReadFloatBlock(STFReader.UNITS.Distance, 0f)),
            new STFReader.TokenProcessor("platformstartid", () =>
            {
                var platformStartId = reader.ReadIntBlock(-1);
                if (platformStartId < 0)
                    throw new InvalidDataException("Activity passenger stop PlatformStartID must be non-negative.");
                if (!double.IsFinite(arrivalSeconds) || !double.IsFinite(departureSeconds) ||
                    !float.IsFinite(distanceDownPathMeters) || distanceDownPathMeters < 0)
                    throw new InvalidDataException("Activity passenger stop contains invalid timetable values.");
                // Native Player_Traffic_Item receives SkipCount but does not retain it.
                _ = skipCount;
                stops.Add(new ActivityPassengerStopSchedule(
                    arrivalSeconds, departureSeconds, distanceDownPathMeters, platformStartId));
            })
        });
        return new ActivityPassengerServiceDefinition(serviceName, startTimeSeconds, stops.AsReadOnly());
    }
}

public sealed record ActivityPassengerServiceDefinition(string ServiceName,
    int StartTimeSeconds, IReadOnlyList<ActivityPassengerStopSchedule> Stops);

public sealed record ActivityPassengerStopSchedule(double ArrivalSeconds,
    double DepartureSeconds, float DistanceDownPathMeters, int PlatformStartId);

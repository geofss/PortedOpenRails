using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

/// <summary>Reads single-player activity headers with the upstream Open Rails STFReader.</summary>
public sealed class ActivityMetadataReader
{
    public ActivityMetadata Read(IVirtualContentStore store, string path)
    {
        ArgumentNullException.ThrowIfNull(store);
        var virtualPath = VirtualPath.Normalize(path);
        lock (StfReaderAccess.SyncRoot)
        {
            STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(store);
            using var reader = new STFReader(virtualPath, useTree: false);
            ActivityMetadata? result = null;
            reader.ParseFile(new[]
            {
                new STFReader.TokenProcessor("tr_activity", () => result = ReadActivity(reader, virtualPath))
            });
            return result ?? throw new InvalidDataException(
                $"Activity file '{virtualPath}' does not contain a Tr_Activity block.");
        }
    }

    private static ActivityMetadata ReadActivity(STFReader reader, string path)
    {
        var serial = 1;
        ActivityHeader? header = null;
        reader.MustMatch("(");
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("serial", () => serial = reader.ReadIntBlock(1)),
            new STFReader.TokenProcessor("tr_activity_header", () => header = ReadHeader(reader))
        });
        if (header is null)
            throw new InvalidDataException($"Activity file '{path}' is missing Tr_Activity_Header.");
        if (string.IsNullOrWhiteSpace(header.Name) || string.IsNullOrWhiteSpace(header.RouteId))
            throw new InvalidDataException($"Activity file '{path}' must define both Name and RouteID.");
        return new ActivityMetadata(path, serial, header.RouteId, header.Name, header.Description,
            header.Briefing, header.PathId, header.Mode, header.StartTime, header.Season,
            header.Weather, header.StartingSpeed, header.DurationMinutes, header.Difficulty);
    }

    private static ActivityHeader ReadHeader(STFReader reader)
    {
        string? routeId = null, name = null, description = null, briefing = null, pathId = null;
        var mode = 2;
        var startTime = new ActivityClockTime(10, 0, 0);
        var season = 1;
        var weather = 0;
        var startingSpeed = 0f;
        var durationMinutes = 60;
        var difficulty = 0;
        reader.MustMatch("(");
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("routeid", () => routeId = reader.ReadStringBlock(null)),
            new STFReader.TokenProcessor("name", () => name = reader.ReadStringBlock(null)),
            new STFReader.TokenProcessor("description", () => description = reader.ReadStringBlock("")),
            new STFReader.TokenProcessor("briefing", () => briefing = reader.ReadStringBlock("")),
            new STFReader.TokenProcessor("pathid", () => pathId = reader.ReadStringBlock(null)),
            new STFReader.TokenProcessor("mode", () => mode = reader.ReadIntBlock(mode)),
            new STFReader.TokenProcessor("starttime", () => startTime = ReadClockTime(reader)),
            new STFReader.TokenProcessor("season", () => season = reader.ReadIntBlock(season)),
            new STFReader.TokenProcessor("weather", () => weather = reader.ReadIntBlock(weather)),
            new STFReader.TokenProcessor("startingspeed", () => startingSpeed = reader.ReadFloatBlock(STFReader.UNITS.Speed, 0f)),
            new STFReader.TokenProcessor("duration", () => durationMinutes = ReadDurationMinutes(reader)),
            new STFReader.TokenProcessor("difficulty", () => difficulty = reader.ReadIntBlock(difficulty))
        });
        return new ActivityHeader(routeId, name, description ?? "", briefing ?? "", pathId,
            mode, startTime, season, weather, startingSpeed, durationMinutes, difficulty);
    }

    private static ActivityClockTime ReadClockTime(STFReader reader)
    {
        reader.MustMatch("(");
        var time = new ActivityClockTime(reader.ReadInt(null), reader.ReadInt(null), reader.ReadInt(null));
        reader.MustMatch(")");
        if (time.Hour is < 0 or > 23 || time.Minute is < 0 or > 59 || time.Second is < 0 or > 59)
            throw new InvalidDataException("Activity StartTime must be a valid 24-hour clock time.");
        return time;
    }

    private static int ReadDurationMinutes(STFReader reader)
    {
        reader.MustMatch("(");
        var hours = reader.ReadInt(null);
        var minutes = reader.ReadInt(null);
        reader.MustMatch(")");
        if (hours < 0 || minutes is < 0 or > 59)
            throw new InvalidDataException("Activity Duration must contain non-negative hours and 0–59 minutes.");
        return checked(hours * 60 + minutes);
    }

    private sealed record ActivityHeader(string? RouteId, string? Name, string Description,
        string Briefing, string? PathId, int Mode, ActivityClockTime StartTime, int Season,
        int Weather, float StartingSpeed, int DurationMinutes, int Difficulty);
}

public sealed record ActivityClockTime(int Hour, int Minute, int Second)
{
    public override string ToString() => $"{Hour:00}:{Minute:00}:{Second:00}";
}

public sealed record ActivityMetadata(string SourcePath, int Serial, string RouteId, string Name,
    string Description, string Briefing, string? PathId, int Mode, ActivityClockTime StartTime,
    int Season, int Weather, float StartingSpeedMetersPerSecond, int DurationMinutes, int Difficulty)
{
    public string ModeName => Mode switch { 0 => "Introductory train ride", 2 => "Player", 3 => "Tutorial", _ => $"Mode {Mode}" };
    public string SeasonName => Season switch { 0 => "Spring", 1 => "Summer", 2 => "Autumn", 3 => "Winter", _ => $"Season {Season}" };
    public string WeatherName => Weather switch { 0 => "Clear", 1 => "Snow", 2 => "Rain", _ => $"Weather {Weather}" };
    public string DifficultyName => Difficulty switch { 0 => "Easy", 1 => "Medium", 2 => "Hard", _ => $"Difficulty {Difficulty}" };
}

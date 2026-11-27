using System.Text;
using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Browser-safe port of Orts.Parsers.OR.TimetableReader. Reads the native
/// delimiter-prefixed spreadsheet format without requiring a host file path.
/// Each row preserves its cell positions, including empty continuation cells.
/// </summary>
public sealed class NativeTimetableReader
{
    private static readonly char[] SupportedSeparators = [';', ',', '\t'];

    public NativeTimetableGrid Read(IVirtualContentStore store, string path)
    {
        ArgumentNullException.ThrowIfNull(store);
        var virtualPath = VirtualPath.Normalize(path);
        using var stream = store.OpenRead(virtualPath);
        using var reader = new StreamReader(stream, Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);

        var firstLine = reader.ReadLine();
        if (string.IsNullOrEmpty(firstLine))
            throw new InvalidDataException($"Timetable {virtualPath} is empty or has an empty first line.");

        var separator = firstLine[0];
        if (!SupportedSeparators.Contains(separator))
            throw new InvalidDataException(
                $"Expected timetable separators are ';', ',' and tab, but found '{separator}' in {virtualPath}.");

        var rows = new List<IReadOnlyList<string>>();
        string? line = firstLine;
        do
        {
            rows.Add(Array.AsReadOnly(line.Split(separator, StringSplitOptions.None)
                .Select(cell => cell.Trim()).ToArray()));
            line = reader.ReadLine();
        } while (line is not null);

        return new NativeTimetableGrid(virtualPath, separator,
            Array.AsReadOnly(rows.ToArray()));
    }
}

public sealed record NativeTimetableGrid(
    string SourcePath,
    char Separator,
    IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>
/// Extracts the native timetable's menu/runtime header metadata from the
/// delimiter-preserving grid, following TimetableFileLite's header rows.
/// </summary>
public sealed class NativeTimetableDefinitionReader
{
    public NativeTimetableDefinition Read(IVirtualContentStore store, string path)
    {
        var grid = new NativeTimetableReader().Read(store, path);
        var header = grid.Rows[0];
        var firstCommentColumn = -1;
        var trains = new List<NativeTimetableTrainDefinition>();

        for (var column = 1; column < header.Count; column++)
        {
            var name = header[column].Trim();
            if (name.Equals("#comment", StringComparison.OrdinalIgnoreCase))
            {
                if (firstCommentColumn < 0)
                    firstCommentColumn = column;
            }
            else if (!string.IsNullOrEmpty(name) &&
                     !name.Contains("$static", StringComparison.OrdinalIgnoreCase))
            {
                trains.Add(new NativeTimetableTrainDefinition(column, name));
            }
        }

        var description = grid.SourcePath;
        var briefing = string.Empty;
        var rows = grid.Rows.Skip(1).ToArray();
        foreach (var row in rows)
        {
            if (row.Count == 0)
                continue;
            var key = row[0].Trim();
            if (key.Equals("#comment", StringComparison.OrdinalIgnoreCase) &&
                firstCommentColumn > 0 && firstCommentColumn < row.Count)
                description = row[firstCommentColumn];
            else if (key.Equals("#briefing", StringComparison.OrdinalIgnoreCase))
            {
                briefing = Cell(row, 1).Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase);
                foreach (var train in trains)
                    train.Briefing = Cell(row, train.Column).Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase);
            }
            else if (key.StartsWith("#path", StringComparison.OrdinalIgnoreCase))
                foreach (var train in trains) train.Path = Cell(row, train.Column);
            else if (key.Equals("#consist", StringComparison.OrdinalIgnoreCase))
                foreach (var train in trains) train.Consist = Cell(row, train.Column);
            else if (key.Equals("#direction", StringComparison.OrdinalIgnoreCase))
                foreach (var train in trains) train.Direction = Cell(row, train.Column);
            else if (key.Equals("#start", StringComparison.OrdinalIgnoreCase))
                foreach (var train in trains) train.StartTime = Cell(row, train.Column);
        }

        return new NativeTimetableDefinition(grid.SourcePath, description, briefing,
            Array.AsReadOnly(trains.ToArray()));
    }

    private static string Cell(IReadOnlyList<string> row, int column) =>
        column >= 0 && column < row.Count ? row[column] : string.Empty;
}

public sealed record NativeTimetableDefinition(
    string SourcePath,
    string Description,
    string Briefing,
    IReadOnlyList<NativeTimetableTrainDefinition> Trains);

public sealed class NativeTimetableTrainDefinition(int column, string train)
{
    public int Column { get; } = column;
    public string Train { get; } = train;
    public string Path { get; internal set; } = string.Empty;
    public string Consist { get; internal set; } = string.Empty;
    public string Direction { get; internal set; } = string.Empty;
    public string StartTime { get; internal set; } = string.Empty;
    public string Briefing { get; internal set; } = string.Empty;

    public bool TryGetDepartureSeconds(out double seconds)
    {
        if (TimeSpan.TryParse(StartTime, System.Globalization.CultureInfo.InvariantCulture, out var time) &&
            time >= TimeSpan.Zero && time.TotalSeconds <= int.MaxValue)
        {
            seconds = time.TotalSeconds;
            return true;
        }
        seconds = 0;
        return false;
    }
}

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

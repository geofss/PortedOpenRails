using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class NativeTimetableReaderTests
{
    [Fact]
    public void ReadsNativeCommaDelimitedRowsAndPreservesEmptyTrainColumns()
    {
        using var store = CreateStore("ROUTES/SCE/TIMETABLES/weekday.timetable",
            ",Train A,,Train C\n#path,Main,Main,Branch\n#start,08:00,,08:15\n");

        var grid = new NativeTimetableReader().Read(store, "ROUTES/SCE/TIMETABLES/weekday.timetable");

        Assert.Equal(',', grid.Separator);
        Assert.Equal(3, grid.Rows.Count);
        Assert.Equal(new[] { "Train A", "", "Train C" }, grid.Rows[0]);
        Assert.Equal(new[] { "#path", "Main", "Main", "Branch" }, grid.Rows[1]);
        Assert.Equal(new[] { "#start", "08:00", "", "08:15" }, grid.Rows[2]);
    }

    [Fact]
    public void ReadsNativeSemicolonAndTabSeparators()
    {
        using var semicolonStore = CreateStore("semi.timetable", ";Train A;Train B\n#path;A;B\n");
        using var tabStore = CreateStore("tab.timetable", "\tTrain A\tTrain B\n#path\tA\tB\n");

        Assert.Equal(';', new NativeTimetableReader().Read(semicolonStore, "semi.timetable").Separator);
        Assert.Equal('\t', new NativeTimetableReader().Read(tabStore, "tab.timetable").Separator);
    }

    [Fact]
    public void RejectsEmptyOrUnsupportedNativeTimetableHeaders()
    {
        using var emptyStore = CreateStore("empty.timetable", "");
        using var invalidStore = CreateStore("invalid.timetable", "|Train A\n");

        Assert.Throws<InvalidDataException>(() =>
            new NativeTimetableReader().Read(emptyStore, "empty.timetable"));
        Assert.Throws<InvalidDataException>(() =>
            new NativeTimetableReader().Read(invalidStore, "invalid.timetable"));
    }

    private static ZipContentStore CreateStore(string path, string text)
    {
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(path);
            using var stream = entry.Open();
            stream.Write(Encoding.UTF8.GetBytes(text));
        }
        memory.Position = 0;
        return new ZipContentStore(memory);
    }
}

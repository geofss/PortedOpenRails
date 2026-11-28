using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
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
        Assert.Equal(new[] { "", "Train A", "", "Train C" }, grid.Rows[0]);
        Assert.Equal(new[] { "#path", "Main", "Main", "Branch" }, grid.Rows[1]);
        Assert.Equal(new[] { "#start", "08:00", "", "08:15" }, grid.Rows[2]);
    }

    [Fact]
    public void ExtractsNativeTimetableDescriptionPathsConsistsAndDepartureTimes()
    {
        using var store = CreateStore("ROUTES/SCE/TIMETABLES/weekday.timetable",
            ",Train A,,Train C,#comment\n" +
            "#comment,,,,Weekday service\n" +
            "#path,Main,,Branch,\n" +
            "#consist,Express.con,,Local.con,\n" +
            "#direction,Up,,Down,\n" +
            "#start,08:00,,08:15,\n" +
            "#briefing,Main line service,,Branch service,General briefing\n");

        var timetable = new NativeTimetableDefinitionReader()
            .Read(store, "ROUTES/SCE/TIMETABLES/weekday.timetable");

        Assert.Equal("Weekday service", timetable.Description);
        Assert.Equal("Main line service", timetable.Briefing);
        Assert.Equal(2, timetable.Trains.Count);
        var first = timetable.Trains[0];
        Assert.Equal("Train A", first.Train);
        Assert.Equal("Main", first.Path);
        Assert.Equal("Express.con", first.Consist);
        Assert.Equal("Up", first.Direction);
        Assert.Equal("08:00", first.StartTime);
        Assert.Equal("Main line service", first.Briefing);
        Assert.True(first.TryGetDepartureSeconds(out var departure));
        Assert.Equal(8 * 60 * 60, departure);
        Assert.Equal("Branch", timetable.Trains[1].Path);
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


    [Fact]
    public void BindsNativeTimetableServicesToDispatcherAtTheirDeclaredStartTimes()
    {
        using var store = CreateStore("ROUTES/SCE/TIMETABLES/weekday.timetable",
            ",Train A,,Train C,#comment\n" +
            "#comment,,,,Weekday service\n" +
            "#path,Main,,Branch,\n" +
            "#consist,Express.con,,Local.con,\n" +
            "#direction,Up,,Down,\n" +
            "#start,08:00,,08:15,\n" +
            "Edinburgh,08:05,,08:20,\n" +
            "Haymarket,08:10,,08:25,\n");

        var timetable = new NativeTimetableDefinitionReader()
            .Read(store, "ROUTES/SCE/TIMETABLES/weekday.timetable");
        Assert.Equal(2, timetable.Trains[0].Stops.Count);
        Assert.Equal("Edinburgh", timetable.Trains[0].Stops[0].Location);
        Assert.True(timetable.Trains[0].Stops[0].TryGetTimeSeconds(out var firstStopSeconds));
        Assert.Equal(8 * 60 * 60 + 5 * 60, firstStopSeconds);

        var interlocking = new SignalInterlockingController(
            new uint[] { 2, 3 },
            new Dictionary<uint, IReadOnlyCollection<uint>>());
        var built = new NativeTimetableDispatcherBuilder().Build(timetable, train =>
            new NativeTimetableServiceRegistration(
                new AiServiceInterlockingSession(train.Train, interlocking),
                train.Train == "Train A" ? new uint[] { 2 } : new uint[] { 3 },
                new Dictionary<uint, uint>()));

        Assert.Equal(new[] { "Train A", "Train C" }, built.RegisteredServiceIds);
        Assert.Empty(built.Issues);
        Assert.Empty(built.Dispatcher.Advance(7 * 60 * 60));
        Assert.Equal(new[] { "Train A" }, built.Dispatcher.Advance(60 * 60));
        Assert.Equal(new[] { "Train C" }, built.Dispatcher.Advance(15 * 60));
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

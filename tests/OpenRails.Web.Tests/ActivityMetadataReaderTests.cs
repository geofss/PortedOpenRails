using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class ActivityMetadataReaderTests
{
    [Fact]
    public void ReadsSinglePlayerActivityHeaderUsingUpstreamStfReader()
    {
        using var store = CreateStore(("ROUTES/SCE/ACTIVITIES/Express.act", """
            SIMISA@@@@@@@@@@JINX0t1t______
            Tr_Activity (
                Serial ( 7 )
                Tr_Activity_Header (
                    RouteID ( SCE )
                    Name ( "Edinburgh to Glasgow" )
                    Description ( "Passenger service" )
                    Briefing ( "Call at each station" )
                    Mode ( 2 )
                    StartTime ( 8 15 30 )
                    Season ( 1 )
                    Weather ( 0 )
                    PathID ( MainLine )
                    StartingSpeed ( 12 )
                    Duration ( 1 45 )
                    Difficulty ( 1 )
                )
                Tr_Activity_File ( )
            )
            """));

        var activity = new ActivityMetadataReader().Read(store, "ROUTES/SCE/ACTIVITIES/Express.act");

        Assert.Equal(7, activity.Serial);
        Assert.Equal("SCE", activity.RouteId);
        Assert.Equal("Edinburgh to Glasgow", activity.Name);
        Assert.Equal("Passenger service", activity.Description);
        Assert.Equal("Call at each station", activity.Briefing);
        Assert.Equal("MainLine", activity.PathId);
        Assert.Equal("08:15:30", activity.StartTime.ToString());
        Assert.Equal(105, activity.DurationMinutes);
        Assert.Equal("Player", activity.ModeName);
        Assert.Equal("Summer", activity.SeasonName);
        Assert.Equal("Clear", activity.WeatherName);
        Assert.Equal("Medium", activity.DifficultyName);
    }

    [Fact]
    public void RejectsActivityWithoutHeader()
    {
        using var store = CreateStore(("ROUTES/SCE/ACTIVITIES/Bad.act", """
            SIMISA@@@@@@@@@@JINX0t1t______
            Tr_Activity ( Serial ( 1 ) )
            """));
        Assert.Throws<InvalidDataException>(() =>
            new ActivityMetadataReader().Read(store, "ROUTES/SCE/ACTIVITIES/Bad.act"));
    }

    private static ZipContentStore CreateStore(params (string Path, string Content)[] files)
    {
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Path);
                using var stream = entry.Open();
                stream.Write(Encoding.UTF8.GetBytes(file.Content));
            }
        }
        memory.Position = 0;
        return new ZipContentStore(memory);
    }
}

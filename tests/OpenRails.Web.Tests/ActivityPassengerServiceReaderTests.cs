using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class ActivityPassengerServiceReaderTests
{
    [Fact]
    public void ReadsNativePlayerTrafficStopSchedule()
    {
        using var store = CreateStore();
        var service = new ActivityPassengerServiceReader()
            .Read(store, "ROUTES/SCE/ACTIVITIES/Passenger.act");

        Assert.NotNull(service);
        Assert.Equal("Scottish Capital Express", service.ServiceName);
        Assert.Equal(36000, service.StartTimeSeconds);
        Assert.Equal(2, service.Stops.Count);
        Assert.Equal(new ActivityPassengerStopSchedule(3600, 3780, 1000, 5), service.Stops[0]);
        Assert.Equal(new ActivityPassengerStopSchedule(5000, 5180, 2000, 6), service.Stops[1]);
    }

    [Fact]
    public void ReturnsNullWhenActivityHasNoPlayerServiceDefinition()
    {
        using var store = CreateStore(includeService: false);

        Assert.Null(new ActivityPassengerServiceReader()
            .Read(store, "ROUTES/SCE/ACTIVITIES/Passenger.act"));
    }

    private static ZipContentStore CreateStore(bool includeService = true)
    {
        var service = includeService ? """
                Player_Service_Definition (
                  "Scottish Capital Express"
                  Player_Traffic_Definition (
                    36000
                    ArrivalTime ( 3600 ) DepartTime ( 3780 )
                    DistanceDownPath ( 1000 ) PlatformStartID ( 5 )
                    ArrivalTime ( 5000 ) DepartTime ( 5180 )
                    DistanceDownPath ( 2000 ) PlatformStartID ( 6 )
                  )
                )
            """ : "";
        var content = $"""
            SIMISA@@@@@@@@@@JINX0t1t______
            Tr_Activity (
              Tr_Activity_Header ( RouteID ( SCE ) Name ( Passenger ) )
              Tr_Activity_File (
            {service}
              )
            )
            """;
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("ROUTES/SCE/ACTIVITIES/Passenger.act");
            using var stream = entry.Open();
            stream.Write(Encoding.UTF8.GetBytes(content));
        }
        memory.Position = 0;
        return new ZipContentStore(memory);
    }
}

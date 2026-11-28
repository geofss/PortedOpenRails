using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class ServiceMetadataReaderTests
{
    [Fact]
    public void ReadsOriginalServiceDefinitionAndTimetableFields()
    {
        using var store = CreateStore(("TRAINS/SERVICES/Express.srv", """
            SIMISA@@@@@@@@@@JINX0t1t______
            Service_Definition (
                Serial ( 42 )
                Name ( "Morning Express" )
                Train_Config ( Express )
                PathID ( MainLine )
                MaxWheelAcceleration ( 0.18 )
                Efficiency ( 0.92 )
                Timetable ( StartingSpeed ( 45km/h ) )
            )
            """));

        var service = new ServiceMetadataReader().Read(store, "TRAINS/SERVICES/Express.srv");

        Assert.Equal(42, service.Serial);
        Assert.Equal("Morning Express", service.Name);
        Assert.Equal("Express", service.TrainConfig);
        Assert.Equal("MainLine", service.PathId);
        Assert.Equal(0.18f, service.MaximumWheelAcceleration, 4);
        Assert.Equal(0.92f, service.Efficiency, 4);
        Assert.Equal(12.5f, service.InitialSpeedMetersPerSecond, 3);
    }

    [Fact]
    public void RejectsServiceWithoutRequiredConsistOrPath()
    {
        using var store = CreateStore(("TRAINS/SERVICES/Broken.srv", """
            SIMISA@@@@@@@@@@JINX0t1t______
            Service_Definition ( Name ( Broken ) )
            """));
        Assert.Throws<InvalidDataException>(() =>
            new ServiceMetadataReader().Read(store, "TRAINS/SERVICES/Broken.srv"));
    }

    private static ZipContentStore CreateStore(params (string Path, string Content)[] files)
    {
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Path);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(file.Content);
            }
        }
        memory.Position = 0;
        return new ZipContentStore(memory);
    }
}

public sealed class ServiceFileLocatorTests
{
    [Fact]
    public void FindsGlobalServicesAndOnlySelectedRoutesPaths()
    {
        var paths = new[]
        {
            "TRAINS/SERVICES/Express.srv",
            "TRAINS/SERVICES/Freight.SRV",
            "TRAINS/CONSISTS/Express.con",
            "ROUTES/RouteA/RouteA.trk",
            "ROUTES/RouteA/PATHS/Main.pat",
            "ROUTES/RouteA/paths/Loop.PAT",
            "ROUTES/RouteB/PATHS/Other.pat"
        };

        Assert.Equal(new[] { "TRAINS/SERVICES/Express.srv", "TRAINS/SERVICES/Freight.SRV" },
            ServiceFileLocator.FindServices(paths));
        Assert.Equal(new[] { "ROUTES/RouteA/paths/Loop.PAT", "ROUTES/RouteA/PATHS/Main.pat" },
            ServiceFileLocator.FindPaths(paths, "ROUTES/RouteA/RouteA.trk"));
    }
}

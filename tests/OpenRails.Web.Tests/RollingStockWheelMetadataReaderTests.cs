using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class RollingStockWheelMetadataReaderTests
{
    [Fact]
    public void ReadsWagonAndEngineWheelRadii()
    {
        using var store = CreateStore(("TRAINS/TEST/engine.eng", """
            SIMISA@@@@@@@@@@JINX0t1t______
            Wagon ( Engine
                Size ( 2.8m 4m 20m )
                Mass ( 80t )
                WheelRadius ( 0.5m )
                Engine ( Diesel
                    WheelRadius ( 0.65m )
                )
            )
            """));

        var metadata = new RollingStockMetadataReader().Read(store, "TRAINS/TEST/engine.eng");

        Assert.Equal(0.5f, metadata.WheelRadiusM);
        Assert.Equal(0.65f, metadata.DriverWheelRadiusM);
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

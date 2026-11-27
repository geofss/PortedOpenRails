using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class CabViewAssetLocatorTests
{
    [Fact]
    public void ResolvesEngineCabViewRelativeToTheEngFile()
    {
        using var store = CreateStore(
            ("TRAINS/TRAINSET/DEMO/engine.eng", """
                SIMISA@@@@@@@@@@JINX0t1t______
                Engine (
                    CabView ( Cab/Front.CVF )
                    Wagon ( DEMO )
                )
                """),
            ("TRAINS/TRAINSET/DEMO/Cab/Front.CVF", "cab definition"));

        var path = new CabViewAssetLocator().Locate(store, "TRAINS/TRAINSET/DEMO/engine.eng");

        Assert.Equal("TRAINS/TRAINSET/DEMO/Cab/Front.CVF", path);
    }

    [Fact]
    public void ReturnsNullWhenEngineHasNoCabViewReference()
    {
        using var store = CreateStore(
            ("TRAINS/TRAINSET/DEMO/engine.eng", """
                SIMISA@@@@@@@@@@JINX0t1t______
                Engine ( Wagon ( DEMO ) )
                """));
        Assert.Null(new CabViewAssetLocator().Locate(store, "TRAINS/TRAINSET/DEMO/engine.eng"));
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

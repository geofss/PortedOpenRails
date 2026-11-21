using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class ConsistCatalogReaderTests
{
    [Fact]
    public void DiscoversConsistsAndReportsBrokenFilesWithoutStoppingCatalog()
    {
        using var store = CreateStore(
            ("TRAINS/CONSISTS/Alpha.con", """
                Train ( TrainCfg ( "Alpha" Engine ( UiD ( 1 ) EngineData ( "EngineA" "FolderA" ) ) ) )
                """),
            ("TRAINS/CONSISTS/Broken.con", """
                Train ( TrainCfg ( "Broken" Wagon ( UiD ( 2 ) ) ) )
                """),
            ("TRAINS/CONSISTS/Beta.con", """
                Train ( TrainCfg ( "Beta" Wagon ( UiD ( 3 ) WagonData ( "CarB" "FolderB" ) ) ) )
                """),
            ("TRAINS/CONSISTS/Duplicate.con", """
                Train ( TrainCfg ( "Duplicate" Wagon ( UiD ( 4 ) WagonData ( "CarA" "FolderA" ) ) Wagon ( UiD ( 4 ) WagonData ( "CarB" "FolderB" ) ) ) )
                """),
            ("TRAINS/ENGINES/EngineA/EngineA.eng", "unrelated"));

        var result = new ConsistCatalogReader().Read(store);

        Assert.Equal(new[] { "Alpha", "Beta", "Duplicate" }, result.Consists.Select(consist => consist.Name));
        Assert.Equal(2, result.Issues.Count);
        Assert.Equal("TRAINS/CONSISTS/Broken.con", result.Issues[0].Path);
        Assert.Contains("missing", result.Issues[0].Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("TRAINS/CONSISTS/Duplicate.con", result.Issues[1].Path);
        Assert.Contains("Duplicate car UID 4", result.Issues[1].Message);
    }

    private static ZipContentStore CreateStore(params (string Path, string Content)[] files)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in files)
            {
                var entry = archive.CreateEntry(path);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }
        stream.Position = 0;
        return new ZipContentStore(stream);
    }
}

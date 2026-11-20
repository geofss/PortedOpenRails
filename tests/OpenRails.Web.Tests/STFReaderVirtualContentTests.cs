using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using Orts.Parsers.Msts;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class STFReaderVirtualContentTests
{
    [Fact]
    public void UpstreamReaderParsesTokensFromVirtualZipContent()
    {
        using var store = CreateStore(
            ("ROUTES/Test/route.trk", "SIMISA@@@@@@@@@@JINX0T0t\nroute_name ( ExampleRoute )\nroute_value ( 42 )"),
            ("ROUTES/Test/parts/first.inc", "SIMISA@@@@@@@@@@JINX0T0t\nfirst_value ( 7 )"));

        STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(store);
        using var reader = new STFReader("ROUTES/Test/route.trk", false);

        var tokens = ReadAll(reader);

        Assert.Contains("route_name", tokens);
        Assert.Contains("ExampleRoute", tokens);
        Assert.Contains("route_value", tokens);
        Assert.Contains("42", tokens);
    }

    [Fact]
    public void UpstreamReaderResolvesNestedIncludesRelativeToEachIncludingFile()
    {
        using var store = CreateStore(
            ("ROUTES/Test/route.trk", "SIMISA@@@@@@@@@@JINX0T0t\ninclude ( parts/first.inc )\nroot_value ( 99 )"),
            ("ROUTES/Test/parts/first.inc", "SIMISA@@@@@@@@@@JINX0T0t\ninclude ( nested/second.inc )\nfirst_value ( 7 )"),
            ("ROUTES/Test/parts/nested/second.inc", "SIMISA@@@@@@@@@@JINX0T0t\nsecond_value ( 13 )"));

        STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(store);
        using var reader = new STFReader("ROUTES/Test/route.trk", false);

        var tokens = ReadAll(reader);

        Assert.Contains("second_value", tokens);
        Assert.Contains("13", tokens);
        Assert.Contains("first_value", tokens);
        Assert.Contains("7", tokens);
        Assert.Contains("root_value", tokens);
        Assert.Contains("99", tokens);
    }

    [Fact]
    public void UpstreamReaderRejectsIncludeEscapingVirtualRoot()
    {
        using var store = CreateStore(
            ("route.trk", "SIMISA@@@@@@@@@@JINX0T0t\ninclude ( ../outside.inc )"),
            ("outside.inc", "SIMISA@@@@@@@@@@JINX0T0t\noutside_value ( 1 )"));

        STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(store);

        Assert.Throws<ArgumentException>(() =>
        {
            using var reader = new STFReader("route.trk", false);
            ReadAll(reader);
        });
    }

    private static List<string> ReadAll(STFReader reader)
    {
        var tokens = new List<string>();
        while (!reader.Eof)
            tokens.Add(reader.ReadItem());
        return tokens;
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
                var bytes = Encoding.UTF8.GetBytes(file.Content);
                stream.Write(bytes);
            }
        }

        memory.Position = 0;
        return new ZipContentStore(memory);
    }
}

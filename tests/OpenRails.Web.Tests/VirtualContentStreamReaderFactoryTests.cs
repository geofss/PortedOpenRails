using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class VirtualContentStreamReaderFactoryTests
{
    [Fact]
    public void GetStreamReaderReadsSimisSignatureAndLeavesContentPositioned()
    {
        using var store = CreateStore(("ROUTES/Test/route.trk", "SIMISA@@@@@@@@@@JINX0T0t\nRouteData"));
        var factory = new VirtualContentStreamReaderFactory(store);

        using var reader = factory.GetStreamReader("routes/test/route.trk", out var signature);

        Assert.Equal("SIMISA@@@@@@@@@@JINX0T0t", signature);
        Assert.Equal("RouteData", reader.ReadToEnd());
    }

    [Fact]
    public void GetStreamReaderPreservesFirstLineWhenNoSimisSignatureExists()
    {
        using var store = CreateStore(("route.trk", "route_name ( ExampleRoute )\nroute_value ( 42 )"));
        var factory = new VirtualContentStreamReaderFactory(store);

        using var reader = factory.GetStreamReader("route.trk", out var signature);

        Assert.Null(signature);
        Assert.Equal("route_name ( ExampleRoute )\nroute_value ( 42 )", reader.ReadToEnd());
    }

    [Fact]
    public void GetStreamReaderSupportsUtf16Bom()
    {
        var content = Encoding.Unicode.GetPreamble()
            .Concat(Encoding.Unicode.GetBytes("SIMISA UTF16\nRouteData"))
            .ToArray();
        using var store = CreateStore(("route.trk", content));
        var factory = new VirtualContentStreamReaderFactory(store);

        using var reader = factory.GetStreamReader("route.trk", out var signature);

        Assert.Equal("SIMISA UTF16", signature);
        Assert.Equal("RouteData", reader.ReadToEnd());
    }

    [Fact]
    public void GetStreamReaderReportsMissingVirtualFile()
    {
        using var store = CreateStore(("route.trk", Encoding.UTF8.GetBytes("data")));
        var factory = new VirtualContentStreamReaderFactory(store);

        Assert.Throws<FileNotFoundException>(() =>
            factory.GetStreamReader("missing.trk", out _));
    }

    [Theory]
    [InlineData("ROUTES/Test/route.trk", "../GLOBAL/tsection.dat", "ROUTES/GLOBAL/tsection.dat")]
    [InlineData("ROUTES/Test/sub/parts.inc", "../../GLOBAL/tsection.dat", "ROUTES/GLOBAL/tsection.dat")]
    public void ResolveIncludePathUsesContainingFilesDirectory(
        string containingFile, string include, string expected)
    {
        var factory = new VirtualContentStreamReaderFactory(CreateStore(("dummy", Encoding.UTF8.GetBytes(""))));

        Assert.Equal(expected, factory.ResolveIncludePath(containingFile, include));
    }

    [Fact]
    public void ResolveIncludePathRejectsVirtualRootEscape()
    {
        var factory = new VirtualContentStreamReaderFactory(CreateStore(("dummy", Encoding.UTF8.GetBytes(""))));

        Assert.Throws<ArgumentException>(() =>
            factory.ResolveIncludePath("route.trk", "../outside.inc"));
    }

    private static ZipContentStore CreateStore(params (string Path, byte[] Content)[] files)
    {
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Path);
                using var stream = entry.Open();
                stream.Write(file.Content);
            }
        }

        memory.Position = 0;
        return new ZipContentStore(memory);
    }

    private static ZipContentStore CreateStore(params (string Path, string Content)[] files) =>
        CreateStore(files.Select(file => (file.Path, Encoding.UTF8.GetBytes(file.Content))).ToArray());
}

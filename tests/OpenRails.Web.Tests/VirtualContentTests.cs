using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class VirtualContentTests
{
    [Theory]
    [InlineData(@"ROUTES\\Test\\ROUTE.TDB", "ROUTES/Test/ROUTE.TDB")]
    [InlineData("ROUTES//Test/ROUTE.TDB", "ROUTES/Test/ROUTE.TDB")]
    [InlineData("GLOBAL\\TSECTION.DAT", "GLOBAL/TSECTION.DAT")]
    public void Normalize_UsesPortableSeparators(string input, string expected)
    {
        Assert.Equal(expected, VirtualPath.Normalize(input));
    }

    [Theory]
    [InlineData("../../GLOBAL/TSECTION.DAT", "ROUTES/Test/route.trk", "GLOBAL/TSECTION.DAT")]
    [InlineData("./sub/parts.dat", "ROUTES/Test/route.trk", "ROUTES/Test/sub/parts.dat")]
    [InlineData(@"..\\..\\GLOBAL\\TSECTION.DAT", "ROUTES/Test/route.trk", "GLOBAL/TSECTION.DAT")]
    public void ResolveRelative_ResolvesIncludesWithinRoot(string include, string source, string expected)
    {
        Assert.Equal(expected, VirtualPath.ResolveRelative(source, include));
    }

    [Theory]
    [InlineData(".", "ROUTES/Test/route.trk")]
    [InlineData("./", "ROUTES/Test/route.trk")]
    [InlineData("../", "ROUTES/Test/route.trk")]
    [InlineData("../../../../outside.dat", "ROUTES/Test/route.trk")]
    [InlineData("/outside.dat", "ROUTES/Test/route.trk")]
    [InlineData("C:\\outside.dat", "ROUTES/Test/route.trk")]
    public void ResolveRelative_RejectsRootEscape(string include, string source)
    {
        Assert.Throws<ArgumentException>(() => VirtualPath.ResolveRelative(source, include));
    }

    [Fact]
    public void ZipStore_IndexesAndReadsEntriesCaseInsensitively()
    {
        using var store = CreateStore(("ROUTES/Test/route.trk", "RouteData"), ("GLOBAL/tsection.dat", "TrackSections"));

        Assert.True(store.Contains(@"routes\\test\\ROUTE.TRK"));
        Assert.Equal(2, store.Paths.Count);

        using var stream = store.OpenRead("routes/test/route.trk");
        using var reader = new StreamReader(stream);
        Assert.Equal("RouteData", reader.ReadToEnd());
    }

    [Fact]
    public void ZipStore_TryOpenReadReturnsFalseForMissingFile()
    {
        using var store = CreateStore(("route.trk", "data"));

        Assert.False(store.TryOpenRead("missing.trk", out var stream));
        Assert.Null(stream);
    }

    [Fact]
    public void ZipStore_OpenReadReportsMissingVirtualFile()
    {
        using var store = CreateStore(("route.trk", "data"));
        Assert.Throws<FileNotFoundException>(() => store.OpenRead("missing.trk"));
    }

    [Fact]
    public void ZipStore_RejectsTraversalEntries()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            CreateStore(("../outside.txt", "not allowed")));

        Assert.Contains("Unsafe ZIP entry path", exception.Message);
    }

    [Fact]
    public void ZipStore_EnforcesUncompressedSizeLimit()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            CreateStoreWithLimits(new[] { ("large.txt", "123456") }, maxEntryBytes: 5));

        Assert.Contains("exceeds", exception.Message);
    }

    [Fact]
    public void ZipStore_EnforcesTotalUncompressedSizeLimitAcrossEntries()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            CreateStoreWithLimits(new[] { ("one.txt", "1234"), ("two.txt", "5678") }, maxTotalBytes: 7));

        Assert.Contains("uncompressed limit", exception.Message);
    }

    [Fact]
    public void ZipStore_EnforcesArchiveEntryCountLimit()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            CreateStoreWithLimits(new[] { ("one.txt", "1"), ("two.txt", "2") }, maxEntries: 1));

        Assert.Contains("more than 1 entries", exception.Message);
    }

    [Fact]
    public void ZipStore_RejectsDuplicateVirtualPathsCaseInsensitively()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            CreateStoreWithLimits(new[] { ("ROUTES/Test.trk", "a"), ("routes/test.TRK", "b") }));

        Assert.Contains("duplicate virtual paths", exception.Message);
    }

    [Fact]
    public void ZipStore_ThrowsObjectDisposedAfterStoreDisposal()
    {
        var store = CreateStore(("route.trk", "data"));
        store.Dispose();

        Assert.Throws<ObjectDisposedException>(() => store.Contains("route.trk"));
        Assert.Throws<ObjectDisposedException>(() => store.OpenRead("route.trk"));
        Assert.Throws<ObjectDisposedException>(() => store.TryOpenRead("route.trk", out _));
    }

    private static ZipContentStore CreateStore(params (string Path, string Content)[] files) =>
        CreateStoreWithLimits(files, ZipContentStore.DefaultMaxEntryBytes);

    private static ZipContentStore CreateStoreWithLimits(
        (string Path, string Content)[] files,
        long maxEntryBytes = ZipContentStore.DefaultMaxEntryBytes,
        long maxTotalBytes = ZipContentStore.DefaultMaxTotalBytes,
        int maxEntries = ZipContentStore.DefaultMaxEntries)
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
        return new ZipContentStore(memory, maxEntryBytes: maxEntryBytes, maxTotalBytes: maxTotalBytes, maxEntries: maxEntries);
    }
}

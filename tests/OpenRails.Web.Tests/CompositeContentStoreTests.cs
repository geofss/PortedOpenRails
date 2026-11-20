using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class CompositeContentStoreTests
{
    [Fact]
    public void HigherPrecedencePackageOverridesLowerPackage()
    {
        var global = CreateStore(("GLOBAL.TSECTION", "global"));
        var route = CreateStore(("GLOBAL.TSECTION", "route override"), ("ROUTE.TRK", "route"));

        using var composite = new CompositeContentStore(new[]
        {
            new PackageLayer("Route", route),
            new PackageLayer("Global", global)
        });

        using var reader = new StreamReader(composite.OpenRead("global.tsection"));
        Assert.Equal("route override", reader.ReadToEnd());
        Assert.Equal("Route", composite.GetSourcePackage("GLOBAL.TSECTION"));
    }

    [Fact]
    public void CompositeListsUnionOfPathsWithoutDuplicates()
    {
        using var composite = new CompositeContentStore(new[]
        {
            new PackageLayer("Route", CreateStore(("shared.dat", "route"), ("route.trk", "route"))),
            new PackageLayer("Global", CreateStore(("SHARED.DAT", "global"), ("global.dat", "global")))
        });

        Assert.Equal(3, composite.Paths.Count);
        Assert.Contains(composite.Paths, path => path.Equals("shared.dat", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UniqueFilesFallThroughToLowerPrecedencePackage()
    {
        using var composite = new CompositeContentStore(new[]
        {
            new PackageLayer("Route", CreateStore(("route.trk", "route"))),
            new PackageLayer("Global", CreateStore(("tsection.dat", "global track sections")))
        });

        using var reader = new StreamReader(composite.OpenRead("tsection.dat"));
        Assert.Equal("global track sections", reader.ReadToEnd());
        Assert.Equal("Global", composite.GetSourcePackage("tsection.dat"));
    }

    [Fact]
    public void MissingFileReturnsFalseAcrossAllPackages()
    {
        using var composite = new CompositeContentStore(new[]
        {
            new PackageLayer("Route", CreateStore(("route.trk", "route"))),
            new PackageLayer("Global", CreateStore(("tsection.dat", "global")))
        });

        Assert.False(composite.TryOpenRead("missing.dat", out var stream));
        Assert.Null(stream);
    }

    [Fact]
    public void DuplicatePackageNamesAreRejected()
    {
        var first = CreateStore(("a.dat", "a"));
        var second = CreateStore(("b.dat", "b"));

        try
        {
            Assert.Throws<ArgumentException>(() => new CompositeContentStore(new[]
            {
                new PackageLayer("Global", first),
                new PackageLayer("global", second)
            }));
        }
        finally
        {
            first.Dispose();
            second.Dispose();
        }
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

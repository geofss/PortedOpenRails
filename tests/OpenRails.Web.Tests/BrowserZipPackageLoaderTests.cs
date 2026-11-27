using OpenRails.Web.Content;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class BrowserZipPackageLoaderTests
{
    [Fact]
    public void DemoModel1UsesDiskBackedImportInsteadOfTheInMemoryZipPath()
    {
        const long demoModel1ArchiveBytes = 272_422_379;

        Assert.True(
            demoModel1ArchiveBytes > ZipContentStore.DefaultMaxBrowserArchiveBytes,
            "The authorized Demo Model 1 archive must be routed through the OPFS streaming importer, not copied into a WebAssembly MemoryStream.");
        Assert.True(BrowserPackageArchiveLimits.MaxArchiveBytes >= demoModel1ArchiveBytes);
    }

    [Fact]
    public void DiskBackedBrowserUploadSupportsThirtyGigabytes()
    {
        Assert.Equal(30_000_000_000L, BrowserPackageArchiveLimits.MaxArchiveBytes);
        Assert.True(BrowserPackageArchiveLimits.MaxArchiveBytes > 5_000_000_000L);
    }

    [Fact]
    public void InMemoryZipParserRemainsSeparatelyBounded()
    {
        Assert.Equal(64L * 1024 * 1024, ZipContentStore.DefaultMaxBrowserArchiveBytes);
        Assert.True(BrowserPackageArchiveLimits.MaxArchiveBytes > ZipContentStore.DefaultMaxBrowserArchiveBytes);
    }
}
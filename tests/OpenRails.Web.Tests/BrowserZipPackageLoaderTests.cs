using OpenRails.Web.Content;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class BrowserZipPackageLoaderTests
{
    [Fact]
    public void DefaultUploadLimitAllowsPublishedDemoModel1ArchiveSize()
    {
        const long demoModel1ArchiveBytes = 272_422_379;

        Assert.True(
            ZipContentStore.DefaultMaxBrowserArchiveBytes >= demoModel1ArchiveBytes,
            $"The browser upload limit ({ZipContentStore.DefaultMaxBrowserArchiveBytes} bytes) must permit the authorized Demo Model 1 archive ({demoModel1ArchiveBytes} bytes).");
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
        Assert.Equal(300L * 1024 * 1024, ZipContentStore.DefaultMaxBrowserArchiveBytes);
        Assert.True(BrowserPackageArchiveLimits.MaxArchiveBytes > ZipContentStore.DefaultMaxBrowserArchiveBytes);
    }
}

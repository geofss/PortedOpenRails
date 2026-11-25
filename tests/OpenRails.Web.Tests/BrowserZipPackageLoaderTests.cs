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
    public void DefaultUploadLimitRemainsBounded()
    {
        Assert.InRange(
            ZipContentStore.DefaultMaxBrowserArchiveBytes,
            272_422_379,
            512L * 1024 * 1024);
    }
}

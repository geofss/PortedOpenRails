using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class SignalConfigurationLocatorTests
{
    [Fact]
    public void FindsRouteSignalConfigurationRegardlessOfPathCase()
    {
        var result = SignalConfigurationLocator.FindForRoute(
            new[] { "ROUTES/RouteA/RouteA.trk", "routes/routea/SIGCFG.DAT", "ROUTES/RouteB/sigcfg.dat" },
            "ROUTES/RouteA");
        Assert.Equal("routes/routea/SIGCFG.DAT", result);
    }
}

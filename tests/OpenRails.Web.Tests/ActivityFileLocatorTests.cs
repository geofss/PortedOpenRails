using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class ActivityFileLocatorTests
{
    [Fact]
    public void FindsOnlyActivitiesInsideSelectedRouteFolder()
    {
        var paths = new[]
        {
            "ROUTES/RouteA/RouteA.trk",
            "ROUTES/RouteA/ACTIVITIES/Passenger.act",
            "ROUTES/RouteA/activities/Freight.ACT",
            "ROUTES/RouteA/PATHS/NotActivity.pat",
            "ROUTES/RouteB/ACTIVITIES/Other.act",
            "TRAINS/ACTIVITIES/Global.act"
        };

        var result = ActivityFileLocator.FindForRoute(paths, "routes\\RouteA\\RouteA.trk");

        Assert.Equal(new[]
        {
            "ROUTES/RouteA/activities/Freight.ACT",
            "ROUTES/RouteA/ACTIVITIES/Passenger.act"
        }, result);
    }

    [Fact]
    public void ReturnsEmptyWhenMetadataPathDoesNotContainRouteFolder()
    {
        Assert.Empty(ActivityFileLocator.FindForRoute(
            new[] { "ROUTES/RouteA/ACTIVITIES/Passenger.act" }, "Passenger.trk"));
    }
}

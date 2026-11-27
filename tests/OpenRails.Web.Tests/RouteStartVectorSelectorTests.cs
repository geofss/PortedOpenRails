using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class RouteStartVectorSelectorTests
{
    [Fact]
    public void RanksVectorsByNearestEndpointToDeclaredRouteStart()
    {
        var nearAtStart = CreatePath(10, new[] { 1, 2 }, 100, 200, 0);
        var nearAtEnd = CreatePath(20, new[] { 2, 3 }, 500, 600, 0);
        var far = CreatePath(30, new[] { 3, 4 }, 1800, 1900, 0);
        var network = CreateNetwork(nearAtStart, far, nearAtEnd);

        var selected = new RouteStartVectorSelector().SelectNearest(
            network, new RouteStartCoordinates(0, 0, 510, 0));

        Assert.Equal(new uint[] { 20, 10, 30 },
            selected.Select(path => path.TrackNodeIndex));
    }

    [Fact]
    public void RejectsNonFiniteRouteStartCoordinates()
    {
        var selector = new RouteStartVectorSelector();
        var network = CreateNetwork(CreatePath(1, new[] { 1, 2 }, 0, 100, 0));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            selector.SelectNearest(network, new RouteStartCoordinates(0, 0, double.NaN, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            selector.IsStartReversed(network.VectorPaths[0],
                new RouteStartCoordinates(double.PositiveInfinity, 0, 0, 0)));
    }

    [Fact]
    public void DetectsWhenDeclaredRouteStartIsNearestTheStoredVectorEnd()
    {
        var path = CreatePath(42, new[] { 1, 2 }, 100, 500, 0);
        var selector = new RouteStartVectorSelector();

        Assert.True(selector.IsStartReversed(path, new RouteStartCoordinates(0, 0, 490, 0)));
        Assert.False(selector.IsStartReversed(path, new RouteStartCoordinates(0, 0, 110, 0)));
    }

    [Fact]
    public void ConvertsRouteStartTileCoordinatesIntoWorldCoordinates()
    {
        var targetX = TrackNetworkBuilder.TileSizeMeters * 12 + 40;
        var exact = CreatePath(77, new[] { 1, 2 }, targetX - 50, targetX, 0);
        var other = CreatePath(88, new[] { 2, 3 }, 40, 90, 0);
        var network = CreateNetwork(other, exact);

        var selected = new RouteStartVectorSelector().SelectNearest(
            network, new RouteStartCoordinates(12, 0, 40, 0));

        Assert.Equal(77u, selected[0].TrackNodeIndex);
    }

    [Fact]
    public void ExcludesVectorsWithoutTwoConnectedEndpointsAndHonorsResultLimit()
    {
        var validLong = CreatePath(10, new[] { 1, 2 }, 0, 100, 0);
        var validShort = CreatePath(20, new[] { 2, 3 }, 10, 20, 0);
        var invalid = CreatePath(30, new[] { 3 }, 0, 1, 0);
        var network = CreateNetwork(validLong, invalid, validShort);

        var selected = new RouteStartVectorSelector().SelectNearest(
            network, new RouteStartCoordinates(0, 0, 0, 0), maximumResults: 1);

        Assert.Single(selected);
        Assert.Equal(10u, selected[0].TrackNodeIndex);
    }

    private static TrackNetwork CreateNetwork(params TrackVectorPath[] paths) =>
        new(new TrackDatabase(new TrackNode?[] { null }, Array.Empty<string>()),
            paths, Array.Empty<string>());

    private static TrackVectorPath CreatePath(
        uint id, int[] nodes, double startX, double endX, double z)
    {
        var start = new TrackWorldPosition(startX, 0, z);
        var end = new TrackWorldPosition(endX, 0, z);
        var section = new TrackSectionPlacement(
            new TrackVectorSection(1, 0, 0, 0, 0, 0, 0, "00", 0, 0,
                (float)startX, 0, (float)z, 0, 0, 0),
            new TrackSectionGeometry(1, null, (float)Math.Abs(endX - startX), null, null, false),
            start,
            Math.Abs(endX - startX));
        return new TrackVectorPath(id, nodes, new[] { section },
            Math.Abs(endX - startX), end);
    }
}

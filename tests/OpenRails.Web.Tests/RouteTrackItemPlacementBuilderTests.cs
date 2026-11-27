using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class RouteTrackItemPlacementBuilderTests
{
    [Fact]
    public void MapsLinkedPlatformItemsToTrackGeometry()
    {
        var first = new TrackItem(0, "platform", 0, 0, 0, 0, 0, 0, 0, 0, 0,
            30, string.Empty, "Platform A", "Station", string.Empty, 1, null, null);
        var second = new TrackItem(1, "platform", 0, 0, 0, 0, 0, 0, 0, 0, 0,
            70, string.Empty, "Platform B", "Station", string.Empty, 0, null, null);
        var node = new TrackNode(1, TrackNodeKind.Vector, null, null,
            Array.Empty<TrackVectorSection>(),
            new[] { new TrackPin(2, 0), new TrackPin(3, 0) },
            new[] { 0, 1 });
        var database = new TrackDatabase(
            new TrackNode?[] { null, node },
            new TrackItem?[] { first, second },
            Array.Empty<string>());
        var section = new TrackVectorSection(1, 1, 0, 0, 0, 0, 0, string.Empty,
            0, 0, 0, 0, 0, 0, 0, 0);
        var geometry = new TrackSectionGeometry(1, null, 100, null, null, false);
        var path = new TrackVectorPath(1, new[] { 2, 3 },
            new[]
            {
                new TrackSectionPlacement(section, geometry, new TrackWorldPosition(0, 0, 0), 100)
            },
            100,
            new TrackWorldPosition(0, 0, 100));
        var network = new TrackNetwork(database, new[] { path }, Array.Empty<string>());

        var result = new RouteTrackItemPlacementBuilder().Build(network);

        Assert.Equal(2, result.Placements.Count);
        Assert.Equal(30, result.Placements[0].Position.Z, 2);
        Assert.Equal(70, result.Placements[1].Position.Z, 2);
        Assert.All(result.Placements, placement => Assert.True(placement.LinkedItemResolved));
        Assert.Empty(result.Diagnostics);
    }
}

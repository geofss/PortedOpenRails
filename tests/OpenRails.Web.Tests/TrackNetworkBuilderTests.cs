using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackNetworkBuilderTests
{
    [Fact]
    public void BuildsVectorPathsUsingRouteOverridesAndAbsoluteTileCoordinates()
    {
        var vector = new TrackNode(
            1,
            TrackNodeKind.Vector,
            null,
            null,
            new[]
            {
                CreateSection(1, tileX: 2, tileZ: -1, x: 12, y: 5, z: 34),
                CreateSection(2, tileX: 2, tileZ: -1, x: 22, y: 5, z: 44)
            },
            new[] { new TrackPin(2, 0), new TrackPin(3, 1) });

        var endA = new TrackNode(2, TrackNodeKind.End, null, null,
            Array.Empty<TrackVectorSection>(), new[] { new TrackPin(1, 0) });
        var endB = new TrackNode(3, TrackNodeKind.End, null, null,
            Array.Empty<TrackVectorSection>(), new[] { new TrackPin(1, 1) });
        var database = new TrackDatabase(new TrackNode?[] { null, vector, endA, endB }, Array.Empty<string>());

        var global = new TrackSectionCatalog(10, new Dictionary<uint, TrackSectionGeometry>
        {
            [1] = new(1, null, 10, null, null, false),
            [2] = new(2, null, 20, null, null, false)
        });
        var route = new TrackSectionCatalog(20, new Dictionary<uint, TrackSectionGeometry>
        {
            [2] = new(2, null, 30, null, null, true)
        });

        var network = new TrackNetworkBuilder().Build(database, global, route);

        var path = Assert.Single(network.VectorPaths);
        Assert.Equal((uint)1, path.TrackNodeIndex);
        Assert.Equal(new[] { 2, 3 }, path.ConnectedNodeIndices);
        Assert.Equal(40, path.LengthMeters);
        Assert.Equal(40, network.TotalTrackLengthMeters);
        Assert.Equal(2, path.Sections.Count);
        Assert.Equal(30, path.Sections[1].LengthMeters);
        Assert.True(path.Sections[1].Geometry.IsRouteSpecific);
        Assert.Equal(4108, path.Sections[0].Start.X);
        Assert.Equal(5, path.Sections[0].Start.Y);
        Assert.Equal(-2014, path.Sections[0].Start.Z);
        Assert.Empty(network.Diagnostics);
    }

    [Fact]
    public void RejectsTrackSectionReferencesMissingFromBothCatalogs()
    {
        var vector = new TrackNode(1, TrackNodeKind.Vector, null, null,
            new[] { CreateSection(99, 0, 0, 0, 0, 0) },
            new[] { new TrackPin(2, 0), new TrackPin(3, 1) });
        var database = new TrackDatabase(new TrackNode?[]
        {
            null,
            vector,
            new TrackNode(2, TrackNodeKind.End, null, null, Array.Empty<TrackVectorSection>(), Array.Empty<TrackPin>()),
            new TrackNode(3, TrackNodeKind.End, null, null, Array.Empty<TrackVectorSection>(), Array.Empty<TrackPin>())
        }, Array.Empty<string>());

        var emptyCatalog = new TrackSectionCatalog(0, new Dictionary<uint, TrackSectionGeometry>());

        var error = Assert.Throws<InvalidDataException>(() =>
            new TrackNetworkBuilder().Build(database, emptyCatalog));

        Assert.Contains("Track section 99", error.Message);
    }

    private static TrackVectorSection CreateSection(uint index, int tileX, int tileZ,
        float x, float y, float z) =>
        new(index, 0, tileX, tileZ, 0, 0, 1, "00", tileX, tileZ, x, y, z, 0, 0, 0);
}

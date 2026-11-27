using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackNetworkRouteBuilderTests
{
    [Fact]
    public void TraversesConnectedVectorNodesAndReversesTheNextPathWhenRequired()
    {
        var first = CreatePath(10, new[] { 1, 2 }, startX: 0, endX: 100);
        // This vector is stored from node 3 toward node 2, so traversal from
        // node 2 must reverse its original section order.
        var second = CreatePath(20, new[] { 3, 2 }, startX: 200, endX: 100);
        var third = CreatePath(30, new[] { 3, 4 }, startX: 200, endX: 300);
        var network = new TrackNetwork(
            new TrackDatabase(new TrackNode?[] { null }, Array.Empty<string>()),
            new[] { first, second, third },
            Array.Empty<string>());

        var route = new TrackNetworkRouteBuilder().Build(network, 10);

        Assert.Equal(new uint[] { 10, 20, 30 }, route.Segments.Select(segment => segment.Path.TrackNodeIndex));
        Assert.False(route.Segments[0].IsReversed);
        Assert.True(route.Segments[1].IsReversed);
        Assert.False(route.Segments[2].IsReversed);
        Assert.Equal(300, route.LengthMeters);
        Assert.Empty(route.Diagnostics);
    }

    [Fact]
    public void CanStartTraversalFromTheReverseEndOfASelectedVector()
    {
        var first = CreatePath(10, new[] { 1, 2 }, 0, 100);
        var previous = CreatePath(20, new[] { 0, 1 }, -100, 0);
        var network = new TrackNetwork(
            new TrackDatabase(new TrackNode?[] { null }, Array.Empty<string>()),
            new[] { first, previous },
            Array.Empty<string>());

        var route = new TrackNetworkRouteBuilder().Build(
            network, 10, startReversed: true);

        Assert.Equal(new uint[] { 10, 20 }, route.Segments.Select(segment => segment.Path.TrackNodeIndex));
        Assert.True(route.Segments[0].IsReversed);
        Assert.True(route.Segments[1].IsReversed);
        Assert.Equal(200, route.LengthMeters);
        Assert.Empty(route.Diagnostics);
    }

    [Fact]
    public void StopsAndReportsAmbiguousJunctionContinuation()
    {
        var first = CreatePath(10, new[] { 1, 2 }, 0, 100);
        var branchA = CreatePath(20, new[] { 2, 3 }, 100, 200);
        var branchB = CreatePath(30, new[] { 2, 4 }, 100, 200);
        var network = new TrackNetwork(
            new TrackDatabase(new TrackNode?[] { null }, Array.Empty<string>()),
            new[] { first, branchA, branchB },
            Array.Empty<string>());

        var route = new TrackNetworkRouteBuilder().Build(network, 10);

        Assert.Single(route.Segments);
        Assert.Equal(2, route.PendingJunctionNodeIndex);
        Assert.Contains(route.Diagnostics, diagnostic => diagnostic.Contains("ambiguous"));
    }

    [Fact]
    public void FollowsExplicitlySelectedBranchAtAmbiguousJunction()
    {
        var first = CreatePath(10, new[] { 1, 2 }, 0, 100);
        var branchA = CreatePath(20, new[] { 2, 3 }, 100, 200);
        var branchB = CreatePath(30, new[] { 2, 4 }, 100, 250);
        var network = new TrackNetwork(
            new TrackDatabase(new TrackNode?[] { null }, Array.Empty<string>()),
            new[] { first, branchA, branchB },
            Array.Empty<string>());

        var route = new TrackNetworkRouteBuilder().Build(
            network, 10, preferredNextTrackNodeIndex: 30);

        Assert.Equal(new uint[] { 10, 30 },
            route.Segments.Select(segment => segment.Path.TrackNodeIndex));
        Assert.Empty(route.Diagnostics);
    }

    [Fact]
    public void FollowsExplicitBranchChoicesAtMultipleJunctions()
    {
        var first = CreatePath(10, new[] { 1, 2 }, 0, 100);
        var firstUnselected = CreatePath(20, new[] { 2, 3 }, 100, 200);
        var selectedFirst = CreatePath(30, new[] { 2, 3 }, 100, 200);
        var secondUnselected = CreatePath(40, new[] { 3, 4 }, 200, 300);
        var selectedSecond = CreatePath(50, new[] { 3, 5 }, 200, 300);
        var network = new TrackNetwork(
            new TrackDatabase(new TrackNode?[] { null }, Array.Empty<string>()),
            new[] { first, firstUnselected, selectedFirst, secondUnselected, selectedSecond },
            Array.Empty<string>());

        var route = new TrackNetworkRouteBuilder().Build(
            network, 10,
            preferredNextTrackNodeIndices: new Dictionary<int, uint>
            {
                [2] = 30,
                [3] = 50
            });

        Assert.Equal(new uint[] { 10, 30, 50 },
            route.Segments.Select(segment => segment.Path.TrackNodeIndex));
        Assert.Null(route.PendingJunctionNodeIndex);
        Assert.Empty(route.Diagnostics);
    }

    [Fact]
    public void StopsWhenConnectedTrackGeometryHasAnExcessiveGap()
    {
        var first = CreatePath(10, new[] { 1, 2 }, 0, 100);
        var disconnectedGeometry = CreatePath(20, new[] { 2, 3 }, 140, 240);
        var network = new TrackNetwork(
            new TrackDatabase(new TrackNode?[] { null }, Array.Empty<string>()),
            new[] { first, disconnectedGeometry },
            Array.Empty<string>());

        var route = new TrackNetworkRouteBuilder().Build(network, 10);

        Assert.Single(route.Segments);
        Assert.Contains(route.Diagnostics, diagnostic => diagnostic.Contains("geometry gap"));
        Assert.Equal(100, route.LengthMeters);
    }

    [Fact]
    public void ExplicitFirstBranchChoiceDoesNotSuppressLaterAmbiguityChecks()
    {
        var first = CreatePath(10, new[] { 1, 2 }, 0, 100);
        var selectedFirstBranch = CreatePath(30, new[] { 2, 3 }, 100, 200);
        var laterBranchA = CreatePath(40, new[] { 3, 4 }, 200, 300);
        var laterBranchB = CreatePath(50, new[] { 3, 5 }, 200, 300);
        var network = new TrackNetwork(
            new TrackDatabase(new TrackNode?[] { null }, Array.Empty<string>()),
            new[] { first, selectedFirstBranch, laterBranchA, laterBranchB },
            Array.Empty<string>());

        var route = new TrackNetworkRouteBuilder().Build(
            network, 10, preferredNextTrackNodeIndex: 30);

        Assert.Equal(new uint[] { 10, 30 },
            route.Segments.Select(segment => segment.Path.TrackNodeIndex));
        Assert.Contains(route.Diagnostics, diagnostic => diagnostic.Contains("ambiguous"));
    }

    [Fact]
    public void StopsAtAmbiguousJunctionWhenEndpointGeometryIsUnavailable()
    {
        var first = CreatePathWithoutGeometry(10, new[] { 1, 2 });
        var branchA = CreatePathWithoutGeometry(20, new[] { 2, 3 });
        var branchB = CreatePathWithoutGeometry(30, new[] { 2, 4 });
        var network = new TrackNetwork(
            new TrackDatabase(new TrackNode?[] { null }, Array.Empty<string>()),
            new[] { first, branchA, branchB },
            Array.Empty<string>());

        var route = new TrackNetworkRouteBuilder().Build(network, 10);

        Assert.Single(route.Segments);
        Assert.Contains(route.Diagnostics, diagnostic =>
            diagnostic.Contains("ambiguous") && diagnostic.Contains("geometry is unavailable"));
    }

    [Fact]
    public void StopsWhenTheOnlyContinuationHasNoEndpointGeometry()
    {
        var first = CreatePath(10, new[] { 1, 2 }, 0, 100);
        var continuation = CreatePathWithoutGeometry(20, new[] { 2, 3 });
        var network = new TrackNetwork(
            new TrackDatabase(new TrackNode?[] { null }, Array.Empty<string>()),
            new[] { first, continuation }, Array.Empty<string>());

        var route = new TrackNetworkRouteBuilder().Build(network, 10);

        Assert.Single(route.Segments);
        Assert.Equal(2, route.PendingJunctionNodeIndex);
        Assert.Contains(route.Diagnostics, diagnostic => diagnostic.Contains("no usable endpoint geometry"));
    }

    [Fact]
    public void StopsAtAmbiguousJunctionWhenOnlyOneBranchHasEndpointGeometry()
    {
        var first = CreatePath(10, new[] { 1, 2 }, 0, 100);
        var branchWithGeometry = CreatePath(20, new[] { 2, 3 }, 100, 200);
        var branchWithoutGeometry = CreatePathWithoutGeometry(30, new[] { 2, 4 });
        var network = new TrackNetwork(
            new TrackDatabase(new TrackNode?[] { null }, Array.Empty<string>()),
            new[] { first, branchWithGeometry, branchWithoutGeometry },
            Array.Empty<string>());

        var route = new TrackNetworkRouteBuilder().Build(network, 10);

        Assert.Single(route.Segments);
        Assert.Contains(route.Diagnostics, diagnostic =>
            diagnostic.Contains("ambiguous") &&
            diagnostic.Contains("geometry is unavailable for at least one branch"));
    }

    [Fact]
    public void StopsWhenThirdJunctionBranchHasNoEndpointGeometry()
    {
        var first = CreatePath(10, new[] { 1, 2 }, 0, 100);
        var nearest = CreatePath(20, new[] { 2, 3 }, 100, 200);
        var secondNearest = CreatePath(30, new[] { 2, 4 }, 105, 205);
        var unknownGeometry = CreatePathWithoutGeometry(40, new[] { 2, 5 });
        var network = new TrackNetwork(
            new TrackDatabase(new TrackNode?[] { null }, Array.Empty<string>()),
            new[] { first, nearest, secondNearest, unknownGeometry },
            Array.Empty<string>());

        var route = new TrackNetworkRouteBuilder().Build(network, 10);

        Assert.Single(route.Segments);
        Assert.Equal(2, route.PendingJunctionNodeIndex);
        Assert.Contains(route.Diagnostics, diagnostic =>
            diagnostic.Contains("ambiguous") &&
            diagnostic.Contains("endpoint geometry is unavailable for at least one branch"));
    }

    [Fact]
    public void DoesNotReportSafetyLimitWhenRouteEndsExactlyAtLimit()
    {
        var terminal = CreatePath(10, new[] { 1, 2 }, 0, 100);
        var network = new TrackNetwork(
            new TrackDatabase(new TrackNode?[] { null }, Array.Empty<string>()),
            new[] { terminal }, Array.Empty<string>());

        var route = new TrackNetworkRouteBuilder().Build(network, 10, maximumSegments: 1);

        Assert.Single(route.Segments);
        Assert.Empty(route.Diagnostics);
    }

    [Fact]
    public void ReportsSafetyLimitOnlyWhenAnUnvisitedContinuationRemains()
    {
        var first = CreatePath(10, new[] { 1, 2 }, 0, 100);
        var second = CreatePath(20, new[] { 2, 3 }, 100, 200);
        var network = new TrackNetwork(
            new TrackDatabase(new TrackNode?[] { null }, Array.Empty<string>()),
            new[] { first, second }, Array.Empty<string>());

        var route = new TrackNetworkRouteBuilder().Build(network, 10, maximumSegments: 1);

        Assert.Single(route.Segments);
        Assert.Contains(route.Diagnostics, diagnostic => diagnostic.Contains("safety limit"));
    }

    [Fact]
    public void RejectsConnectedRouteLengthOverflow()
    {
        var first = CreatePathWithoutGeometry(10, new[] { 1, 2 }) with { };
        var second = CreatePathWithoutGeometry(20, new[] { 2, 3 });
        first = new TrackVectorPath(10, new[] { 1, 2 }, Array.Empty<TrackSectionPlacement>(), double.MaxValue);
        second = new TrackVectorPath(20, new[] { 2, 3 }, Array.Empty<TrackSectionPlacement>(), double.MaxValue);
        var network = new TrackNetwork(
            new TrackDatabase(new TrackNode?[] { null }, Array.Empty<string>()),
            new[] { first, second }, Array.Empty<string>());

        Assert.Throws<InvalidDataException>(() => new TrackNetworkRouteBuilder().Build(
            network, 10, maximumSegments: 2, preferredNextTrackNodeIndex: 20));
    }

    [Fact]
    public void RejectsVectorPathsWithInvalidLengthMetadata()
    {
        var invalid = new TrackVectorPath(10, new[] { 1, 2 },
            Array.Empty<TrackSectionPlacement>(), double.NaN, null);
        var network = new TrackNetwork(
            new TrackDatabase(new TrackNode?[] { null }, Array.Empty<string>()),
            new[] { invalid }, Array.Empty<string>());

        Assert.Throws<InvalidDataException>(() => new TrackNetworkRouteBuilder().Build(network, 10));
    }

    [Fact]
    public void RejectsUnknownStartingVectorNode()
    {
        var network = new TrackNetwork(
            new TrackDatabase(new TrackNode?[] { null }, Array.Empty<string>()),
            Array.Empty<TrackVectorPath>(),
            Array.Empty<string>());

        Assert.Throws<ArgumentException>(() => new TrackNetworkRouteBuilder().Build(network, 999));
    }

    private static TrackVectorPath CreatePathWithoutGeometry(uint id, int[] nodes) =>
        new(id, nodes, Array.Empty<TrackSectionPlacement>(), 100, endPosition: null);

    private static TrackVectorPath CreatePath(uint id, int[] nodes, double startX, double endX)
    {
        var start = new TrackWorldPosition(startX, 0, 0);
        var end = new TrackWorldPosition(endX, 0, 0);
        var section = new TrackSectionPlacement(
            new TrackVectorSection(1, 0, 0, 0, 0, 0, 0, "00", 0, 0,
                (float)startX, 0, 0, 0, 0, 0),
            new TrackSectionGeometry(1, null, (float)Math.Abs(endX - startX), null, null, false),
            start,
            Math.Abs(endX - startX));
        return new TrackVectorPath(id, nodes, new[] { section },
            Math.Abs(endX - startX), end);
    }
}

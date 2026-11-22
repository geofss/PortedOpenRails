namespace OpenRails.Web.Simulation;

/// <summary>
/// A directed view of one original TDB vector node. Forward follows the
/// vector-section order; reverse traverses the same source geometry backwards.
/// </summary>
public sealed record TrackNetworkRouteSegment(TrackVectorPath Path, bool IsReversed)
{
    public int EntryNodeIndex => IsReversed
        ? Path.ConnectedNodeIndices[1]
        : Path.ConnectedNodeIndices[0];

    public int ExitNodeIndex => IsReversed
        ? Path.ConnectedNodeIndices[0]
        : Path.ConnectedNodeIndices[1];

    public double LengthMeters => Path.LengthMeters;
}

public sealed record TrackNetworkRoute(
    IReadOnlyList<TrackNetworkRouteSegment> Segments,
    IReadOnlyList<string> Diagnostics,
    double LengthMeters);

/// <summary>
/// Follows connected vector nodes through TDB pins instead of treating each
/// TrVectorNode as an unrelated route. At junctions, continuation is selected
/// by the closest known endpoint geometry; ambiguous equal choices are reported.
/// </summary>
public sealed class TrackNetworkRouteBuilder
{
    private const double GeometryTieToleranceMeters = 0.25;

    public TrackNetworkRoute Build(
        TrackNetwork network,
        uint startingTrackNodeIndex,
        int maximumSegments = 100_000,
        bool startReversed = false,
        uint? preferredNextTrackNodeIndex = null)
    {
        ArgumentNullException.ThrowIfNull(network);
        if (maximumSegments <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumSegments));

        var byNode = network.VectorPaths.ToDictionary(path => path.TrackNodeIndex);
        if (!byNode.TryGetValue(startingTrackNodeIndex, out var first))
            throw new ArgumentException(
                $"TrackNode {startingTrackNodeIndex} is not a vector path in this network.",
                nameof(startingTrackNodeIndex));
        if (first.ConnectedNodeIndices.Count != 2)
            throw new InvalidDataException(
                $"TrackNode {startingTrackNodeIndex} does not have two connected endpoints.");

        var segments = new List<TrackNetworkRouteSegment>
        {
            new(first, IsReversed: startReversed)
        };
        var diagnostics = new List<string>();
        var visited = new HashSet<uint> { first.TrackNodeIndex };
        var currentNode = segments[0].ExitNodeIndex;
        var currentPosition = GetEndPosition(first, startReversed);
        var preferredContinuationUsed = false;

        while (segments.Count < maximumSegments)
        {
            var candidates = network.VectorPaths
                .Where(path => !visited.Contains(path.TrackNodeIndex) &&
                    path.ConnectedNodeIndices.Count == 2 &&
                    path.ConnectedNodeIndices.Contains(currentNode))
                .Select(path =>
                {
                    var reversed = path.ConnectedNodeIndices[1] == currentNode;
                    var start = GetStartPosition(path, reversed);
                    var gap = currentPosition is null || start is null
                        ? double.PositiveInfinity
                        : Distance(currentPosition, start);
                    return (Path: path, Reversed: reversed, Gap: gap);
                })
                .OrderBy(candidate => candidate.Gap)
                .ThenBy(candidate => candidate.Path.TrackNodeIndex)
                .ToArray();

            if (candidates.Length == 0)
                break;

            if (!preferredContinuationUsed && preferredNextTrackNodeIndex is { } preferredNode)
            {
                var preferredIndex = Array.FindIndex(candidates,
                    candidate => candidate.Path.TrackNodeIndex == preferredNode);
                if (preferredIndex >= 0)
                {
                    var preferred = candidates[preferredIndex];
                    candidates = new[] { preferred }
                        .Concat(candidates.Where((_, index) => index != preferredIndex))
                        .ToArray();
                    preferredContinuationUsed = true;
                }
            }

            if (!preferredContinuationUsed && candidates.Length > 1 &&
                double.IsFinite(candidates[0].Gap) &&
                Math.Abs(candidates[1].Gap - candidates[0].Gap) <= GeometryTieToleranceMeters)
            {
                diagnostics.Add(
                    $"Route traversal stopped at junction node {currentNode}: " +
                    $"continuation is ambiguous between vector nodes " +
                    $"{candidates[0].Path.TrackNodeIndex} and {candidates[1].Path.TrackNodeIndex}.");
                break;
            }

            var next = candidates[0];
            if (double.IsFinite(next.Gap) && next.Gap > 25)
            {
                diagnostics.Add(
                    $"Route traversal stopped at node {currentNode}: nearest connected vector " +
                    $"{next.Path.TrackNodeIndex} has a {next.Gap:N1} m geometry gap.");
                break;
            }

            var segment = new TrackNetworkRouteSegment(next.Path, next.Reversed);
            segments.Add(segment);
            visited.Add(next.Path.TrackNodeIndex);
            currentNode = segment.ExitNodeIndex;
            currentPosition = GetEndPosition(next.Path, next.Reversed);
        }

        if (segments.Count == maximumSegments)
            diagnostics.Add($"Route traversal reached the safety limit of {maximumSegments} vector nodes.");

        var length = segments.Sum(segment => segment.LengthMeters);
        return new TrackNetworkRoute(
            Array.AsReadOnly(segments.ToArray()),
            Array.AsReadOnly(diagnostics.ToArray()),
            length);
    }

    private static TrackWorldPosition? GetStartPosition(TrackVectorPath path, bool reversed) =>
        reversed ? path.EndPosition : path.Sections.FirstOrDefault()?.Start;

    private static TrackWorldPosition? GetEndPosition(TrackVectorPath path, bool reversed) =>
        reversed ? path.Sections.FirstOrDefault()?.Start : path.EndPosition;

    private static double Distance(TrackWorldPosition a, TrackWorldPosition b)
    {
        var x = a.X - b.X;
        var y = a.Y - b.Y;
        var z = a.Z - b.Z;
        return Math.Sqrt(x * x + y * y + z * z);
    }
}

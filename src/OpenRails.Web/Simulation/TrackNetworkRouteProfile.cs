using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

public sealed record TrackRouteProfileSample(
    TrackWorldPosition Position,
    double GradeRadians,
    double HeadingRadians);

/// <summary>
/// Builds a distance-indexed elevation profile from connected track geometry.
/// Grade is positive downhill in the route's forward direction, matching the
/// sign convention used by TrainCarResistanceModel.
/// </summary>
public sealed class TrackNetworkRouteProfile
{
    private readonly TrackWorldPosition[] points;
    private readonly double[] cumulativeDistances;
    private readonly IReadOnlyDictionary<uint, TrackRouteSegmentProfile> segmentProfiles;

    public double LengthMeters => cumulativeDistances[^1];
    public int PointCount => points.Length;

    private TrackNetworkRouteProfile(
        TrackWorldPosition[] points,
        double[] cumulativeDistances,
        IReadOnlyDictionary<uint, TrackRouteSegmentProfile> segmentProfiles)
    {
        this.points = points;
        this.cumulativeDistances = cumulativeDistances;
        this.segmentProfiles = segmentProfiles;
    }

    public static TrackNetworkRouteProfile Build(
        TrackNetworkRoute route,
        double maximumSegmentLengthMeters = 10)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (!double.IsFinite(maximumSegmentLengthMeters) || maximumSegmentLengthMeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumSegmentLengthMeters));

        var tessellator = new TrackGeometryTessellator();
        var samples = new List<TrackWorldPosition>();
        var segmentProfiles = new Dictionary<uint, TrackRouteSegmentProfile>();
        var profileDistance = 0d;
        foreach (var segment in route.Segments)
        {
            var segmentPoints = tessellator.BuildPolyline(
                segment.Path, maximumSegmentLengthMeters);
            if (segment.IsReversed)
                segmentPoints = segmentPoints.Reverse().ToArray();
            if (segmentPoints.Count < 2)
                continue;

            if (samples.Count > 0)
            {
                var gap = Distance(samples[^1], segmentPoints[0]);
                if (gap > 25)
                    break;
                if (gap > 1e-6)
                {
                    samples.Add(segmentPoints[0]);
                    profileDistance += TrackDistance(samples[^2], samples[^1]);
                }
            }
            else
            {
                samples.Add(segmentPoints[0]);
            }

            var segmentStartDistance = profileDistance;
            for (var i = 1; i < segmentPoints.Count; i++)
            {
                samples.Add(segmentPoints[i]);
                profileDistance += TrackDistance(segmentPoints[i - 1], segmentPoints[i]);
            }

            var profileLength = profileDistance - segmentStartDistance;
            if (segment.Path.LengthMeters > 0 && profileLength > 0)
                segmentProfiles[segment.Path.TrackNodeIndex] = new TrackRouteSegmentProfile(
                    segmentStartDistance, profileLength, segment.Path.LengthMeters, segment.IsReversed);
        }

        if (samples.Count < 2)
            throw new ArgumentException("Connected route has insufficient geometry to build an elevation profile.", nameof(route));

        var points = samples.ToArray();
        var cumulative = new double[points.Length];
        for (var i = 1; i < points.Length; i++)
            cumulative[i] = cumulative[i - 1] + TrackDistance(points[i - 1], points[i]);

        if (!double.IsFinite(cumulative[^1]) || cumulative[^1] <= 0)
            throw new InvalidDataException("Connected route profile has no measurable length.");

        return new TrackNetworkRouteProfile(points, cumulative, segmentProfiles);
    }

    /// <summary>Maps a vector-node-local signal position onto this directed route profile.</summary>
    public bool TryGetRouteDistanceForTrackNode(
        uint trackNodeIndex,
        double distanceAlongTrackMeters,
        out double routeDistanceMeters)
    {
        routeDistanceMeters = 0;
        if (!double.IsFinite(distanceAlongTrackMeters) ||
            !segmentProfiles.TryGetValue(trackNodeIndex, out var segment) ||
            segment.SourceLengthMeters <= 0)
            return false;

        var localDistance = Math.Clamp(distanceAlongTrackMeters, 0, segment.SourceLengthMeters);
        var fraction = localDistance / segment.SourceLengthMeters;
        if (segment.IsReversed)
            fraction = 1 - fraction;
        routeDistanceMeters = segment.StartDistanceMeters + fraction * segment.ProfileLengthMeters;
        return true;
    }

    /// <summary>Finds the nearest point on the connected route profile to a world-space location.</summary>
    public bool TryFindNearestDistance(
        TrackWorldPosition target,
        double maximumOffsetMeters,
        out double routeDistanceMeters,
        out double offsetMeters)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!double.IsFinite(target.X) || !double.IsFinite(target.Y) || !double.IsFinite(target.Z))
            throw new ArgumentOutOfRangeException(nameof(target), "Target position must be finite.");
        if (!double.IsFinite(maximumOffsetMeters) || maximumOffsetMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumOffsetMeters));

        var bestSquared = double.PositiveInfinity;
        var bestDistance = 0d;
        for (var index = 1; index < points.Length; index++)
        {
            var start = points[index - 1];
            var end = points[index];
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var dz = end.Z - start.Z;
            var lengthSquared = dx * dx + dy * dy + dz * dz;
            var fraction = lengthSquared <= 1e-12
                ? 0
                : Math.Clamp(((target.X - start.X) * dx +
                    (target.Y - start.Y) * dy + (target.Z - start.Z) * dz) / lengthSquared, 0, 1);
            var offsetX = target.X - (start.X + dx * fraction);
            var offsetY = target.Y - (start.Y + dy * fraction);
            var offsetZ = target.Z - (start.Z + dz * fraction);
            var squared = offsetX * offsetX + offsetY * offsetY + offsetZ * offsetZ;
            if (squared < bestSquared)
            {
                bestSquared = squared;
                bestDistance = cumulativeDistances[index - 1] +
                    (cumulativeDistances[index] - cumulativeDistances[index - 1]) * fraction;
            }
        }

        offsetMeters = Math.Sqrt(bestSquared);
        routeDistanceMeters = bestDistance;
        return offsetMeters <= maximumOffsetMeters;
    }

    public TrackRouteProfileSample Sample(double distanceMeters)
    {
        if (!double.IsFinite(distanceMeters))
            throw new ArgumentOutOfRangeException(nameof(distanceMeters));
        var distance = Math.Clamp(distanceMeters, 0, LengthMeters);
        var upper = Array.BinarySearch(cumulativeDistances, distance);
        if (upper < 0)
            upper = ~upper;
        upper = Math.Clamp(upper, 1, points.Length - 1);
        var lower = upper - 1;

        var start = points[lower];
        var end = points[upper];
        var segmentLength = cumulativeDistances[upper] - cumulativeDistances[lower];
        var fraction = segmentLength <= 1e-9
            ? 0
            : (distance - cumulativeDistances[lower]) / segmentLength;
        var position = new TrackWorldPosition(
            start.X + (end.X - start.X) * fraction,
            start.Y + (end.Y - start.Y) * fraction,
            start.Z + (end.Z - start.Z) * fraction);
        var deltaX = end.X - start.X;
        var deltaY = end.Y - start.Y;
        var deltaZ = end.Z - start.Z;
        var horizontalLength = Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
        var grade = -Math.Atan2(deltaY, horizontalLength);
        var heading = Math.Atan2(deltaX, deltaZ);
        return new TrackRouteProfileSample(position, grade, heading);
    }

    private sealed record TrackRouteSegmentProfile(
        double StartDistanceMeters, double ProfileLengthMeters,
        double SourceLengthMeters, bool IsReversed);

    private static double TrackDistance(TrackWorldPosition a, TrackWorldPosition b)
    {
        var x = a.X - b.X;
        var z = a.Z - b.Z;
        return Math.Sqrt(x * x + z * z);
    }

    private static double Distance(TrackWorldPosition a, TrackWorldPosition b)
    {
        var x = a.X - b.X;
        var y = a.Y - b.Y;
        var z = a.Z - b.Z;
        return Math.Sqrt(x * x + y * y + z * z);
    }
}

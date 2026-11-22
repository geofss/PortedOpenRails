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

    public double LengthMeters => cumulativeDistances[^1];
    public int PointCount => points.Length;

    private TrackNetworkRouteProfile(
        TrackWorldPosition[] points,
        double[] cumulativeDistances)
    {
        this.points = points;
        this.cumulativeDistances = cumulativeDistances;
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
                    samples.Add(segmentPoints[0]);
            }
            else
            {
                samples.Add(segmentPoints[0]);
            }

            for (var i = 1; i < segmentPoints.Count; i++)
                samples.Add(segmentPoints[i]);
        }

        if (samples.Count < 2)
            throw new ArgumentException("Connected route has insufficient geometry to build an elevation profile.", nameof(route));

        var points = samples.ToArray();
        var cumulative = new double[points.Length];
        for (var i = 1; i < points.Length; i++)
            cumulative[i] = cumulative[i - 1] + TrackDistance(points[i - 1], points[i]);

        if (!double.IsFinite(cumulative[^1]) || cumulative[^1] <= 0)
            throw new InvalidDataException("Connected route profile has no measurable length.");

        return new TrackNetworkRouteProfile(points, cumulative);
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

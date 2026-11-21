namespace OpenRails.Web.Simulation;

using OpenRails.Web.Parsing;

/// <summary>Ordered geometry path built from parsed track-section definitions.</summary>
public sealed class TrackPath
{
    private readonly TrackSectionGeometry[] sections;
    private readonly double[] cumulative;
    public double LengthMeters { get; }
    public int SegmentCount => sections.Length;

    public TrackPath(IEnumerable<TrackSectionGeometry> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        this.sections = sections.ToArray();
        if (this.sections.Length == 0) throw new ArgumentException("A track path needs at least one section.", nameof(sections));
        cumulative = new double[this.sections.Length + 1];
        for (int i = 0; i < this.sections.Length; i++)
        {
            var length = this.sections[i].CalculatedLengthMeters;
            if (!double.IsFinite(length) || length <= 0) throw new ArgumentException("Track sections must have positive finite length.", nameof(sections));
            cumulative[i + 1] = cumulative[i] + length;
        }
        LengthMeters = cumulative[^1];
    }

    public TrackPathPoint GetPointAtDistance(double distanceMeters)
    {
        if (!double.IsFinite(distanceMeters)) throw new ArgumentOutOfRangeException(nameof(distanceMeters));
        var d = Math.Clamp(distanceMeters, 0, LengthMeters);
        var index = Array.BinarySearch(cumulative, d);
        if (index < 0) index = ~index - 1;
        index = Math.Clamp(index, 0, sections.Length - 1);
        var local = d - cumulative[index];
        var section = sections[index];
        double x = 0, z = 0, heading = 0;
        for (int i = 0; i < index; i++)
        {
            var end = sections[i].GetEndOffset();
            var rotated = Rotate(end.X, end.Z, heading);
            x += rotated.X; z += rotated.Z;
            heading += CurvatureHeading(sections[i]);
        }
        if (section.IsStraight)
        {
            var offset = Rotate(0, local, heading);
            x += offset.X; z += offset.Z;
        }
        else
        {
            var radius = section.RadiusMeters!.Value;
            var angle = Math.Clamp(local / radius, 0, Math.Abs(section.AngleDegrees!.Value) * Math.PI / 180d);
            if (section.AngleDegrees < 0) angle = -angle;
            var offset = Rotate(radius * (1 - Math.Cos(angle)), radius * Math.Sin(angle), heading);
            x += offset.X; z += offset.Z;
            heading += angle;
        }
        return new TrackPathPoint(d, x, z, heading);
    }

    private static double CurvatureHeading(TrackSectionGeometry s) => s.IsStraight ? 0 : Math.Sign(s.AngleDegrees!.Value) * Math.Abs(s.AngleDegrees.Value) * Math.PI / 180d;
    private static (double X,double Z) Rotate(double x,double z,double heading) => (x*Math.Cos(heading)+z*Math.Sin(heading), -x*Math.Sin(heading)+z*Math.Cos(heading));
}

public sealed record TrackPathPoint(double DistanceMeters, double X, double Z, double HeadingRadians);

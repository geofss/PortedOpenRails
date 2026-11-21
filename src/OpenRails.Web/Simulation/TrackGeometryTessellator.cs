// COPYRIGHT 2009-2014 by the Open Rails project.
// This file is part of Open Rails and is licensed under GPL-3.0-or-later.
// Browser-port geometry sampling follows the original TrackViewer angle convention.

namespace OpenRails.Web.Simulation;

/// <summary>
/// Samples original MSTS track-section geometry into world-space centerline points.
/// AY is the Open Rails heading in radians, measured right of North.
/// </summary>
public sealed class TrackGeometryTessellator
{
    public IReadOnlyList<TrackWorldPosition> BuildPolyline(
        TrackVectorPath path,
        double maximumSegmentLengthMeters = 5,
        int maximumPointCount = 2_000_000)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!double.IsFinite(maximumSegmentLengthMeters) || maximumSegmentLengthMeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumSegmentLengthMeters));
        if (maximumPointCount < 2)
            throw new ArgumentOutOfRangeException(nameof(maximumPointCount));

        var points = new List<TrackWorldPosition>();
        for (var sectionIndex = 0; sectionIndex < path.Sections.Count; sectionIndex++)
        {
            var section = path.Sections[sectionIndex];
            var geometry = section.Geometry;
            var requestedSteps = Math.Ceiling(section.LengthMeters / maximumSegmentLengthMeters);
            if (!double.IsFinite(requestedSteps) || requestedSteps > maximumPointCount - points.Count - 1)
                throw new InvalidDataException($"Track path {path.TrackNodeIndex} exceeds the {maximumPointCount}-point geometry limit.");
            var steps = Math.Max(1, (int)requestedSteps);

            TrackWorldPosition? target = sectionIndex + 1 < path.Sections.Count
                ? path.Sections[sectionIndex + 1].Start
                : path.EndPosition;

            for (var step = sectionIndex == 0 ? 0 : 1; step <= steps; step++)
            {
                var fraction = (double)step / steps;
                var distance = section.LengthMeters * fraction;
                var point = Evaluate(section, distance);

                if (target is not null)
                {
                    point = point with
                    {
                        Y = section.Start.Y + (target.Y - section.Start.Y) * fraction
                    };

                    if (step == steps)
                        point = target;
                }

                points.Add(point);
            }
        }

        return Array.AsReadOnly(points.ToArray());
    }

    private static TrackWorldPosition Evaluate(TrackSectionPlacement section, double distance)
    {
        var start = section.Start;
        var heading = section.Source.AY;

        if (section.Geometry.IsStraight)
        {
            return new TrackWorldPosition(
                start.X + distance * Math.Sin(heading),
                start.Y,
                start.Z + distance * Math.Cos(heading));
        }

        var radius = section.Geometry.RadiusMeters!.Value;
        var signedAngle = section.Geometry.AngleDegrees!.Value * Math.PI / 180d;
        var direction = Math.Sign(signedAngle);
        var delta = direction * distance / radius;
        return new TrackWorldPosition(
            start.X + direction * radius * (Math.Cos(heading) - Math.Cos(heading + delta)),
            start.Y,
            start.Z + direction * radius * (Math.Sin(heading + delta) - Math.Sin(heading)));
    }
}

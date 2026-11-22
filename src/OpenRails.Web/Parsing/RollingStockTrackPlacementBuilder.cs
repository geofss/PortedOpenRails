// COPYRIGHT 2009-2014 by the Open Rails project.
// Browser-port adaptation places original consist vehicles along route TDB geometry.
// Licensed under GPL-3.0-or-later.

using System.Numerics;
using OpenRails.Web.Simulation;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Places each consist vehicle by arc distance along a real route vector path.
/// Vehicle geometry stays reusable; each result carries a world transform aligned
/// to the route centerline and respects the consist's Flip flag.
/// </summary>
public sealed class RollingStockTrackPlacementBuilder
{
    public RollingStockTrackPlacementResult Build(
        ConsistMetadata consist,
        IReadOnlyList<ConsistCarAssetReference> carAssets,
        IReadOnlyDictionary<int, TrackShapeAssetMesh> meshesByUid,
        TrackVectorPath path,
        double maximumSegmentLengthMeters = 2,
        double startDistanceMeters = 0)
    {
        ArgumentNullException.ThrowIfNull(consist);
        ArgumentNullException.ThrowIfNull(carAssets);
        ArgumentNullException.ThrowIfNull(meshesByUid);
        ArgumentNullException.ThrowIfNull(path);
        if (!double.IsFinite(maximumSegmentLengthMeters) || maximumSegmentLengthMeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumSegmentLengthMeters));
        if (!double.IsFinite(startDistanceMeters) || startDistanceMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(startDistanceMeters));

        var centerline = new TrackGeometryTessellator().BuildPolyline(path, maximumSegmentLengthMeters);
        if (centerline.Count < 2)
            return new RollingStockTrackPlacementResult(
                Array.Empty<TrackShapePlacement>(),
                new[] { $"TrackNode {path.TrackNodeIndex} has insufficient centerline geometry for rolling stock." },
                0);

        var cumulative = new double[centerline.Count];
        for (var i = 1; i < centerline.Count; i++)
        {
            var dx = centerline[i].X - centerline[i - 1].X;
            var dy = centerline[i].Y - centerline[i - 1].Y;
            var dz = centerline[i].Z - centerline[i - 1].Z;
            cumulative[i] = cumulative[i - 1] + Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        var availableLength = cumulative[^1];
        var placements = new List<TrackShapePlacement>();
        var issues = new List<string>();
        var cursor = startDistanceMeters;

        foreach (var car in consist.Cars.Where(car => !car.IsEndOfTrain))
        {
            var reference = carAssets.FirstOrDefault(asset =>
                asset.ConsistName == consist.Name && asset.Uid == car.Uid);
            var length = reference?.Metadata?.LengthM ?? 10f;
            if (!float.IsFinite(length) || length <= 0)
            {
                issues.Add($"{car.Name}: invalid vehicle length; using 10 m for consist spacing.");
                length = 10;
            }

            var centerDistance = cursor + length / 2d;
            cursor += length;

            if (centerDistance > availableLength)
            {
                issues.Add($"{car.Name}: route path is too short to place this vehicle (needs {centerDistance:N1} m, has {availableLength:N1} m).");
                continue;
            }

            if (!meshesByUid.TryGetValue(car.Uid, out var asset))
            {
                issues.Add($"{car.Name}: original shape mesh is unavailable; its consist position was retained.");
                continue;
            }

            var sample = Sample(centerline, cumulative, centerDistance);
            var heading = Math.Atan2(sample.TangentX, sample.TangentZ);
            if (car.Flipped)
                heading += Math.PI;

            var position = sample.Position;
            var translation = Matrix4x4.CreateTranslation(
                (float)position.X, (float)position.Y, (float)position.Z);
            var rotation = Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, (float)heading);
            var sectionIndex = ResolveSectionIndex(path, centerDistance);

            placements.Add(new TrackShapePlacement(
                path.TrackNodeIndex,
                sectionIndex,
                asset.ShapeIndex,
                position,
                heading,
                asset,
                rotation * translation));
        }

        return new RollingStockTrackPlacementResult(
            Array.AsReadOnly(placements.ToArray()),
            Array.AsReadOnly(issues.ToArray()),
            Math.Min(Math.Max(0, cursor - startDistanceMeters), Math.Max(0, availableLength - startDistanceMeters)));
    }

    private static (TrackWorldPosition Position, double TangentX, double TangentZ) Sample(
        IReadOnlyList<TrackWorldPosition> points,
        IReadOnlyList<double> cumulative,
        double distance)
    {
        var index = 0;
        while (index < cumulative.Count - 2 && cumulative[index + 1] <= distance)
            index++;
        while (index < cumulative.Count - 2 &&
            cumulative[index + 1] - cumulative[index] <= 1e-9)
            index++;

        var segmentLength = cumulative[index + 1] - cumulative[index];
        if (segmentLength <= 1e-9)
        {
            index = cumulative.Count - 2;
            while (index > 0 && cumulative[index + 1] - cumulative[index] <= 1e-9)
                index--;
            segmentLength = cumulative[index + 1] - cumulative[index];
        }
        if (segmentLength <= 1e-9)
            throw new InvalidDataException("Route centerline contains no usable non-zero-length segment.");
        var fraction = Math.Clamp((distance - cumulative[index]) / segmentLength, 0, 1);
        var a = points[index];
        var b = points[index + 1];
        var dx = b.X - a.X;
        var dz = b.Z - a.Z;
        var dy = b.Y - a.Y;
        var magnitude = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (magnitude <= 1e-9)
            throw new InvalidDataException("Route centerline contains a zero-length segment.");

        return (
            new TrackWorldPosition(
                a.X + dx * fraction,
                a.Y + dy * fraction,
                a.Z + dz * fraction),
            dx / magnitude,
            dz / magnitude);
    }

    private static uint ResolveSectionIndex(TrackVectorPath path, double distance)
    {
        var traversed = 0d;
        foreach (var section in path.Sections)
        {
            traversed += section.LengthMeters;
            if (distance <= traversed)
                return section.Source.SectionIndex;
        }
        return path.Sections.Count == 0 ? 0 : path.Sections[^1].Source.SectionIndex;
    }
}

public sealed record RollingStockTrackPlacementResult(
    IReadOnlyList<TrackShapePlacement> Placements,
    IReadOnlyList<string> Issues,
    double UsedLengthMeters);

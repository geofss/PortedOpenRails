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

        var cumulative = BuildTrackChainage(path, maximumSegmentLengthMeters, centerline.Count);
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

    /// <summary>
    /// Builds render placements from per-car route distances maintained by the
    /// multi-car simulation loop. This is the bridge for animated route movement.
    /// </summary>
    public RollingStockTrackPlacementResult BuildAtMotionState(
        ConsistMetadata consist,
        IReadOnlyList<ConsistCarAssetReference> carAssets,
        IReadOnlyDictionary<int, TrackShapeAssetMesh> meshesByUid,
        TrackVectorPath path,
        TrainConsistMotionState motionState,
        double maximumSegmentLengthMeters = 2)
    {
        ArgumentNullException.ThrowIfNull(consist);
        ArgumentNullException.ThrowIfNull(carAssets);
        ArgumentNullException.ThrowIfNull(meshesByUid);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(motionState);
        if (!double.IsFinite(maximumSegmentLengthMeters) || maximumSegmentLengthMeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumSegmentLengthMeters));

        var cars = consist.Cars.Where(car => !car.IsEndOfTrain).ToArray();
        if (cars.Length != motionState.Cars.Count)
            throw new ArgumentException("The motion state must contain one entry for every non-EOT consist car.", nameof(motionState));

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
        var minimumDistance = double.PositiveInfinity;
        var maximumDistance = double.NegativeInfinity;

        for (var i = 0; i < cars.Length; i++)
        {
            var car = cars[i];
            var distance = motionState.Cars[i].DistanceMeters;
            if (!double.IsFinite(distance) || distance < 0 || distance > availableLength)
            {
                issues.Add($"{car.Name}: simulated route distance {distance:N1} m is outside the available path (0–{availableLength:N1} m).");
                continue;
            }

            var reference = carAssets.FirstOrDefault(asset =>
                asset.ConsistName == consist.Name && asset.Uid == car.Uid);
            if (reference?.ShapeFound != true || !meshesByUid.TryGetValue(car.Uid, out var asset))
            {
                issues.Add($"{car.Name}: original shape mesh is unavailable at its simulated position.");
                continue;
            }

            var sample = Sample(centerline, cumulative, distance);
            var heading = Math.Atan2(sample.TangentX, sample.TangentZ);
            if (car.Flipped)
                heading += Math.PI;

            var position = sample.Position;
            var translation = Matrix4x4.CreateTranslation(
                (float)position.X, (float)position.Y, (float)position.Z);
            var rotation = Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, (float)heading);
            placements.Add(new TrackShapePlacement(
                path.TrackNodeIndex,
                ResolveSectionIndex(path, distance),
                asset.ShapeIndex,
                position,
                heading,
                asset,
                rotation * translation));

            minimumDistance = Math.Min(minimumDistance, distance);
            maximumDistance = Math.Max(maximumDistance, distance);
        }

        var usedLength = double.IsFinite(minimumDistance) && double.IsFinite(maximumDistance)
            ? maximumDistance - minimumDistance
            : 0;
        return new RollingStockTrackPlacementResult(
            Array.AsReadOnly(placements.ToArray()),
            Array.AsReadOnly(issues.ToArray()),
            usedLength);
    }


    /// <summary>
    /// Places a consist from the start of a connected multi-node route, preserving
    /// each vehicle's metadata length when calculating its initial center distance.
    /// </summary>
    public RollingStockTrackPlacementResult Build(
        ConsistMetadata consist,
        IReadOnlyList<ConsistCarAssetReference> carAssets,
        IReadOnlyDictionary<int, TrackShapeAssetMesh> meshesByUid,
        TrackNetworkRoute route,
        double maximumSegmentLengthMeters = 2,
        double startDistanceMeters = 0)
    {
        ArgumentNullException.ThrowIfNull(consist);
        ArgumentNullException.ThrowIfNull(carAssets);
        ArgumentNullException.ThrowIfNull(meshesByUid);
        ArgumentNullException.ThrowIfNull(route);
        if (!double.IsFinite(startDistanceMeters) || startDistanceMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(startDistanceMeters));

        var cars = consist.Cars.Where(car => !car.IsEndOfTrain).ToArray();
        var states = new List<TrainCarMotionState>(cars.Length);
        var cursor = startDistanceMeters;
        foreach (var car in cars)
        {
            var reference = carAssets.FirstOrDefault(asset =>
                asset.ConsistName == consist.Name && asset.Uid == car.Uid);
            var length = reference?.Metadata?.LengthM ?? 10f;
            if (!float.IsFinite(length) || length <= 0)
                length = 10;
            var mass = reference?.Metadata?.MassKg ?? 10_000f;
            if (!float.IsFinite(mass) || mass <= 0)
                mass = 10_000;
            states.Add(new TrainCarMotionState(mass, 0, cursor + length / 2d));
            cursor += length;
        }

        return BuildAtMotionState(
            consist, carAssets, meshesByUid, route,
            new TrainConsistMotionState(states), maximumSegmentLengthMeters);
    }

    /// <summary>
    /// Places vehicles on an ordered multi-node route assembled from connected
    /// TDB vector nodes. Reversed segments are sampled in reverse centerline order.
    /// </summary>
    public RollingStockTrackPlacementResult BuildAtMotionState(
        ConsistMetadata consist,
        IReadOnlyList<ConsistCarAssetReference> carAssets,
        IReadOnlyDictionary<int, TrackShapeAssetMesh> meshesByUid,
        TrackNetworkRoute route,
        TrainConsistMotionState motionState,
        double maximumSegmentLengthMeters = 2)
    {
        ArgumentNullException.ThrowIfNull(consist);
        ArgumentNullException.ThrowIfNull(carAssets);
        ArgumentNullException.ThrowIfNull(meshesByUid);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(motionState);
        if (!double.IsFinite(maximumSegmentLengthMeters) || maximumSegmentLengthMeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumSegmentLengthMeters));

        var cars = consist.Cars.Where(car => !car.IsEndOfTrain).ToArray();
        if (cars.Length != motionState.Cars.Count)
            throw new ArgumentException("The motion state must contain one entry for every non-EOT consist car.", nameof(motionState));

        var centerline = new List<TrackWorldPosition>();
        var segmentRanges = new List<(TrackNetworkRouteSegment Segment, double Start, double End)>();
        var tessellator = new TrackGeometryTessellator();
        var distanceCursor = 0d;

        foreach (var segment in route.Segments)
        {
            var points = tessellator.BuildPolyline(segment.Path, maximumSegmentLengthMeters);
            if (segment.IsReversed)
                points = points.Reverse().ToArray();
            if (points.Count < 2)
                continue;

            if (centerline.Count > 0)
            {
                var previous = centerline[^1];
                var next = points[0];
                var gap = Distance(previous, next);
                if (gap > 25)
                    break;
                if (gap > 1e-6)
                {
                    centerline.Add(next);
                    distanceCursor += TrackDistance(previous, next);
                }
            }
            else
            {
                centerline.Add(points[0]);
            }

            var segmentStart = distanceCursor;
            for (var i = 1; i < points.Count; i++)
            {
                distanceCursor += TrackDistance(points[i - 1], points[i]);
                centerline.Add(points[i]);
            }
            segmentRanges.Add((segment, segmentStart, distanceCursor));
        }

        if (centerline.Count < 2)
            return new RollingStockTrackPlacementResult(
                Array.Empty<TrackShapePlacement>(),
                new[] { "Connected route has insufficient centerline geometry for rolling stock." },
                0);

        var cumulative = new double[centerline.Count];
        for (var i = 1; i < centerline.Count; i++)
            cumulative[i] = cumulative[i - 1] + TrackDistance(centerline[i - 1], centerline[i]);

        var availableLength = cumulative[^1];
        var placements = new List<TrackShapePlacement>();
        var issues = new List<string>();
        var minimumDistance = double.PositiveInfinity;
        var maximumDistance = double.NegativeInfinity;

        for (var i = 0; i < cars.Length; i++)
        {
            var car = cars[i];
            var distance = motionState.Cars[i].DistanceMeters;
            if (!double.IsFinite(distance) || distance < 0 || distance > availableLength)
            {
                issues.Add($"{car.Name}: simulated route distance {distance:N1} m is outside the connected route (0–{availableLength:N1} m).");
                continue;
            }

            var reference = carAssets.FirstOrDefault(asset =>
                asset.ConsistName == consist.Name && asset.Uid == car.Uid);
            if (reference?.ShapeFound != true || !meshesByUid.TryGetValue(car.Uid, out var asset))
            {
                issues.Add($"{car.Name}: original shape mesh is unavailable at its simulated position.");
                continue;
            }

            var sample = Sample(centerline, cumulative, distance);
            var routeSegment = segmentRanges.LastOrDefault(range => distance >= range.Start && distance <= range.End);
            if (routeSegment.Segment is null)
            {
                issues.Add($"{car.Name}: no source vector node owns route distance {distance:N1} m.");
                continue;
            }

            var localDistance = routeSegment.Segment.IsReversed
                ? routeSegment.End - distance
                : distance - routeSegment.Start;
            var sectionIndex = routeSegment.Segment.IsReversed
                ? ResolveSectionIndexReversed(routeSegment.Segment.Path, localDistance)
                : ResolveSectionIndex(routeSegment.Segment.Path, localDistance);
            var heading = Math.Atan2(sample.TangentX, sample.TangentZ);
            if (car.Flipped)
                heading += Math.PI;

            var position = sample.Position;
            var translation = Matrix4x4.CreateTranslation(
                (float)position.X, (float)position.Y, (float)position.Z);
            var rotation = Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, (float)heading);
            placements.Add(new TrackShapePlacement(
                routeSegment.Segment.Path.TrackNodeIndex,
                sectionIndex,
                asset.ShapeIndex,
                position,
                heading,
                asset,
                rotation * translation));

            minimumDistance = Math.Min(minimumDistance, distance);
            maximumDistance = Math.Max(maximumDistance, distance);
        }

        var usedLength = double.IsFinite(minimumDistance) && double.IsFinite(maximumDistance)
            ? maximumDistance - minimumDistance
            : 0;
        return new RollingStockTrackPlacementResult(
            Array.AsReadOnly(placements.ToArray()),
            Array.AsReadOnly(issues.ToArray()),
            usedLength);
    }

    private static double[] BuildTrackChainage(
        TrackVectorPath path,
        double maximumSegmentLengthMeters,
        int pointCount)
    {
        var cumulative = new double[pointCount];
        var pointIndex = 0;
        var routeDistance = 0d;
        for (var sectionIndex = 0; sectionIndex < path.Sections.Count; sectionIndex++)
        {
            var section = path.Sections[sectionIndex];
            var steps = Math.Max(1, (int)Math.Ceiling(section.LengthMeters / maximumSegmentLengthMeters));
            for (var step = sectionIndex == 0 ? 0 : 1; step <= steps && pointIndex < pointCount; step++)
                cumulative[pointIndex++] = routeDistance + section.LengthMeters * step / steps;
            routeDistance += section.LengthMeters;
        }
        if (pointIndex != pointCount)
            throw new InvalidDataException($"Track path {path.TrackNodeIndex} tessellation produced {pointCount} points, but section chainage described {pointIndex}.");
        return cumulative;
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

    private static uint ResolveSectionIndexReversed(TrackVectorPath path, double distance)
    {
        var traversed = 0d;
        for (var i = path.Sections.Count - 1; i >= 0; i--)
        {
            var section = path.Sections[i];
            traversed += section.LengthMeters;
            if (distance <= traversed)
                return section.Source.SectionIndex;
        }
        return path.Sections.Count == 0 ? 0 : path.Sections[0].Source.SectionIndex;
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

// COPYRIGHT 2009-2014 by the Open Rails project.
// This file is part of Open Rails and is licensed under GPL-3.0-or-later.
// Browser-port adaptation creates world transforms for original route track-shape assets.

using System.Numerics;
using OpenRails.Web.Simulation;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Resolves original track-shape assets onto TDB vector-section anchors.
/// This preserves the source section's world position and AY heading and keeps
/// mesh data reusable for GPU rendering.
/// </summary>
public sealed class TrackShapePlacementBuilder
{
    public IReadOnlyList<TrackShapePlacement> Build(
        TrackNetwork network,
        IEnumerable<TrackShapeAssetMesh> loadedMeshes)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(loadedMeshes);

        var meshes = loadedMeshes
            .GroupBy(mesh => mesh.ShapeIndex)
            .ToDictionary(group => group.Key, group => group.First());

        var placements = new List<TrackShapePlacement>();
        foreach (var path in network.VectorPaths)
        foreach (var section in path.Sections)
        {
            if (section.Shape is null || !meshes.TryGetValue(section.Shape.ShapeIndex, out var asset))
                continue;

            var heading = section.Source.AY;
            if (!double.IsFinite(heading))
                throw new InvalidDataException($"TrackNode {path.TrackNodeIndex} has an invalid shape heading.");

            // The track-section anchor is authoritative. AY is the MSTS/Open Rails
            // heading (radians, right of North); mesh local coordinates use the
            // converted Open Rails shape coordinate convention.
            var position = new Vector3(
                (float)section.Start.X,
                (float)section.Start.Y,
                (float)section.Start.Z);
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)heading);
            var transform = Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
            placements.Add(new TrackShapePlacement(
                path.TrackNodeIndex,
                section.Source.SectionIndex,
                section.Shape.ShapeIndex,
                section.Start,
                heading,
                asset,
                transform));
        }

        return Array.AsReadOnly(placements.ToArray());
    }
}

public sealed record TrackShapePlacement(
    uint TrackNodeIndex,
    uint TrackSectionIndex,
    uint ShapeIndex,
    TrackWorldPosition Anchor,
    double HeadingRadians,
    TrackShapeAssetMesh Asset,
    Matrix4x4 WorldTransform,
    int? VehicleUid = null,
    double RouteDistanceMeters = 0,
    IReadOnlyDictionary<int, Matrix4x4>? RuntimeMatrixOverrides = null);

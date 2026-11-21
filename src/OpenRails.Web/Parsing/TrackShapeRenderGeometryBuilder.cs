using System.Numerics;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Expands original Open Rails track-shape primitives into world-space triangle
/// geometry. Matrix conversion and parent traversal follow Viewer3D/Shapes.cs.
/// </summary>
public sealed class TrackShapeRenderGeometryBuilder
{
    private const int MaximumVertices = 500_000;
    private const int MaximumIndices = 1_500_000;

    public TrackShapeRenderGeometry Build(IEnumerable<TrackShapePlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(placements);
        var vertices = new List<Vector3>();
        var indices = new List<int>();

        foreach (var placement in placements)
        {
            var mesh = placement.Asset.Mesh;
            var lod = mesh.Lods.FirstOrDefault();
            if (lod is null) continue;

            foreach (var subObject in lod.SubObjects)
            foreach (var primitive in subObject.Primitives)
            {
                if (primitive.Indices.Count < 3) continue;
                var localTransform = BuildHierarchyTransform(mesh.Matrices, primitive.MatrixIndex, primitive.Hierarchy);
                var worldTransform = localTransform * placement.WorldTransform;

                if (vertices.Count + subObject.Vertices.Count > MaximumVertices ||
                    indices.Count + primitive.Indices.Count > MaximumIndices)
                    return new TrackShapeRenderGeometry(vertices, indices, true);

                var baseIndex = vertices.Count;
                foreach (var vertex in subObject.Vertices)
                    vertices.Add(Vector3.Transform(vertex.Position, worldTransform));

                foreach (var index in primitive.Indices)
                {
                    if ((uint)index >= (uint)subObject.Vertices.Count)
                        throw new InvalidDataException("Track-shape primitive index is outside its sub-object vertex range.");
                    indices.Add(baseIndex + index);
                }
            }
        }

        return new TrackShapeRenderGeometry(vertices, indices, false);
    }

    private static Matrix4x4 BuildHierarchyTransform(
        IReadOnlyList<TrackShapeMatrix> matrices,
        int matrixIndex,
        IReadOnlyList<int> hierarchy)
    {
        var transform = Matrix4x4.Identity;
        var node = matrixIndex;
        var visited = new HashSet<int>();
        while (node >= 0 && node < hierarchy.Count && hierarchy[node] != -1)
        {
            if (!visited.Add(node))
                throw new InvalidDataException("Track-shape matrix hierarchy contains a cycle.");
            if ((uint)node >= (uint)matrices.Count)
                throw new InvalidDataException($"Track-shape hierarchy references missing matrix {node}.");

            transform *= ToOpenRailsMatrix(matrices[node]);
            node = hierarchy[node];
        }
        return transform;
    }

    // Exact axis/sign conversion used by Open Rails XNAMatrixFromMSTS.
    private static Matrix4x4 ToOpenRailsMatrix(TrackShapeMatrix m) => new(
        m.AX, m.AY, -m.AZ, 0,
        m.BX, m.BY, -m.BZ, 0,
        -m.CX, -m.CY, m.CZ, 0,
        m.DX, m.DY, -m.DZ, 1);
}

public sealed record TrackShapeRenderGeometry(
    IReadOnlyList<Vector3> Vertices,
    IReadOnlyList<int> Indices,
    bool WasTruncated);

using System.Numerics;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Expands original Open Rails track-shape primitives into world-space triangle
/// geometry. Matrix conversion and parent traversal follow Viewer3D/Shapes.cs.
/// </summary>
public sealed class TrackShapeRenderGeometryBuilder
{
    private const int DefaultMaximumVertices = 500_000;
    private const int DefaultMaximumIndices = 1_500_000;
    private readonly int maximumVertices;
    private readonly int maximumIndices;

    public TrackShapeRenderGeometryBuilder(
        int maximumVertices = DefaultMaximumVertices,
        int maximumIndices = DefaultMaximumIndices)
    {
        if (maximumVertices < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumVertices));
        if (maximumIndices < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumIndices));
        this.maximumVertices = maximumVertices;
        this.maximumIndices = maximumIndices;
    }

    public TrackShapeRenderGeometry Build(
        IEnumerable<TrackShapePlacement> placements,
        int animationClipIndex = -1,
        float animationFrame = 0,
        Vector3? cameraPosition = null,
        double? elapsedSeconds = null,
        bool loopAnimation = true)
    {
        ArgumentNullException.ThrowIfNull(placements);
        if (elapsedSeconds is { } elapsed && !double.IsFinite(elapsed))
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        var vertices = new List<Vector3>();
        var indices = new List<int>();

        foreach (var placement in placements)
        {
            var mesh = placement.Asset.Mesh;
            var frame = ResolveAnimationFrame(mesh.Animations, animationClipIndex, animationFrame, elapsedSeconds, loopAnimation);
            var pose = TrackShapeAnimationPoseBuilder.Build(mesh.Matrices, mesh.Animations, animationClipIndex, frame);
            var lodDistance = cameraPosition is { } camera
                ? Vector3.Distance(camera, placement.WorldTransform.Translation) : 0;
            var lod = TrackShapeLodSelector.Select(mesh.Lods, lodDistance);
            if (lod is null) continue;

            foreach (var subObject in lod.SubObjects)
            foreach (var primitive in subObject.Primitives)
            {
                if (primitive.Indices.Count < 3) continue;
                var localTransform = BuildHierarchyTransform(mesh.Matrices, primitive.MatrixIndex, primitive.Hierarchy, pose);
                var worldTransform = localTransform * placement.WorldTransform;

                if (vertices.Count + subObject.Vertices.Count > maximumVertices ||
                    indices.Count + primitive.Indices.Count > maximumIndices)
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

    public TrackShapeTexturedRenderGeometry BuildTextured(
        IEnumerable<TrackShapePlacement> placements,
        IReadOnlyList<LoadedTrackShapeTexture> loadedTextures,
        string? seasonFolder = null,
        int animationClipIndex = -1,
        float animationFrame = 0,
        Vector3? cameraPosition = null,
        double? elapsedSeconds = null,
        bool loopAnimation = true)
    {
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(loadedTextures);
        if (elapsedSeconds is { } elapsed && !double.IsFinite(elapsed))
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

        var textureReferences = TrackShapeSeasonTextureSelector.Select(loadedTextures, seasonFolder).ToArray();
        var textureIndexes = new Dictionary<(uint ShapeIndex, string ImageName), int>(new ShapeImageKeyComparer());
        for (var i = 0; i < textureReferences.Length; i++)
            textureIndexes.TryAdd((textureReferences[i].Reference.ShapeIndex, textureReferences[i].Reference.ImageName), i);

        var batchBuilders = new Dictionary<TrackShapeBatchKey, TrackShapeBatchBuilder>();
        var totalVertices = 0;
        var totalIndices = 0;
        var truncated = false;

        foreach (var placement in placements)
        {
            var mesh = placement.Asset.Mesh;
            var frame = ResolveAnimationFrame(mesh.Animations, animationClipIndex, animationFrame, elapsedSeconds, loopAnimation);
            var pose = TrackShapeAnimationPoseBuilder.Build(mesh.Matrices, mesh.Animations, animationClipIndex, frame);
            var lodDistance = cameraPosition is { } camera
                ? Vector3.Distance(camera, placement.WorldTransform.Translation) : 0;
            var lod = TrackShapeLodSelector.Select(mesh.Lods, lodDistance);
            if (lod is null) continue;

            foreach (var subObject in lod.SubObjects)
            {
                foreach (var primitive in subObject.Primitives)
                {
                    if (primitive.Indices.Count < 3) continue;
                    if (totalVertices + subObject.Vertices.Count > maximumVertices ||
                        totalIndices + primitive.Indices.Count > maximumIndices)
                    {
                        truncated = true;
                        break;
                    }

                var textureIndex = primitive.ImageName is not null &&
                    textureIndexes.TryGetValue((placement.Asset.ShapeIndex, primitive.ImageName), out var foundTexture)
                    ? foundTexture : -1;
                var key = new TrackShapeBatchKey(textureIndex, primitive.AlphaTest,
                    primitive.TextureAddressMode, primitive.ZBias);
                if (!batchBuilders.TryGetValue(key, out var batch))
                {
                    batch = new TrackShapeBatchBuilder(key);
                    batchBuilders.Add(key, batch);
                }

                var localTransform = BuildHierarchyTransform(mesh.Matrices, primitive.MatrixIndex, primitive.Hierarchy, pose);
                var worldTransform = localTransform * placement.WorldTransform;
                var baseIndex = batch.Vertices.Count / 5;
                foreach (var vertex in subObject.Vertices)
                {
                    var position = Vector3.Transform(vertex.Position, worldTransform);
                    batch.Vertices.Add(position.X);
                    batch.Vertices.Add(position.Y);
                    batch.Vertices.Add(position.Z);
                    batch.Vertices.Add(vertex.TextureCoordinate.X);
                    batch.Vertices.Add(vertex.TextureCoordinate.Y);
                }

                foreach (var index in primitive.Indices)
                {
                    if ((uint)index >= (uint)subObject.Vertices.Count)
                        throw new InvalidDataException("Track-shape primitive index is outside its sub-object vertex range.");
                    batch.Indices.Add(baseIndex + index);
                }

                    totalVertices += subObject.Vertices.Count;
                    totalIndices += primitive.Indices.Count;
                }
                if (truncated) break;
            }
            if (truncated) break;
        }

        var batches = batchBuilders.Values
            .Where(batch => batch.Indices.Count > 0)
            .Select(batch => new TrackShapeTexturedRenderBatch(
                batch.Vertices.ToArray(), batch.Indices.ToArray(), batch.Key.TextureIndex,
                batch.Key.AlphaTest, batch.Key.TextureAddressMode, batch.Key.ZBias))
            .ToArray();
        return new TrackShapeTexturedRenderGeometry(Array.AsReadOnly(batches), truncated);
    }

    private static float ResolveAnimationFrame(
        IReadOnlyList<TrackShapeAnimationClip>? clips,
        int clipIndex,
        float fallbackFrame,
        double? elapsedSeconds,
        bool loopAnimation)
    {
        if (elapsedSeconds is not { } elapsed || clips is null ||
            clipIndex < 0 || clipIndex >= clips.Count)
            return fallbackFrame;
        return TrackShapeAnimationClock.GetFrame(clips[clipIndex], elapsed, loopAnimation);
    }

    private sealed record TrackShapeBatchKey(int TextureIndex, bool AlphaTest, int TextureAddressMode, float ZBias);
    private sealed class TrackShapeBatchBuilder
    {
        public TrackShapeBatchKey Key { get; }
        public List<float> Vertices { get; } = new();
        public List<int> Indices { get; } = new();
        public TrackShapeBatchBuilder(TrackShapeBatchKey key) => Key = key;
    }

    private sealed class ShapeImageKeyComparer : IEqualityComparer<(uint ShapeIndex, string ImageName)>
    {
        public bool Equals((uint ShapeIndex, string ImageName) x, (uint ShapeIndex, string ImageName) y) =>
            x.ShapeIndex == y.ShapeIndex && StringComparer.OrdinalIgnoreCase.Equals(x.ImageName, y.ImageName);
        public int GetHashCode((uint ShapeIndex, string ImageName) value) =>
            HashCode.Combine(value.ShapeIndex, StringComparer.OrdinalIgnoreCase.GetHashCode(value.ImageName));
    }

    private static Matrix4x4 BuildHierarchyTransform(
        IReadOnlyList<TrackShapeMatrix> matrices,
        int matrixIndex,
        IReadOnlyList<int> hierarchy,
        IReadOnlyDictionary<int, Matrix4x4>? animatedPose = null)
    {
        var transform = Matrix4x4.Identity;
        var node = matrixIndex;
        var visited = new HashSet<int>();
        while (node >= 0)
        {
            if ((uint)node >= (uint)hierarchy.Count)
                throw new InvalidDataException($"Track-shape hierarchy has no parent entry for matrix {node}.");
            // MSTS hierarchy entries of -1 terminate a chain; the sentinel is
            // not itself a matrix and must not be resolved through the matrix table.
            if (hierarchy[node] == -1)
                break;
            if ((uint)node >= (uint)matrices.Count)
                throw new InvalidDataException($"Track-shape primitive references missing matrix {node}.");
            if (!visited.Add(node))
                throw new InvalidDataException("Track-shape matrix hierarchy contains a cycle.");

            var nodeTransform = ToOpenRailsMatrix(matrices[node]);
            if (animatedPose is not null && animatedPose.TryGetValue(node, out var animationTransform))
                nodeTransform *= animationTransform;
            transform *= nodeTransform;
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

public sealed record TrackShapeTexturedRenderBatch(
    IReadOnlyList<float> Vertices,
    IReadOnlyList<int> Indices,
    int TextureIndex,
    bool AlphaTest,
    int TextureAddressMode,
    float ZBias);

public sealed record TrackShapeTexturedRenderGeometry(
    IReadOnlyList<TrackShapeTexturedRenderBatch> Batches,
    bool WasTruncated);

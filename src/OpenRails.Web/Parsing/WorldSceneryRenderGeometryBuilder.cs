// COPYRIGHT 2009-2015 by the Open Rails project.
// This file is part of Open Rails and is licensed under GPL-3.0-or-later.
// Browser-port adaptation expands original WORLD placements into portable render batches.

using System.Numerics;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Converts original WORLD object placements and .s shape primitives to textured
/// batches. WORLD transforms follow the upstream Scenery.WorldPositionFromMSTSLocation
/// conversions; output coordinates are converted back to the preview's MSTS axes.
/// </summary>
public sealed class WorldSceneryRenderGeometryBuilder
{
    private const int DefaultMaximumVertices = 500_000;
    private const int DefaultMaximumIndices = 1_500_000;
    private readonly int maximumVertices;
    private readonly int maximumIndices;

    public WorldSceneryRenderGeometryBuilder(
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

    public WorldSceneryRenderGeometry Build(
        IEnumerable<LoadedWorldSceneryTile> tiles,
        string? seasonFolder = null,
        int animationClipIndex = -1,
        float animationFrame = 0,
        Vector3? cameraPosition = null)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        var textureAssets = new List<LoadedTrackShapeTexture>();
        var texturePathIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var batchBuilders = new Dictionary<WorldBatchKey, WorldBatchBuilder>();
        var totalVertices = 0;
        var totalIndices = 0;
        var truncated = false;

        foreach (var tile in tiles)
        {
            var shapeTextureIndexes = new Dictionary<(uint ShapeIndex, string ImageName), int>(new ShapeImageKeyComparer());
            foreach (var texture in TrackShapeSeasonTextureSelector.Select(tile.Textures.Loaded, seasonFolder))
            {

                if (!texturePathIndexes.TryGetValue(texture.Reference.Path, out var textureIndex))
                {
                    textureIndex = textureAssets.Count;
                    texturePathIndexes.Add(texture.Reference.Path, textureIndex);
                    textureAssets.Add(texture);
                }
                shapeTextureIndexes.TryAdd((texture.Reference.ShapeIndex, texture.Reference.ImageName), textureIndex);
            }

            var meshesByUid = tile.ShapeMeshes.GroupBy(mesh => mesh.ShapeIndex)
                .ToDictionary(group => group.Key, group => group.First());
            foreach (var placement in tile.Placements)
            {
                if (placement.WorldPosition is null ||
                    !meshesByUid.TryGetValue(placement.Uid, out var asset))
                    continue;

                var worldTransform = BuildWorldTransform(placement);
                var mesh = asset.Mesh;
                var pose = TrackShapeAnimationPoseBuilder.Build(mesh.Matrices, mesh.Animations, animationClipIndex, animationFrame);
                var lodDistance = cameraPosition is { } camera
                    ? Vector3.Distance(camera, worldTransform.Translation) : 0;
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
                            shapeTextureIndexes.TryGetValue((placement.Uid, primitive.ImageName), out var foundTexture)
                            ? foundTexture : -1;
                        var key = new WorldBatchKey(textureIndex, primitive.AlphaTest,
                            primitive.TextureAddressMode, primitive.ZBias);
                        if (!batchBuilders.TryGetValue(key, out var batch))
                        {
                            batch = new WorldBatchBuilder(key);
                            batchBuilders.Add(key, batch);
                        }

                        var localTransform = BuildHierarchyTransform(mesh.Matrices, primitive.MatrixIndex, primitive.Hierarchy, pose);
                        var transform = localTransform * worldTransform;
                        var baseIndex = batch.Vertices.Count / 5;
                        foreach (var vertex in subObject.Vertices)
                        {
                            var position = Vector3.Transform(vertex.Position, transform);
                            batch.Vertices.Add(position.X);
                            batch.Vertices.Add(position.Y);
                            batch.Vertices.Add(-position.Z);
                            batch.Vertices.Add(vertex.TextureCoordinate.X);
                            batch.Vertices.Add(vertex.TextureCoordinate.Y);
                        }

                        foreach (var index in primitive.Indices)
                        {
                            if ((uint)index >= (uint)subObject.Vertices.Count)
                                throw new InvalidDataException("WORLD shape primitive index is outside its sub-object vertex range.");
                            batch.Indices.Add(baseIndex + index);
                        }

                        totalVertices += subObject.Vertices.Count;
                        totalIndices += primitive.Indices.Count;
                    }
                    if (truncated) break;
                }
                if (truncated) break;
            }
            if (!truncated)
            {
                var forestTextureIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var texture in TrackShapeSeasonTextureSelector.Select(tile.ForestTextures?.Loaded ?? Array.Empty<LoadedTrackShapeTexture>(), seasonFolder))
                {
                    if (!texturePathIndexes.TryGetValue(texture.Reference.Path, out var forestTextureIndex))
                    {
                        forestTextureIndex = textureAssets.Count;
                        texturePathIndexes.Add(texture.Reference.Path, forestTextureIndex);
                        textureAssets.Add(texture);
                    }
                    forestTextureIndexes.TryAdd(texture.Reference.ImageName, forestTextureIndex);
                }

                var forestBuilder = new ForestMeshBuilder();
                foreach (var forest in tile.Placements.Where(item =>
                    StringComparer.OrdinalIgnoreCase.Equals(item.ObjectKind, "Forest")))
                {
                    if (string.IsNullOrWhiteSpace(forest.TreeTexture) ||
                        !forestTextureIndexes.TryGetValue(forest.TreeTexture, out var forestTextureIndex))
                        continue;
                    var forestMesh = forestBuilder.Build(forest);
                    if (forestMesh.Indices.Length == 0)
                        continue;
                    var vertexCount = forestMesh.Vertices.Length / 5;
                    if (totalVertices + vertexCount > maximumVertices ||
                        totalIndices + forestMesh.Indices.Length > maximumIndices)
                    {
                        truncated = true;
                        break;
                    }

                    var key = new WorldBatchKey(forestTextureIndex, true, 0, 0);
                    if (!batchBuilders.TryGetValue(key, out var forestBatch))
                    {
                        forestBatch = new WorldBatchBuilder(key);
                        batchBuilders.Add(key, forestBatch);
                    }
                    var baseIndex = forestBatch.Vertices.Count / 5;
                    forestBatch.Vertices.AddRange(forestMesh.Vertices);
                    foreach (var index in forestMesh.Indices)
                        forestBatch.Indices.Add(baseIndex + index);
                    totalVertices += vertexCount;
                    totalIndices += forestMesh.Indices.Length;
                }
            }
            if (truncated) break;
        }

        var batches = batchBuilders.Values
            .Where(batch => batch.Indices.Count > 0)
            .Select(batch => new TrackShapeTexturedRenderBatch(
                batch.Vertices.ToArray(), batch.Indices.ToArray(), batch.Key.TextureIndex,
                batch.Key.AlphaTest, batch.Key.TextureAddressMode, batch.Key.ZBias))
            .ToArray();
        return new WorldSceneryRenderGeometry(
            Array.AsReadOnly(textureAssets.ToArray()), Array.AsReadOnly(batches), truncated);
    }

    private static Matrix4x4 BuildWorldTransform(WorldObjectPlacement placement)
    {
        var worldPosition = placement.WorldPosition!;
        var translation = new Vector3(worldPosition.X, worldPosition.Y, -worldPosition.Z);
        Matrix4x4 orientation;
        if (placement.Matrix is { } m)
        {
            orientation = new Matrix4x4(
                m.AX, m.AY, -m.AZ, 0,
                m.BX, m.BY, -m.BZ, 0,
                -m.CX, -m.CY, m.CZ, 0,
                0, 0, 0, 1);
        }
        else if (placement.Direction is { } q)
        {
            orientation = Matrix4x4.CreateFromQuaternion(new Quaternion(q.A, q.B, -q.C, q.D));
        }
        else
        {
            orientation = Matrix4x4.Identity;
        }

        return orientation * Matrix4x4.CreateTranslation(translation);
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
        while (node >= 0 && node < hierarchy.Count && hierarchy[node] != -1)
        {
            if (!visited.Add(node))
                throw new InvalidDataException("WORLD shape matrix hierarchy contains a cycle.");
            if ((uint)node >= (uint)matrices.Count)
                throw new InvalidDataException($"WORLD shape hierarchy references missing matrix {node}.");
            var nodeTransform = ToOpenRailsMatrix(matrices[node]);
            if (animatedPose is not null && animatedPose.TryGetValue(node, out var animationTransform))
                nodeTransform *= animationTransform;
            transform *= nodeTransform;
            node = hierarchy[node];
        }
        return transform;
    }

    private static Matrix4x4 ToOpenRailsMatrix(TrackShapeMatrix m) => new(
        m.AX, m.AY, -m.AZ, 0,
        m.BX, m.BY, -m.BZ, 0,
        -m.CX, -m.CY, m.CZ, 0,
        m.DX, m.DY, -m.DZ, 1);

    private sealed record WorldBatchKey(int TextureIndex, bool AlphaTest, int TextureAddressMode, float ZBias);
    private sealed class WorldBatchBuilder
    {
        public WorldBatchKey Key { get; }
        public List<float> Vertices { get; } = new();
        public List<int> Indices { get; } = new();
        public WorldBatchBuilder(WorldBatchKey key) => Key = key;
    }

    private sealed class ShapeImageKeyComparer : IEqualityComparer<(uint ShapeIndex, string ImageName)>
    {
        public bool Equals((uint ShapeIndex, string ImageName) x, (uint ShapeIndex, string ImageName) y) =>
            x.ShapeIndex == y.ShapeIndex && StringComparer.OrdinalIgnoreCase.Equals(x.ImageName, y.ImageName);
        public int GetHashCode((uint ShapeIndex, string ImageName) value) =>
            HashCode.Combine(value.ShapeIndex, StringComparer.OrdinalIgnoreCase.GetHashCode(value.ImageName));
    }
}

public sealed record WorldSceneryRenderGeometry(
    IReadOnlyList<LoadedTrackShapeTexture> Textures,
    IReadOnlyList<TrackShapeTexturedRenderBatch> Batches,
    bool WasTruncated);

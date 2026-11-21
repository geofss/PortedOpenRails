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
    private const int MaximumVertices = 500_000;
    private const int MaximumIndices = 1_500_000;

    public WorldSceneryRenderGeometry Build(IEnumerable<LoadedWorldSceneryTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        var textureAssets = new List<LoadedTrackShapeTexture>();
        var texturePathIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var shapeTextureIndexes = new Dictionary<(uint ShapeIndex, string ImageName), int>(new ShapeImageKeyComparer());
        var batchBuilders = new Dictionary<WorldBatchKey, WorldBatchBuilder>();
        var totalVertices = 0;
        var totalIndices = 0;
        var truncated = false;

        foreach (var tile in tiles)
        {
            foreach (var texture in tile.Textures.Loaded)
            {
                if (texture.Reference.SeasonFolder is not null ||
                    texture.Texture.PixelFormat != AceTexturePixelFormat.Rgba8 ||
                    texture.Texture.MipLevels.Count == 0)
                    continue;

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
                var lod = mesh.Lods.FirstOrDefault();
                if (lod is null) continue;

                foreach (var subObject in lod.SubObjects)
                {
                    foreach (var primitive in subObject.Primitives)
                    {
                        if (primitive.Indices.Count < 3) continue;
                        if (totalVertices + subObject.Vertices.Count > MaximumVertices ||
                            totalIndices + primitive.Indices.Count > MaximumIndices)
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

                        var localTransform = BuildHierarchyTransform(mesh.Matrices, primitive.MatrixIndex, primitive.Hierarchy);
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
        IReadOnlyList<int> hierarchy)
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
            transform *= TrackShapeMatrixConversion.ToOpenRailsMatrix(matrices[node]);
            node = hierarchy[node];
        }
        return transform;
    }

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

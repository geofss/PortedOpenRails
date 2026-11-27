using System.Numerics;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Builds deterministic billboard geometry from original MSTS/Open Rails ForestObj
/// parameters. Terrain-height sampling and track-clearance filtering are supplied
/// by later route integration stages; this builder preserves the source placement
/// distribution and per-tree dimensions.
/// </summary>
public sealed class ForestMeshBuilder
{
    public const int MaximumTreesPerForest = 100_000;

    public ForestMesh Build(WorldObjectPlacement forest)
    {
        ArgumentNullException.ThrowIfNull(forest);
        if (!StringComparer.OrdinalIgnoreCase.Equals(forest.ObjectKind, "Forest"))
            throw new ArgumentException("Placement is not a Forest WORLD object.", nameof(forest));
        if (forest.WorldPosition is null || forest.ForestArea is null ||
            forest.ForestScaleRange is null || forest.ForestTreeSize is null ||
            forest.ForestPopulation is null || string.IsNullOrWhiteSpace(forest.TreeTexture))
            return ForestMesh.Empty(forest.TreeTexture);

        var population = forest.ForestPopulation.Value;
        if (population < 0 || population > MaximumTreesPerForest)
            throw new InvalidDataException($"Forest population {population} is outside the supported range.");
        var area = forest.ForestArea;
        var scale = forest.ForestScaleRange;
        var size = forest.ForestTreeSize;
        if (!float.IsFinite(area.First) || !float.IsFinite(area.Second) ||
            area.First < 0 || area.Second < 0 ||
            !float.IsFinite(scale.Minimum) || !float.IsFinite(scale.Maximum) ||
            scale.Minimum < 0 || scale.Maximum < scale.Minimum ||
            !float.IsFinite(size.First) || !float.IsFinite(size.Second) ||
            size.First < 0 || size.Second < 0)
            throw new InvalidDataException("Forest dimensions or scale range are invalid.");

        var position = forest.WorldPosition;
        var seedValue = 1000.0 * (position.X + position.Z + position.Y);
        var seed = double.IsFinite(seedValue) ? unchecked((int)seedValue) : 0;
        var random = new Random(seed);
        var vertices = new float[population * 4 * 5];
        var indices = new int[population * 6];

        for (var i = 0; i < population; i++)
        {
            var x = position.X + ((float)random.NextDouble() - 0.5f) * area.First;
            var z = position.Z + ((float)random.NextDouble() - 0.5f) * area.Second;
            var treeScale = scale.Minimum + (float)random.NextDouble() * (scale.Maximum - scale.Minimum);
            var halfWidth = size.First * treeScale * 0.5f;
            var height = size.Second * treeScale;
            var vertex = i * 20;
            AddVertex(vertices, vertex, x - halfWidth, position.Y, z, 0, 1);
            AddVertex(vertices, vertex + 5, x + halfWidth, position.Y, z, 1, 1);
            AddVertex(vertices, vertex + 10, x + halfWidth, position.Y + height, z, 1, 0);
            AddVertex(vertices, vertex + 15, x - halfWidth, position.Y + height, z, 0, 0);
            var index = i * 6;
            var first = i * 4;
            indices[index] = first;
            indices[index + 1] = first + 1;
            indices[index + 2] = first + 2;
            indices[index + 3] = first;
            indices[index + 4] = first + 2;
            indices[index + 5] = first + 3;
        }

        return new ForestMesh(forest.TreeTexture, vertices, indices);
    }

    private static void AddVertex(float[] target, int offset, float x, float y, float z, float u, float v)
    {
        target[offset] = x;
        target[offset + 1] = y;
        target[offset + 2] = -z;
        target[offset + 3] = u;
        target[offset + 4] = v;
    }
}

public sealed record ForestMesh(string? TextureName, float[] Vertices, int[] Indices)
{
    public int TreeCount => Indices.Length / 6;
    public static ForestMesh Empty(string? textureName) => new(textureName, Array.Empty<float>(), Array.Empty<int>());
}

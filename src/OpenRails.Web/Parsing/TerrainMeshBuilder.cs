using System.Numerics;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Builds the original Open Rails 17x17 terrain patch vertex grids and alternating
/// 16x16 triangle topology from .t metadata, _y.raw elevations, and optional _f.raw flags.
/// </summary>
public sealed class TerrainMeshBuilder
{
    private const int VerticesPerSide = 17;
    private const int CellsPerSide = 16;
    private const int MaximumPatches = 4096;

    public TerrainTileMesh Build(
        TerrainTileContent tile,
        TerrainPatchSet patchSet,
        Func<int, int, float>? elevationResolver = null,
        Func<int, int, bool>? hiddenVertexResolver = null)
    {
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(patchSet);

        var side = patchSet.PatchesPerSide;
        if (side <= 0 || side > 64 || patchSet.Patches.Count != side * side || patchSet.Patches.Count > MaximumPatches)
            throw new InvalidDataException("Terrain patch-set dimensions are invalid.");

        elevationResolver ??= (x, z) => tile.Altitude.GetElevationMeters(
            x, z, tile.Terrain.Samples.Floor, tile.Terrain.Samples.Scale);
        hiddenVertexResolver ??= (x, z) => tile.Flags?.IsVertexHidden(x, z) ?? false;

        var patches = new List<TerrainPatchMesh>(patchSet.Patches.Count);
        for (var patchZ = 0; patchZ < side; patchZ++)
        for (var patchX = 0; patchX < side; patchX++)
        {
            var patch = patchSet.Patches[patchZ * side + patchX];
            patches.Add(BuildPatch(tile, patchSet.Distance, side, patch, patchX, patchZ,
                elevationResolver, hiddenVertexResolver));
        }

        return new TerrainTileMesh(tile.Reference, patchSet.Distance, patches);
    }

    private static TerrainPatchMesh BuildPatch(
        TerrainTileContent tile,
        int lodDistance,
        int patchesPerSide,
        TerrainPatch patch,
        int patchX,
        int patchZ,
        Func<int, int, float> elevation,
        Func<int, int, bool> isHidden)
    {
        var sampleSize = tile.Terrain.Samples.SampleSize;
        if (!float.IsFinite(sampleSize) || sampleSize <= 0)
            throw new InvalidDataException("Terrain sample spacing must be a positive finite value.");

        var vertices = new TerrainMeshVertex[VerticesPerSide * VerticesPerSide];
        var totalElevation = 0f;
        for (var z = 0; z < VerticesPerSide; z++)
        for (var x = 0; x < VerticesPerSide; x++)
        {
            var sampleX = patchX * CellsPerSide + x;
            var sampleZ = patchZ * CellsPerSide + z;
            var absoluteElevation = elevation(sampleX, sampleZ);
            var relativeElevation = absoluteElevation - tile.Terrain.Samples.Floor;
            totalElevation += relativeElevation;

            var east = -patch.RadiusMeters + x * sampleSize;
            var north = -patch.RadiusMeters + z * sampleSize;
            var u = (float)x;
            var v = (float)z;
            var textureU = u * patch.TextureWidth + v * patch.TextureB + patch.TextureX;
            var textureV = u * patch.TextureC + v * patch.TextureHeight + patch.TextureY;
            var normal = CalculateNormal(tile, patchX, patchZ, x, z, elevation);

            vertices[z * VerticesPerSide + x] = new TerrainMeshVertex(
                new Vector3(east, relativeElevation, north),
                normal,
                new Vector2(textureU, textureV));
        }

        var indices = BuildIndices(patchX, patchZ, isHidden);
        var patchOrigin = new Vector3(
            patch.CenterX - 1024,
            tile.Terrain.Samples.Floor,
            patch.CenterZ - 1024 + 2048 * tile.Terrain.TileSize);

        return new TerrainPatchMesh(
            patchX,
            patchZ,
            patch.ShaderIndex,
            patchOrigin,
            tile.Terrain.TileSize * 2048f / patchesPerSide,
            totalElevation / vertices.Length,
            vertices,
            indices,
            lodDistance);
    }

    private static IReadOnlyList<int> BuildIndices(
        int patchX,
        int patchZ,
        Func<int, int, bool> isHidden)
    {
        var indices = new List<int>(CellsPerSide * CellsPerSide * 6);
        for (var z = 0; z < CellsPerSide; z++)
        for (var x = 0; x < CellsPerSide; x++)
        {
            var northWest = z * VerticesPerSide + x;
            var northEast = northWest + 1;
            var southWest = northWest + VerticesPerSide;
            var southEast = southWest + 1;

            var hiddenNW = isHidden(patchX * CellsPerSide + x, patchZ * CellsPerSide + z);
            var hiddenNE = isHidden(patchX * CellsPerSide + x + 1, patchZ * CellsPerSide + z);
            var hiddenSW = isHidden(patchX * CellsPerSide + x, patchZ * CellsPerSide + z + 1);
            var hiddenSE = isHidden(patchX * CellsPerSide + x + 1, patchZ * CellsPerSide + z + 1);

            if ((z & 1) == (x & 1))
            {
                if (!hiddenNW && !hiddenSE && !hiddenSW)
                {
                    indices.Add(northWest);
                    indices.Add(southEast);
                    indices.Add(southWest);
                }
                if (!hiddenNW && !hiddenNE && !hiddenSE)
                {
                    indices.Add(northWest);
                    indices.Add(northEast);
                    indices.Add(southEast);
                }
            }
            else
            {
                if (!hiddenNE && !hiddenSE && !hiddenSW)
                {
                    indices.Add(northEast);
                    indices.Add(southEast);
                    indices.Add(southWest);
                }
                if (!hiddenNW && !hiddenNE && !hiddenSW)
                {
                    indices.Add(northWest);
                    indices.Add(northEast);
                    indices.Add(southWest);
                }
            }
        }

        return Array.AsReadOnly(indices.ToArray());
    }

    private static Vector3 CalculateNormal(
        TerrainTileContent tile,
        int patchX,
        int patchZ,
        int x,
        int z,
        Func<int, int, float> elevation)
    {
        var globalX = patchX * CellsPerSide + x;
        var globalZ = patchZ * CellsPerSide + z;
        var d = tile.Terrain.TileSize * 8f;
        var center = new Vector3(x, elevation(globalX, globalZ), z);

        var toNorth = Vector3.Normalize(new Vector3(x, elevation(globalX, globalZ - 1), z - d) - center);
        var toEast = Vector3.Normalize(new Vector3(x + d, elevation(globalX + 1, globalZ), z) - center);
        var toSouth = Vector3.Normalize(new Vector3(x, elevation(globalX, globalZ + 1), z + d) - center);
        var toWest = Vector3.Normalize(new Vector3(x - d, elevation(globalX - 1, globalZ), z) - center);

        if ((z & 1) == (x & 1))
        {
            var toNorthEast = Vector3.Normalize(new Vector3(x + d, elevation(globalX + 1, globalZ - 1), z - d) - center);
            var toSouthEast = Vector3.Normalize(new Vector3(x + d, elevation(globalX + 1, globalZ + 1), z + d) - center);
            var toSouthWest = Vector3.Normalize(new Vector3(x - d, elevation(globalX - 1, globalZ + 1), z + d) - center);
            var toNorthWest = Vector3.Normalize(new Vector3(x - d, elevation(globalX - 1, globalZ - 1), z - d) - center);

            return Vector3.Normalize(
                Vector3.Normalize(Vector3.Cross(toNorthEast, toNorth)) +
                Vector3.Normalize(Vector3.Cross(toEast, toNorthEast)) +
                Vector3.Normalize(Vector3.Cross(toSouthEast, toEast)) +
                Vector3.Normalize(Vector3.Cross(toSouth, toSouthEast)) +
                Vector3.Normalize(Vector3.Cross(toSouthWest, toSouth)) +
                Vector3.Normalize(Vector3.Cross(toWest, toSouthWest)) +
                Vector3.Normalize(Vector3.Cross(toNorthWest, toWest)) +
                Vector3.Normalize(Vector3.Cross(toNorth, toNorthWest)));
        }

        return Vector3.Normalize(
            Vector3.Normalize(Vector3.Cross(toEast, toNorth)) +
            Vector3.Normalize(Vector3.Cross(toSouth, toEast)) +
            Vector3.Normalize(Vector3.Cross(toWest, toSouth)) +
            Vector3.Normalize(Vector3.Cross(toNorth, toWest)));
    }
}

public sealed record TerrainMeshVertex(Vector3 Position, Vector3 Normal, Vector2 TextureCoordinate);

public sealed record TerrainPatchMesh(
    int PatchX,
    int PatchZ,
    int ShaderIndex,
    Vector3 PatchOrigin,
    float PatchSizeMeters,
    float AverageRelativeElevation,
    IReadOnlyList<TerrainMeshVertex> Vertices,
    IReadOnlyList<int> Indices,
    int LodDistance);

public sealed class TerrainTileMesh
{
    public TerrainTileReference Reference { get; }
    public int LodDistance { get; }
    public IReadOnlyList<TerrainPatchMesh> Patches { get; }

    public TerrainTileMesh(TerrainTileReference reference, int lodDistance, IEnumerable<TerrainPatchMesh> patches)
    {
        Reference = reference;
        LodDistance = lodDistance;
        Patches = Array.AsReadOnly(patches.ToArray());
    }
}

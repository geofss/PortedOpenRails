namespace OpenRails.Web.Parsing;

/// <summary>
/// Resolves terrain samples across adjacent tiles using the same physical-coordinate
/// conversion as Open Rails TileManager.GetElevation and IsVertexHidden.
/// </summary>
public sealed class TerrainTileSampleResolver
{
    private const double TileSizeMeters = 2048;
    private readonly IReadOnlyDictionary<(int TileX, int TileZ), TerrainTileContent> tiles;

    public TerrainTileSampleResolver(IEnumerable<TerrainTileContent> loadedTiles)
    {
        ArgumentNullException.ThrowIfNull(loadedTiles);
        tiles = loadedTiles
            .GroupBy(tile => (tile.Reference.TileX, tile.Reference.TileZ))
            .ToDictionary(group => group.Key, group => group.Last());
    }

    public Func<int, int, float> CreateElevationResolver(TerrainTileContent source) =>
        (x, z) => GetElevationMeters(source, x, z);

    public Func<int, int, bool> CreateHiddenVertexResolver(TerrainTileContent source) =>
        (x, z) => IsVertexHidden(source, x, z);

    public float GetElevationMeters(TerrainTileContent source, int sampleX, int sampleZ)
    {
        ArgumentNullException.ThrowIfNull(source);
        var sampleCount = source.Terrain.Samples.SampleCount;
        if (IsInside(sampleX, sampleZ, sampleCount))
            return ReadElevation(source, sampleX, sampleZ);

        if (TryResolveNeighbor(source, sampleX, sampleZ, out var neighbor, out var neighborX, out var neighborZ))
            return ReadElevation(neighbor, neighborX, neighborZ);

        return ReadElevation(
            source,
            Math.Clamp(sampleX, 0, sampleCount - 1),
            Math.Clamp(sampleZ, 0, sampleCount - 1));
    }

    public bool IsVertexHidden(TerrainTileContent source, int sampleX, int sampleZ)
    {
        ArgumentNullException.ThrowIfNull(source);
        var sampleCount = source.Terrain.Samples.SampleCount;
        if (IsInside(sampleX, sampleZ, sampleCount))
            return source.Flags?.IsVertexHidden(sampleX, sampleZ) ?? false;

        if (!TryResolveNeighbor(source, sampleX, sampleZ, out var neighbor, out var neighborX, out var neighborZ))
            return false;

        return neighbor.Flags?.IsVertexHidden(neighborX, neighborZ) ?? false;
    }

    private bool TryResolveNeighbor(
        TerrainTileContent source,
        int sampleX,
        int sampleZ,
        out TerrainTileContent neighbor,
        out int neighborX,
        out int neighborZ)
    {
        var sampleSize = source.Terrain.Samples.SampleSize;
        if (!float.IsFinite(sampleSize) || sampleSize <= 0)
            throw new InvalidDataException("Terrain sample spacing must be a positive finite value.");

        var xMeters = sampleX * sampleSize;
        var zMeters = TileSizeMeters * source.Terrain.TileSize - sampleZ * sampleSize;
        var neighborTileX = source.Reference.TileX + (int)Math.Floor(xMeters / TileSizeMeters);
        var neighborTileZ = source.Reference.TileZ + (int)Math.Floor((zMeters - 1) / TileSizeMeters);

        if (!tiles.TryGetValue((neighborTileX, neighborTileZ), out neighbor!))
        {
            neighborX = 0;
            neighborZ = 0;
            return false;
        }

        var neighborSampleSize = neighbor.Terrain.Samples.SampleSize;
        if (!float.IsFinite(neighborSampleSize) || neighborSampleSize <= 0)
            throw new InvalidDataException("Neighbor terrain sample spacing must be a positive finite value.");

        neighborX = (int)((xMeters + TileSizeMeters * (source.Reference.TileX - neighbor.Reference.TileX)) / neighborSampleSize);
        neighborZ = -(int)((zMeters + TileSizeMeters *
            (source.Reference.TileZ - neighbor.Reference.TileZ - neighbor.Terrain.TileSize)) / neighborSampleSize);

        neighborX = Math.Clamp(neighborX, 0, neighbor.Terrain.Samples.SampleCount - 1);
        neighborZ = Math.Clamp(neighborZ, 0, neighbor.Terrain.Samples.SampleCount - 1);
        return true;
    }

    private static bool IsInside(int x, int z, int sampleCount) =>
        x >= 0 && x < sampleCount && z >= 0 && z < sampleCount;

    private static float ReadElevation(TerrainTileContent tile, int x, int z) =>
        tile.Altitude.GetElevationMeters(
            x, z, tile.Terrain.Samples.Floor, tile.Terrain.Samples.Scale);
}

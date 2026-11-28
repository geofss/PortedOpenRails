namespace OpenRails.Web.Parsing;

/// <summary>
/// Selects the terrain patch set whose native distance threshold contains the
/// camera-to-tile distance. Invalid thresholds are ignored and input order is not
/// assumed to be rendering order.
/// </summary>
public static class TerrainLodSelector
{
    public static TerrainTileMesh? Select(IReadOnlyList<TerrainTileMesh> levels, double distanceMeters)
    {
        ArgumentNullException.ThrowIfNull(levels);
        if (!double.IsFinite(distanceMeters) || distanceMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(distanceMeters));
        if (levels.Count == 0)
            return null;

        var ordered = levels
            .Where(level => level.LodDistance >= 0)
            .OrderBy(level => level.LodDistance)
            .ToArray();
        if (ordered.Length == 0)
            return levels[0];

        foreach (var level in ordered)
            if (distanceMeters <= level.LodDistance)
                return level;

        return ordered[^1];
    }
}

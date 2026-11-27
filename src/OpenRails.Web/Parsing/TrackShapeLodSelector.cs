namespace OpenRails.Web.Parsing;

/// <summary>
/// Selects the native distance-level whose viewing threshold contains the current
/// camera distance. Levels are sorted by threshold because source files are not
/// required to list them in rendering order.
/// </summary>
public static class TrackShapeLodSelector
{
    public static TrackShapeLodMesh? Select(IReadOnlyList<TrackShapeLodMesh> levels, float distance)
    {
        ArgumentNullException.ThrowIfNull(levels);
        if (!float.IsFinite(distance) || distance < 0)
            throw new ArgumentOutOfRangeException(nameof(distance));
        if (levels.Count == 0)
            return null;

        var ordered = levels
            .Where(level => float.IsFinite(level.ViewingDistance) && level.ViewingDistance >= 0)
            .OrderBy(level => level.ViewingDistance)
            .ToArray();
        if (ordered.Length == 0)
            return levels[0];

        foreach (var level in ordered)
            if (distance <= level.ViewingDistance)
                return level;

        return ordered[^1];
    }
}

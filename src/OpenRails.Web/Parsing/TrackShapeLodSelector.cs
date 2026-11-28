namespace OpenRails.Web.Parsing;

/// <summary>
/// Selects the native distance level using the same viewing-sphere rule as
/// Open Rails Viewer3D: an object remains in range while camera distance is
/// within its bounding radius plus the LOD viewing threshold.
/// </summary>
public static class TrackShapeLodSelector
{
    public static TrackShapeLodMesh? Select(
        IReadOnlyList<TrackShapeLodMesh> levels,
        float distance,
        float viewSphereRadius = 0,
        float lodBias = 1)
    {
        ArgumentNullException.ThrowIfNull(levels);
        if (!float.IsFinite(distance) || distance < 0)
            throw new ArgumentOutOfRangeException(nameof(distance));
        if (!float.IsFinite(viewSphereRadius) || viewSphereRadius < 0)
            throw new ArgumentOutOfRangeException(nameof(viewSphereRadius));
        if (!float.IsFinite(lodBias) || lodBias <= 0)
            throw new ArgumentOutOfRangeException(nameof(lodBias));
        if (levels.Count == 0)
            return null;

        var ordered = levels
            .Where(level => float.IsFinite(level.ViewingDistance) && level.ViewingDistance >= 0)
            .OrderBy(level => level.ViewingDistance)
            .ToArray();
        if (ordered.Length == 0)
            return levels[0];

        var cameraDistanceFromSurface = Math.Max(0, distance - viewSphereRadius);
        foreach (var level in ordered)
            if (cameraDistanceFromSurface <= level.ViewingDistance * lodBias)
                return level;

        return ordered[^1];
    }
}

namespace OpenRails.Web.Parsing;

/// <summary>
/// Selects one seasonal texture per shape/image reference. Prefer the requested
/// season, then the route's base texture, then any available variant as a fallback.
/// </summary>
public static class TrackShapeSeasonTextureSelector
{
    public static IReadOnlyList<LoadedTrackShapeTexture> Select(
        IEnumerable<LoadedTrackShapeTexture> assets,
        string? seasonFolder = null)
    {
        ArgumentNullException.ThrowIfNull(assets);
        var usable = assets.Where(asset =>
            asset.Texture.PixelFormat == AceTexturePixelFormat.Rgba8 &&
            asset.Texture.MipLevels.Count > 0).ToArray();
        var groups = usable.GroupBy(asset => (asset.Reference.ShapeIndex, asset.Reference.ImageName),
            ShapeImageComparer.Instance);
        var selected = new List<LoadedTrackShapeTexture>();
        foreach (var group in groups)
        {
            var choice = (seasonFolder is not null
                ? group.FirstOrDefault(asset => string.Equals(asset.Reference.SeasonFolder, seasonFolder, StringComparison.OrdinalIgnoreCase))
                : null)
                ?? group.FirstOrDefault(asset => asset.Reference.SeasonFolder is null)
                ?? group.FirstOrDefault();
            if (choice is not null) selected.Add(choice);
        }
        return Array.AsReadOnly(selected.ToArray());
    }

    private sealed class ShapeImageComparer : IEqualityComparer<(uint ShapeIndex, string ImageName)>
    {
        public static readonly ShapeImageComparer Instance = new();
        public bool Equals((uint ShapeIndex, string ImageName) x, (uint ShapeIndex, string ImageName) y) =>
            x.ShapeIndex == y.ShapeIndex && StringComparer.OrdinalIgnoreCase.Equals(x.ImageName, y.ImageName);
        public int GetHashCode((uint ShapeIndex, string ImageName) value) =>
            HashCode.Combine(value.ShapeIndex, StringComparer.OrdinalIgnoreCase.GetHashCode(value.ImageName));
    }
}

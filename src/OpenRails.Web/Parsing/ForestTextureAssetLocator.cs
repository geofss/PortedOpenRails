using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Resolves ForestObj TreeTexture references using the seasonal route-texture
/// variants selected by Open Rails' Helpers.GetForestTextureFile.
/// </summary>
public sealed class ForestTextureAssetLocator
{
    private static readonly string?[] SeasonFolders =
    {
        null, "Spring", "Autumn", "Winter", "SpringSnow", "AutumnSnow", "WinterSnow"
    };

    public TrackShapeTextureAssetDiscovery Locate(
        IVirtualContentStore store,
        string routeRoot,
        IEnumerable<WorldObjectPlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(placements);
        var root = VirtualPath.Normalize(routeRoot);
        var found = new List<TrackShapeTextureAssetReference>();
        var missing = new List<TrackShapeTextureAssetIssue>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var forest in placements.Where(item =>
            StringComparer.OrdinalIgnoreCase.Equals(item.ObjectKind, "Forest") &&
            !string.IsNullOrWhiteSpace(item.TreeTexture)))
        {
            var imageName = forest.TreeTexture!;
            if (!seen.Add(imageName))
                continue;

            var normalizedName = imageName.Replace('\\', '/');
            if (normalizedName.StartsWith('/') || normalizedName.Contains(':') ||
                normalizedName.Split('/').Any(segment => segment is "." or ".."))
            {
                missing.Add(new TrackShapeTextureAssetIssue(forest.Uid, imageName,
                    "Forest texture filename is not a safe virtual relative path."));
                continue;
            }

            var matches = new List<TrackShapeTextureAssetReference>();
            foreach (var season in SeasonFolders)
            {
                var seasonalName = season is null ? normalizedName : $"{season}/{normalizedName}";
                var actual = RouteContentPathResolver.Resolve(
                    store, root, "TEXTURES", "GLOBAL/TEXTURES", seasonalName);
                if (actual is not null)
                    matches.Add(new TrackShapeTextureAssetReference(forest.Uid, imageName, season, actual));
            }

            if (matches.Count == 0)
                missing.Add(new TrackShapeTextureAssetIssue(forest.Uid, imageName,
                    "Forest tree texture was not found in route Textures content."));
            else
                found.AddRange(matches);
        }

        return new TrackShapeTextureAssetDiscovery(found, missing);
    }
}

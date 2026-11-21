using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>Locates ACE/DDS image assets referenced by original shape primitives.</summary>
public sealed class TrackShapeTextureAssetLocator
{
    private static readonly string?[] SeasonFolders =
    {
        null, "Snow", "Spring", "Autumn", "Winter", "SpringSnow", "AutumnSnow", "WinterSnow"
    };

    public TrackShapeTextureAssetDiscovery Locate(
        IVirtualContentStore store,
        string routeRoot,
        IEnumerable<TrackShapeAssetMesh> shapeAssets)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(shapeAssets);

        var root = VirtualPath.Normalize(routeRoot);
        var available = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in store.Paths)
            available.TryAdd(path, path);

        var found = new List<TrackShapeTextureAssetReference>();
        var missing = new List<TrackShapeTextureAssetIssue>();
        var seen = new HashSet<(uint ShapeIndex, string ImageName)>(new ShapeImageComparer());

        foreach (var asset in shapeAssets)
        foreach (var imageName in asset.Mesh.Lods
            .SelectMany(lod => lod.SubObjects)
            .SelectMany(subObject => subObject.Primitives)
            .Select(primitive => primitive.ImageName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!seen.Add((asset.ShapeIndex, imageName!)))
                continue;

            var normalizedName = imageName!.Replace('\\', '/');
            if (normalizedName.StartsWith('/') || normalizedName.Contains(':') ||
                normalizedName.Split('/').Any(segment => segment is "." or ".."))
            {
                missing.Add(new TrackShapeTextureAssetIssue(asset.ShapeIndex, imageName,
                    "Shape texture filename is not a safe virtual relative path."));
                continue;
            }

            var matches = new List<TrackShapeTextureAssetReference>();
            foreach (var season in SeasonFolders)
            {
                var candidate = season is null
                    ? $"{root}/TEXTURES/{normalizedName}"
                    : $"{root}/TEXTURES/{season}/{normalizedName}";
                if (available.TryGetValue(candidate, out var actual))
                    matches.Add(new TrackShapeTextureAssetReference(
                        asset.ShapeIndex, imageName, season, actual));
            }

            if (matches.Count == 0)
                missing.Add(new TrackShapeTextureAssetIssue(asset.ShapeIndex, imageName,
                    "Original shape texture was not found in route Textures content."));
            else
                found.AddRange(matches);
        }

        return new TrackShapeTextureAssetDiscovery(found, missing);
    }

    private sealed class ShapeImageComparer : IEqualityComparer<(uint ShapeIndex, string ImageName)>
    {
        public bool Equals((uint ShapeIndex, string ImageName) x, (uint ShapeIndex, string ImageName) y) =>
            x.ShapeIndex == y.ShapeIndex &&
            StringComparer.OrdinalIgnoreCase.Equals(x.ImageName, y.ImageName);

        public int GetHashCode((uint ShapeIndex, string ImageName) value) =>
            HashCode.Combine(value.ShapeIndex, StringComparer.OrdinalIgnoreCase.GetHashCode(value.ImageName));
    }
}

public sealed record TrackShapeTextureAssetReference(
    uint ShapeIndex,
    string ImageName,
    string? SeasonFolder,
    string Path);

public sealed record TrackShapeTextureAssetIssue(uint ShapeIndex, string ImageName, string Reason);

public sealed class TrackShapeTextureAssetDiscovery
{
    public IReadOnlyList<TrackShapeTextureAssetReference> Found { get; }
    public IReadOnlyList<TrackShapeTextureAssetIssue> Missing { get; }

    public TrackShapeTextureAssetDiscovery(
        IEnumerable<TrackShapeTextureAssetReference> found,
        IEnumerable<TrackShapeTextureAssetIssue> missing)
    {
        Found = Array.AsReadOnly(found.ToArray());
        Missing = Array.AsReadOnly(missing.ToArray());
    }
}

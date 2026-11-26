using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Resolves rolling-stock shape images inside the owning TRAINSET folder.
/// Unlike route shapes, rolling-stock textures are searched beside the vehicle
/// assets and in that trainset folder's TEXTURES subdirectory.
/// </summary>
public sealed class RollingStockTextureAssetLocator
{
    public TrackShapeTextureAssetDiscovery Locate(
        IVirtualContentStore store,
        string trainsetFolder,
        IEnumerable<TrackShapeAssetMesh> shapeAssets)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(shapeAssets);

        var folder = VirtualPath.Normalize($"TRAINS/TRAINSET/{trainsetFolder}");
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
                    "Rolling-stock texture filename is not a safe virtual relative path."));
                continue;
            }

            var candidates = new[]
            {
                VirtualPath.Normalize($"{folder}/{normalizedName}"),
                VirtualPath.Normalize($"{folder}/TEXTURES/{normalizedName}")
            };
            var resolved = candidates
                .Select(candidate => available.TryGetValue(candidate, out var actual) ? actual : null)
                .FirstOrDefault(actual => actual is not null);

            if (resolved is null)
                missing.Add(new TrackShapeTextureAssetIssue(asset.ShapeIndex, imageName,
                    "Original rolling-stock texture was not found beside the vehicle or in its TEXTURES folder."));
            else
                found.Add(new TrackShapeTextureAssetReference(asset.ShapeIndex, imageName, null, resolved));
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

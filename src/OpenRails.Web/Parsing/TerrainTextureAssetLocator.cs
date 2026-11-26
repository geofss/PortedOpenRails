using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Locates original route terrain textures in TerrTex and its seasonal variants,
/// matching the Open Rails terrain texture path conventions.
/// </summary>
public sealed class TerrainTextureAssetLocator
{
    private static readonly string?[] SeasonFolders =
    {
        null, "Snow", "Spring", "Autumn", "Winter", "SpringSnow", "AutumnSnow", "WinterSnow"
    };

    public TerrainTextureAssetDiscovery Locate(
        IVirtualContentStore store,
        string routeRoot,
        IEnumerable<TerrainTileContent> tiles)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(tiles);

        var root = VirtualPath.Normalize(routeRoot);
        var available = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in store.Paths)
            available.TryAdd(path, path);

        var found = new List<TerrainTextureAssetReference>();
        var missing = new List<TerrainTextureAssetIssue>();
        var seen = new HashSet<(string Shader, int Slot, string FileName)>(StringTupleComparer.Instance);

        foreach (var tile in tiles)
        foreach (var shader in tile.Terrain.Shaders)
        {
            var slots = shader.TextureSlots
                .Select((slot, index) => (slot.FileName, Index: index, IsImplicit: false))
                .ToList();

            if (slots.Count == 1)
                slots.Add(("microtex.ace", 1, true));

            foreach (var slot in slots)
            {
                if (!seen.Add((shader.Name, slot.Index, slot.FileName)))
                    continue;

                var fileName = slot.FileName.Replace('\\', '/');
                if (fileName.StartsWith('/') || fileName.Contains(':') ||
                    fileName.Split('/').Any(segment => segment is "." or ".."))
                {
                    missing.Add(new TerrainTextureAssetIssue(shader.Name, slot.Index, slot.FileName,
                        slot.IsImplicit, "Terrain texture filename is not a safe virtual relative path."));
                    continue;
                }

                var matches = new List<TerrainTextureAssetReference>();
                foreach (var season in SeasonFolders)
                {
                    var candidate = season is null
                        ? $"{root}/TERRTEX/{fileName}"
                        : $"{root}/TERRTEX/{season}/{fileName}";
                    if (available.TryGetValue(candidate, out var actual))
                        matches.Add(new TerrainTextureAssetReference(
                            shader.Name, slot.Index, slot.FileName, season, actual, slot.IsImplicit));
                }

                if (matches.Count == 0)
                    missing.Add(new TerrainTextureAssetIssue(shader.Name, slot.Index, slot.FileName,
                        slot.IsImplicit, "Terrain texture was not found in route TerrTex content."));
                else
                    found.AddRange(matches);
            }
        }

        return new TerrainTextureAssetDiscovery(found, missing);
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string Shader, int Slot, string FileName)>
    {
        public static StringTupleComparer Instance { get; } = new();

        public bool Equals((string Shader, int Slot, string FileName) x, (string Shader, int Slot, string FileName) y) =>
            x.Slot == y.Slot &&
            StringComparer.OrdinalIgnoreCase.Equals(x.Shader, y.Shader) &&
            StringComparer.OrdinalIgnoreCase.Equals(x.FileName, y.FileName);

        public int GetHashCode((string Shader, int Slot, string FileName) value) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.Shader),
                value.Slot,
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.FileName));
    }
}

public sealed record TerrainTextureAssetReference(
    string ShaderName,
    int TextureSlotIndex,
    string FileName,
    string? SeasonFolder,
    string Path,
    bool IsImplicitOverlay);

public sealed record TerrainTextureAssetIssue(
    string ShaderName,
    int TextureSlotIndex,
    string FileName,
    bool IsImplicitOverlay,
    string Reason);

public sealed class TerrainTextureAssetDiscovery
{
    public IReadOnlyList<TerrainTextureAssetReference> Found { get; }
    public IReadOnlyList<TerrainTextureAssetIssue> Missing { get; }

    public TerrainTextureAssetDiscovery(IEnumerable<TerrainTextureAssetReference> found,
        IEnumerable<TerrainTextureAssetIssue> missing)
    {
        Found = Array.AsReadOnly(found.ToArray());
        Missing = Array.AsReadOnly(missing.ToArray());
    }
}

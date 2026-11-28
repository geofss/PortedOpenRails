using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>Decodes discovered route terrain ACE assets while isolating per-file failures.</summary>
public sealed class TerrainTextureAssetLoader
{
    public TerrainTextureAssetLoadResult Load(
        IVirtualContentStore store,
        TerrainTextureAssetDiscovery discovery) => Load(store, discovery, CancellationToken.None);

    public TerrainTextureAssetLoadResult Load(
        IVirtualContentStore store,
        TerrainTextureAssetDiscovery discovery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(discovery);

        var loaded = new List<LoadedTerrainTextureAsset>();
        var failed = new List<TerrainTextureLoadIssue>();
        var reader = new AceTextureReader();

        foreach (var asset in discovery.Found)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                loaded.Add(new LoadedTerrainTextureAsset(asset, reader.Read(store, asset.Path)));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
            {
                failed.Add(new TerrainTextureLoadIssue(asset.Path, exception.Message));
            }
        }

        return new TerrainTextureAssetLoadResult(loaded, discovery.Missing, failed);
    }
}

public sealed record LoadedTerrainTextureAsset(TerrainTextureAssetReference Reference, AceTextureData Texture);
public sealed record TerrainTextureLoadIssue(string Path, string Reason);

public sealed class TerrainTextureAssetLoadResult
{
    public IReadOnlyList<LoadedTerrainTextureAsset> Loaded { get; }
    public IReadOnlyList<TerrainTextureAssetIssue> Missing { get; }
    public IReadOnlyList<TerrainTextureLoadIssue> Failed { get; }

    public TerrainTextureAssetLoadResult(
        IEnumerable<LoadedTerrainTextureAsset> loaded,
        IEnumerable<TerrainTextureAssetIssue> missing,
        IEnumerable<TerrainTextureLoadIssue> failed)
    {
        Loaded = Array.AsReadOnly(loaded.ToArray());
        Missing = Array.AsReadOnly(missing.ToArray());
        Failed = Array.AsReadOnly(failed.ToArray());
    }
}

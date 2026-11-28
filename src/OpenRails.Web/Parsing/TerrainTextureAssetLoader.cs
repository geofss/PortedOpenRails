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
        CancellationToken cancellationToken) =>
        Load(store, discovery, long.MaxValue, cancellationToken);

    /// <summary>
    /// Decodes textures in discovery order while retaining no more than the supplied
    /// decoded-byte budget. A single texture is decoded before its mip footprint is
    /// known, but rejected textures are not retained in the result.
    /// </summary>
    public TerrainTextureAssetLoadResult Load(
        IVirtualContentStore store,
        TerrainTextureAssetDiscovery discovery,
        long maximumDecodedBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(discovery);
        if (maximumDecodedBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumDecodedBytes));

        var loaded = new List<LoadedTerrainTextureAsset>();
        var failed = new List<TerrainTextureLoadIssue>();
        var reader = new AceTextureReader();
        long retainedBytes = 0;

        foreach (var asset in discovery.Found)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var texture = reader.Read(store, asset.Path);
                var textureBytes = texture.MipLevels.Aggregate(0L,
                    (total, mip) => checked(total + mip.LongLength));
                if (textureBytes > maximumDecodedBytes - retainedBytes)
                {
                    failed.Add(new TerrainTextureLoadIssue(asset.Path,
                        $"Decoded texture exceeds the remaining viewport texture budget ({maximumDecodedBytes - retainedBytes} bytes remain)."));
                    continue;
                }

                loaded.Add(new LoadedTerrainTextureAsset(asset, texture));
                retainedBytes += textureBytes;
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or OverflowException)
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

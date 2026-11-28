using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>Decodes ACE images referenced by shape primitives; reports unsupported DDS files separately.</summary>
public sealed class TrackShapeTextureAssetLoader
{
    public TrackShapeTextureAssetLoadResult Load(
        IVirtualContentStore store,
        TrackShapeTextureAssetDiscovery discovery) =>
        Load(store, discovery, long.MaxValue, CancellationToken.None);

    /// <summary>
    /// Decodes referenced ACE textures in discovery order while retaining no more
    /// than the supplied decoded-byte budget. A texture is decoded before its mip
    /// footprint is known, but assets outside the budget are not kept in the result.
    /// </summary>
    public TrackShapeTextureAssetLoadResult Load(
        IVirtualContentStore store,
        TrackShapeTextureAssetDiscovery discovery,
        long maximumDecodedBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(discovery);
        if (maximumDecodedBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumDecodedBytes));

        var loaded = new List<LoadedTrackShapeTexture>();
        var failed = new List<TrackShapeTextureLoadIssue>();
        var reader = new AceTextureReader();
        long retainedBytes = 0;

        foreach (var asset in discovery.Found)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!asset.Path.EndsWith(".ace", StringComparison.OrdinalIgnoreCase))
            {
                failed.Add(new TrackShapeTextureLoadIssue(asset.Path,
                    "This texture format is not yet supported by the browser texture decoder."));
                continue;
            }

            try
            {
                var texture = reader.Read(store, asset.Path);
                var textureBytes = texture.MipLevels.Aggregate(0L,
                    (total, mip) => checked(total + mip.LongLength));
                if (textureBytes > maximumDecodedBytes - retainedBytes)
                {
                    failed.Add(new TrackShapeTextureLoadIssue(asset.Path,
                        $"Decoded texture exceeds the remaining rolling-stock texture budget ({maximumDecodedBytes - retainedBytes} bytes remain)."));
                    continue;
                }

                loaded.Add(new LoadedTrackShapeTexture(asset, texture));
                retainedBytes += textureBytes;
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or OverflowException)
            {
                failed.Add(new TrackShapeTextureLoadIssue(asset.Path, exception.Message));
            }
        }

        return new TrackShapeTextureAssetLoadResult(loaded, discovery.Missing, failed);
    }
}

public sealed record LoadedTrackShapeTexture(TrackShapeTextureAssetReference Reference, AceTextureData Texture);
public sealed record TrackShapeTextureLoadIssue(string Path, string Reason);

public sealed class TrackShapeTextureAssetLoadResult
{
    public IReadOnlyList<LoadedTrackShapeTexture> Loaded { get; }
    public IReadOnlyList<TrackShapeTextureAssetIssue> Missing { get; }
    public IReadOnlyList<TrackShapeTextureLoadIssue> Failed { get; }

    public TrackShapeTextureAssetLoadResult(
        IEnumerable<LoadedTrackShapeTexture> loaded,
        IEnumerable<TrackShapeTextureAssetIssue> missing,
        IEnumerable<TrackShapeTextureLoadIssue> failed)
    {
        Loaded = Array.AsReadOnly(loaded.ToArray());
        Missing = Array.AsReadOnly(missing.ToArray());
        Failed = Array.AsReadOnly(failed.ToArray());
    }
}

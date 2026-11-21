using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>Decodes ACE images referenced by shape primitives; reports unsupported DDS files separately.</summary>
public sealed class TrackShapeTextureAssetLoader
{
    public TrackShapeTextureAssetLoadResult Load(
        IVirtualContentStore store,
        TrackShapeTextureAssetDiscovery discovery)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(discovery);

        var loaded = new List<LoadedTrackShapeTexture>();
        var failed = new List<TrackShapeTextureLoadIssue>();
        var reader = new AceTextureReader();

        foreach (var asset in discovery.Found)
        {
            if (!asset.Path.EndsWith(".ace", StringComparison.OrdinalIgnoreCase))
            {
                failed.Add(new TrackShapeTextureLoadIssue(asset.Path,
                    "This texture format is not yet supported by the browser texture decoder."));
                continue;
            }

            try
            {
                loaded.Add(new LoadedTrackShapeTexture(asset, reader.Read(store, asset.Path)));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
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

using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>Reads original MSTS/Open Rails .s shape geometry from virtual content.</summary>
public sealed class TrackShapeFileReader
{
    public shape Read(IVirtualContentStore store, TrackShapeAssetReference asset, bool suppressShapeWarnings = true)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(asset);
        using var stream = store.OpenRead(asset.Path);
        return new ShapeFile(stream, asset.Path, suppressShapeWarnings).shape;
    }
}

using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>Resolves WagonShape references relative to the owning TRAINSET folder.</summary>
public sealed class RollingStockShapeAssetLocator
{
    public RollingStockShapeAssetReference? Locate(IVirtualContentStore store, string trainsetFolder, string? shapeFileName)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (string.IsNullOrWhiteSpace(shapeFileName))
            return null;

        var fileName = shapeFileName.Trim().Replace('\\', '/');
        if (fileName.StartsWith('/') || fileName.Contains(':') ||
            fileName.Split('/').Any(segment => segment is "." or ".."))
            throw new InvalidDataException($"WagonShape reference is not a safe relative path: '{shapeFileName}'.");

        var folder = VirtualPath.Normalize($"TRAINS/TRAINSET/{trainsetFolder}");
        var candidate = VirtualPath.Normalize($"{folder}/{fileName}");
        if (!store.Contains(candidate))
            return null;

        return new RollingStockShapeAssetReference(shapeFileName, candidate);
    }
}

public sealed record RollingStockShapeAssetReference(string FileName, string Path);

/// <summary>Loads an original rolling-stock .s shape through the shared Open Rails shape and mesh pipeline.</summary>
public sealed class RollingStockShapeAssetLoader
{
    public TrackShapeMesh Load(IVirtualContentStore store, RollingStockShapeAssetReference asset)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(asset);
        var shape = new TrackShapeFileReader().Read(store,
            new TrackShapeAssetReference(0, asset.FileName, asset.Path));
        return new TrackShapeMeshBuilder().Build(shape);
    }
}

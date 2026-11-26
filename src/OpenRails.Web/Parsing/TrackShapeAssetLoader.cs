using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>Loads one discovered original .s asset and converts it into portable mesh data.</summary>
public sealed class TrackShapeAssetLoader
{
    public TrackShapeAssetMesh Load(IVirtualContentStore store, TrackShapeAssetReference asset)
    {
        var shape = new TrackShapeFileReader().Read(store, asset);
        var mesh = new TrackShapeMeshBuilder().Build(shape);
        return new TrackShapeAssetMesh(asset.ShapeIndex, asset.FileName, asset.Path, mesh);
    }
}

public sealed record TrackShapeAssetMesh(
    uint ShapeIndex,
    string FileName,
    string SourcePath,
    TrackShapeMesh Mesh);

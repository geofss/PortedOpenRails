using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Builds one scenery chunk on demand: WORLD placements, original .s meshes,
/// route texture references, and decoded ACE textures.
/// </summary>
public sealed class WorldSceneryTileLoader
{
    public LoadedWorldSceneryTile Load(
        IVirtualContentStore store,
        string routeRoot,
        WorldTileReference reference)
    {
        ArgumentNullException.ThrowIfNull(store);
        var worldTile = new WorldTileContentLoader().Load(store, reference);
        var shapeDiscovery = new WorldShapeAssetLocator().Locate(store, routeRoot, new[] { worldTile });
        var shapeMeshes = new List<TrackShapeAssetMesh>();
        var failedShapes = new List<WorldShapeMeshIssue>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in shapeDiscovery.Found)
        {
            if (!seenPaths.Add(asset.Path))
                continue;

            var fileName = asset.FileName.Replace('\\', '/').Split('/').Last();
            try
            {
                shapeMeshes.Add(new TrackShapeAssetLoader().Load(
                    store,
                    new TrackShapeAssetReference(asset.ObjectUid, fileName, asset.Path)));
            }
            catch (Exception exception)
            {
                failedShapes.Add(new WorldShapeMeshIssue(asset.ObjectUid, asset.Path, exception.Message));
            }
        }

        var textureDiscovery = new TrackShapeTextureAssetLocator().Locate(store, routeRoot, shapeMeshes);
        var textures = new TrackShapeTextureAssetLoader().Load(store, textureDiscovery);
        var placements = new WorldObjectPlacementBuilder().Build(worldTile);
        var forestTextureDiscovery = new ForestTextureAssetLocator().Locate(store, routeRoot, placements);
        var forestTextures = new TrackShapeTextureAssetLoader().Load(store, forestTextureDiscovery);

        return new LoadedWorldSceneryTile(
            worldTile,
            placements,
            shapeDiscovery,
            shapeMeshes,
            failedShapes,
            textureDiscovery,
            textures,
            forestTextureDiscovery,
            forestTextures);
    }
}

public sealed record WorldShapeMeshIssue(uint ObjectUid, string Path, string Reason);

public sealed record LoadedWorldSceneryTile(
    WorldTileData Tile,
    IReadOnlyList<WorldObjectPlacement> Placements,
    WorldShapeAssetDiscovery ShapeDiscovery,
    IReadOnlyList<TrackShapeAssetMesh> ShapeMeshes,
    IReadOnlyList<WorldShapeMeshIssue> FailedShapes,
    TrackShapeTextureAssetDiscovery TextureDiscovery,
    TrackShapeTextureAssetLoadResult Textures,
    TrackShapeTextureAssetDiscovery ForestTextureDiscovery = null!,
    TrackShapeTextureAssetLoadResult ForestTextures = null!);

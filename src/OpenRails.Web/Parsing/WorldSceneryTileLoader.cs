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
        WorldTileReference reference,
        long maximumTextureBytes = 128L * 1024 * 1024,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (maximumTextureBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumTextureBytes));
        cancellationToken.ThrowIfCancellationRequested();
        var worldTile = new WorldTileContentLoader().Load(store, reference);
        cancellationToken.ThrowIfCancellationRequested();
        var shapeDiscovery = new WorldShapeAssetLocator().Locate(store, routeRoot, new[] { worldTile });
        var shapeMeshes = new List<TrackShapeAssetMesh>();
        var failedShapes = new List<WorldShapeMeshIssue>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in shapeDiscovery.Found)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
        var textureLoader = new TrackShapeTextureAssetLoader();
        var textures = textureLoader.Load(store, textureDiscovery, maximumTextureBytes, cancellationToken);
        var retainedShapeTextureBytes = textures.Loaded.Aggregate(0L, (total, asset) =>
            checked(total + asset.Texture.MipLevels.Aggregate(0L,
                (mipTotal, mip) => checked(mipTotal + mip.LongLength))));
        var remainingTextureBytes = maximumTextureBytes - retainedShapeTextureBytes;

        cancellationToken.ThrowIfCancellationRequested();
        var placements = new WorldObjectPlacementBuilder().Build(worldTile);
        var forestTextureDiscovery = new ForestTextureAssetLocator().Locate(store, routeRoot, placements);
        var forestTextures = textureLoader.Load(store, forestTextureDiscovery, remainingTextureBytes, cancellationToken);

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

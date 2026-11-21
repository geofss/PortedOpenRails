using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Loads one WORLD tile on demand and resolves every original shape referenced by
/// its static and track objects without eagerly loading the entire route's scenery.
/// </summary>
public sealed class RouteWorldTileLoader
{
    public LoadedRouteWorldTile Load(
        IVirtualContentStore store,
        string routeRoot,
        WorldTileReference reference)
    {
        ArgumentNullException.ThrowIfNull(store);
        var tile = new WorldTileContentLoader().Load(store, reference);
        var shapes = new WorldShapeAssetLocator().Locate(store, routeRoot, new[] { tile });
        return new LoadedRouteWorldTile(tile, shapes);
    }
}

public sealed record LoadedRouteWorldTile(
    WorldTileData Tile,
    WorldShapeAssetDiscovery ShapeAssets);

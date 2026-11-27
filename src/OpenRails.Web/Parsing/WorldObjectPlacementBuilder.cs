namespace OpenRails.Web.Parsing;

/// <summary>
/// Converts tile-relative WORLD object positions to the same 2048-metre global
/// coordinate space used by TDB track geometry. Orientation data is preserved
/// unchanged for the renderer to apply using Open Rails' matrix conventions.
/// </summary>
public sealed class WorldObjectPlacementBuilder
{
    private const double TileSizeMeters = 2048;

    public IReadOnlyList<WorldObjectPlacement> Build(WorldTileData tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        var placements = new List<WorldObjectPlacement>();

        foreach (var item in tile.StaticObjects)
        {
            placements.Add(new WorldObjectPlacement(
                item.Uid,
                item.ObjectKind,
                item.FileName,
                item.Position is null ? null : ToWorldPosition(tile.TileX, tile.TileZ, item.Position),
                item.Direction,
                item.Matrix,
                item.StaticFlags,
                item.WorldDatabaseId,
                null,
                0,
                0,
                null,
                item.TreeTexture,
                item.ForestScaleRange,
                item.ForestArea,
                item.ForestPopulation,
                item.ForestTreeSize));
        }

        foreach (var item in tile.TrackObjects)
        {
            WorldTilePosition? junction = item.JunctionPosition is null
                ? null
                : ToWorldPosition(item.JunctionPosition.TileX, item.JunctionPosition.TileZ,
                    new WorldTilePosition(item.JunctionPosition.X, item.JunctionPosition.Y, item.JunctionPosition.Z));

            placements.Add(new WorldObjectPlacement(
                item.Uid,
                "TrackObj",
                item.FileName,
                item.Position is null ? null : ToWorldPosition(tile.TileX, tile.TileZ, item.Position),
                item.Direction,
                null,
                item.StaticFlags,
                item.WorldDatabaseId,
                item.SectionIndex,
                item.Elevation,
                item.CollideFlags,
                junction));
        }

        return Array.AsReadOnly(placements.ToArray());
    }

    private static WorldTilePosition ToWorldPosition(int tileX, int tileZ, WorldTilePosition local) =>
        new((float)(tileX * TileSizeMeters + local.X), local.Y, (float)(tileZ * TileSizeMeters + local.Z));
}

public sealed record WorldObjectPlacement(
    uint Uid,
    string ObjectKind,
    string? FileName,
    WorldTilePosition? WorldPosition,
    WorldQuaternion? Direction,
    WorldMatrix3x3? Matrix,
    uint StaticFlags,
    uint WorldDatabaseId,
    uint? TrackSectionIndex,
    float Elevation,
    uint CollideFlags,
    WorldTilePosition? JunctionWorldPosition,
    string? TreeTexture = null,
    WorldFloatRange? ForestScaleRange = null,
    WorldFloatPair? ForestArea = null,
    int? ForestPopulation = null,
    WorldFloatPair? ForestTreeSize = null);

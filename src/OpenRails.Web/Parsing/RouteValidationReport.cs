namespace OpenRails.Web.Parsing;

/// <summary>
/// Structural route-load validation for imported content. This reports data-pipeline
/// readiness and parser diagnostics; fixture validation is not a substitute for
/// verification against the authorized Demo Model 1 route.
/// </summary>
public sealed record RouteValidationIssue(string Category, string Message);

public sealed record RouteValidationReport(
    bool TrackNetworkUsable,
    int VectorPathCount,
    double TotalTrackLengthMeters,
    int TrackDiagnosticsCount,
    int MissingTerrainTiles,
    int FailedTerrainTiles,
    int MissingTrackShapes,
    int FailedTrackShapes,
    int MissingWorldTiles,
    int FailedWorldTiles,
    IReadOnlyList<RouteValidationIssue> Issues)
{
    public bool HasCriticalIssues => !TrackNetworkUsable;
    public int AssetIssueCount => MissingTerrainTiles + FailedTerrainTiles +
        MissingTrackShapes + FailedTrackShapes + MissingWorldTiles + FailedWorldTiles;

    public static RouteValidationReport Build(
        Simulation.TrackNetwork network,
        RouteTerrainTileDiscovery terrainDiscovery,
        RouteTerrainMeshLoadResult terrainMeshes,
        TrackShapeAssetDiscovery trackShapeDiscovery,
        RouteTrackShapeMeshLoadResult trackShapeMeshes,
        RouteWorldTileDiscovery worldDiscovery,
        RouteWorldSceneryLoadResult worldScenery)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(terrainDiscovery);
        ArgumentNullException.ThrowIfNull(terrainMeshes);
        ArgumentNullException.ThrowIfNull(trackShapeDiscovery);
        ArgumentNullException.ThrowIfNull(trackShapeMeshes);
        ArgumentNullException.ThrowIfNull(worldDiscovery);
        ArgumentNullException.ThrowIfNull(worldScenery);

        var issues = new List<RouteValidationIssue>();
        var length = network.TotalTrackLengthMeters;
        var usable = network.VectorPathCount > 0 && double.IsFinite(length) && length > 0;
        if (!usable)
            issues.Add(new("Track network", "No usable vector track paths or finite positive total track length were produced."));
        foreach (var diagnostic in network.Diagnostics)
            issues.Add(new("Track network", diagnostic));

        foreach (var missing in terrainDiscovery.Missing)
            issues.Add(new("Terrain", $"Missing terrain tile {missing.TileX},{missing.TileZ}: {missing.ExpectedPath}"));
        foreach (var failed in terrainMeshes.Failed)
            issues.Add(new("Terrain", $"Failed terrain tile {failed.TileX},{failed.TileZ}: {failed.Reason}"));
        foreach (var missing in trackShapeDiscovery.Missing)
            issues.Add(new("Track shape", $"Missing shape {missing.ShapeIndex}: {missing.FileName}"));
        foreach (var failed in trackShapeMeshes.FailedMeshes)
            issues.Add(new("Track shape", $"Failed shape {failed.ShapeIndex}: {failed.Reason}"));
        foreach (var missing in worldDiscovery.Missing)
            issues.Add(new("WORLD", $"Missing world tile {missing.TileX},{missing.TileZ}"));
        foreach (var failed in worldScenery.Failed)
            issues.Add(new("WORLD", $"Failed world tile {failed.TileX},{failed.TileZ}: {failed.Reason}"));

        return new RouteValidationReport(
            usable, network.VectorPathCount, length, network.Diagnostics.Count,
            terrainDiscovery.Missing.Count, terrainMeshes.Failed.Count,
            trackShapeDiscovery.Missing.Count, trackShapeMeshes.FailedMeshes.Count,
            worldDiscovery.Missing.Count, worldScenery.Failed.Count,
            Array.AsReadOnly(issues.ToArray()));
    }
}

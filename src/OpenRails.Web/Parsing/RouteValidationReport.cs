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
    int MissingWorldShapes,
    int FailedWorldShapes,
    int MissingTextures,
    int FailedTextures,
    IReadOnlyList<RouteValidationIssue> Issues,
    int TrackDatabaseDiagnosticsCount = 0)
{
    public bool HasCriticalIssues => !TrackNetworkUsable;
    public int AssetIssueCount => MissingTerrainTiles + FailedTerrainTiles +
        MissingTrackShapes + FailedTrackShapes + MissingWorldTiles + FailedWorldTiles +
        MissingWorldShapes + FailedWorldShapes + MissingTextures + FailedTextures;

    public static RouteValidationReport Build(
        TrackDatabase trackDatabase,
        Simulation.TrackNetwork network,
        RouteTerrainTileDiscovery terrainDiscovery,
        RouteTerrainMeshLoadResult terrainMeshes,
        TrackShapeAssetDiscovery trackShapeDiscovery,
        RouteTrackShapeMeshLoadResult trackShapeMeshes,
        RouteWorldTileDiscovery worldDiscovery,
        RouteWorldSceneryLoadResult worldScenery,
        TerrainTextureAssetLoadResult terrainTextures)
    {
        ArgumentNullException.ThrowIfNull(trackDatabase);
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(terrainDiscovery);
        ArgumentNullException.ThrowIfNull(terrainMeshes);
        ArgumentNullException.ThrowIfNull(trackShapeDiscovery);
        ArgumentNullException.ThrowIfNull(trackShapeMeshes);
        ArgumentNullException.ThrowIfNull(worldDiscovery);
        ArgumentNullException.ThrowIfNull(worldScenery);
        ArgumentNullException.ThrowIfNull(terrainTextures);

        var issues = new List<RouteValidationIssue>();
        var length = network.TotalTrackLengthMeters;
        var hasValidPathLength = network.VectorPathCount > 0 && double.IsFinite(length) && length > 0;
        var criticalTopologyDiagnostics = network.Diagnostics.Count(diagnostic =>
            diagnostic.Contains("contains no vector sections", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("distinct connected nodes; expected 2", StringComparison.OrdinalIgnoreCase));
        var usable = hasValidPathLength && criticalTopologyDiagnostics == 0;
        if (!hasValidPathLength)
            issues.Add(new("Track network", "No usable vector track paths or finite positive total track length were produced."));
        if (criticalTopologyDiagnostics > 0)
            issues.Add(new("Track network", $"{criticalTopologyDiagnostics} vector track path(s) have critical topology problems (empty geometry or a pin-link count other than two)."));
        foreach (var diagnostic in trackDatabase.Diagnostics)
            issues.Add(new("Track database", diagnostic));
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
        foreach (var tile in worldScenery.Loaded)
        {
            foreach (var missing in tile.ShapeDiscovery.Missing)
                issues.Add(new("WORLD shape", $"Missing world shape {missing.FileName}: {missing.Reason}"));
            foreach (var failed in tile.FailedShapes)
                issues.Add(new("WORLD shape", $"Failed world shape {failed.Path}: {failed.Reason}"));
            foreach (var missing in tile.Textures.Missing)
                issues.Add(new("Texture", $"Missing world shape texture {missing.ImageName}: {missing.Reason}"));
            foreach (var failed in tile.Textures.Failed)
                issues.Add(new("Texture", $"Failed world shape texture {failed.Path}: {failed.Reason}"));
        }
        foreach (var missing in terrainTextures.Missing)
            issues.Add(new("Texture", $"Missing terrain texture {missing.FileName} for shader {missing.ShaderName}: {missing.Reason}"));
        foreach (var failed in terrainTextures.Failed)
            issues.Add(new("Texture", $"Failed terrain texture {failed.Path}: {failed.Reason}"));
        foreach (var missing in trackShapeMeshes.Textures.Missing)
            issues.Add(new("Texture", $"Missing track-shape texture {missing.ImageName}: {missing.Reason}"));
        foreach (var failed in trackShapeMeshes.Textures.Failed)
            issues.Add(new("Texture", $"Failed track-shape texture {failed.Path}: {failed.Reason}"));

        return new RouteValidationReport(
            usable, network.VectorPathCount, length, network.Diagnostics.Count,
            terrainDiscovery.Missing.Count, terrainMeshes.Failed.Count,
            trackShapeDiscovery.Missing.Count, trackShapeMeshes.FailedMeshes.Count,
            worldDiscovery.Missing.Count, worldScenery.Failed.Count,
            worldScenery.Loaded.Sum(tile => tile.ShapeDiscovery.Missing.Count),
            worldScenery.Loaded.Sum(tile => tile.FailedShapes.Count),
            terrainTextures.Missing.Count + trackShapeMeshes.Textures.Missing.Count +
                worldScenery.Loaded.Sum(tile => tile.Textures.Missing.Count),
            terrainTextures.Failed.Count + trackShapeMeshes.Textures.Failed.Count +
                worldScenery.Loaded.Sum(tile => tile.Textures.Failed.Count),
            Array.AsReadOnly(issues.ToArray()),
            trackDatabase.Diagnostics.Count);
    }
}

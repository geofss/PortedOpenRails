using System.Numerics;
using OpenRails.Web.Simulation;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Joins the fixed-step consist motion state to real rolling-stock shape placements
/// and animated, distance-selected textured geometry. Call once per render frame,
/// passing the simulation clock rather than a wall-clock timer.
/// </summary>
public sealed class RollingStockRenderFrameBuilder
{
    public RollingStockRenderFrame Build(
        ConsistMetadata consist,
        IReadOnlyList<ConsistCarAssetReference> carAssets,
        IReadOnlyDictionary<int, TrackShapeAssetMesh> meshesByUid,
        TrackVectorPath routePath,
        TrainConsistMotionState motionState,
        IReadOnlyList<LoadedTrackShapeTexture> textures,
        double simulationTimeSeconds,
        int animationClipIndex = 0,
        bool loopAnimations = true,
        string? seasonFolder = null,
        Vector3? cameraPosition = null,
        double maximumSegmentLengthMeters = 2)
    {
        ArgumentNullException.ThrowIfNull(consist);
        ArgumentNullException.ThrowIfNull(carAssets);
        ArgumentNullException.ThrowIfNull(meshesByUid);
        ArgumentNullException.ThrowIfNull(routePath);
        ArgumentNullException.ThrowIfNull(motionState);
        ArgumentNullException.ThrowIfNull(textures);
        if (!double.IsFinite(simulationTimeSeconds) || simulationTimeSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(simulationTimeSeconds));

        var placements = new RollingStockTrackPlacementBuilder().BuildAtMotionState(
            consist, carAssets, meshesByUid, routePath, motionState, maximumSegmentLengthMeters);
        IReadOnlyDictionary<int, Matrix4x4>? WheelOverrides(TrackShapePlacement placement)
        {
            if (placement.VehicleUid is not { } uid) return null;
            var car = consist.Cars.FirstOrDefault(item => item.Uid == uid);
            var reference = carAssets.FirstOrDefault(item => item.Uid == uid &&
                string.Equals(item.ConsistName, consist.Name, StringComparison.OrdinalIgnoreCase));
            return RollingStockWheelPoseBuilder.Build(placement.Asset.Mesh, reference?.Metadata,
                car?.IsEngine ?? false, placement.RouteDistanceMeters);
        }
        var geometry = new TrackShapeRenderGeometryBuilder().BuildTextured(
            placements.Placements,
            textures,
            seasonFolder: seasonFolder,
            animationClipIndex: animationClipIndex,
            cameraPosition: cameraPosition,
            elapsedSeconds: simulationTimeSeconds,
            loopAnimation: loopAnimations,
            matrixOverridesProvider: WheelOverrides);

        return new RollingStockRenderFrame(placements, geometry, simulationTimeSeconds);
    }

    public RollingStockRenderFrame Build(
        ConsistMetadata consist,
        IReadOnlyList<ConsistCarAssetReference> carAssets,
        IReadOnlyDictionary<int, TrackShapeAssetMesh> meshesByUid,
        TrackNetworkRoute route,
        TrainConsistMotionState motionState,
        IReadOnlyList<LoadedTrackShapeTexture> textures,
        double simulationTimeSeconds,
        int animationClipIndex = 0,
        bool loopAnimations = true,
        string? seasonFolder = null,
        Vector3? cameraPosition = null,
        double maximumSegmentLengthMeters = 2)
    {
        ArgumentNullException.ThrowIfNull(consist);
        ArgumentNullException.ThrowIfNull(carAssets);
        ArgumentNullException.ThrowIfNull(meshesByUid);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(motionState);
        ArgumentNullException.ThrowIfNull(textures);
        if (!double.IsFinite(simulationTimeSeconds) || simulationTimeSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(simulationTimeSeconds));

        var placements = new RollingStockTrackPlacementBuilder().BuildAtMotionState(
            consist, carAssets, meshesByUid, route, motionState, maximumSegmentLengthMeters);
        var geometry = new TrackShapeRenderGeometryBuilder().BuildTextured(
            placements.Placements,
            textures,
            seasonFolder: seasonFolder,
            animationClipIndex: animationClipIndex,
            cameraPosition: cameraPosition,
            elapsedSeconds: simulationTimeSeconds,
            loopAnimation: loopAnimations);

        return new RollingStockRenderFrame(placements, geometry, simulationTimeSeconds);
    }

    public RollingStockRenderFrame Build(
        ConsistMetadata consist,
        IReadOnlyList<ConsistCarAssetReference> carAssets,
        IReadOnlyDictionary<int, TrackShapeAssetMesh> meshesByUid,
        TrackVectorPath routePath,
        TrainConsistSimulationSession simulation,
        IReadOnlyList<LoadedTrackShapeTexture> textures,
        int animationClipIndex = 0,
        bool loopAnimations = true,
        string? seasonFolder = null,
        Vector3? cameraPosition = null,
        double maximumSegmentLengthMeters = 2)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        return Build(
            consist, carAssets, meshesByUid, routePath,
            simulation.State, textures, simulation.SimulationTimeSeconds,
            animationClipIndex, loopAnimations, seasonFolder, cameraPosition,
            maximumSegmentLengthMeters);
    }
}

public sealed record RollingStockRenderFrame(
    RollingStockTrackPlacementResult Placement,
    TrackShapeTexturedRenderGeometry Geometry,
    double SimulationTimeSeconds);

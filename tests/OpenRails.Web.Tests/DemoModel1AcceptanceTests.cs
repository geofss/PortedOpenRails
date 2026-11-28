using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

/// <summary>
/// Runs the real authorized Demo Model 1 route through the production route loader.
/// The route source is downloaded by the dedicated GitHub Actions acceptance job.
/// </summary>
public sealed class DemoModel1AcceptanceTests
{
    [Fact]
    public async Task LoadsScottishCapitalExpressFromTheAuthorizedRoutePackage()
    {
        var root = Environment.GetEnvironmentVariable("DEMO_MODEL1_ROOT");
        // Other CI jobs compile and run the shared test project without downloading
        // the large route. The dedicated acceptance job always sets this variable.
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("Skipped: the dedicated Demo Model 1 acceptance job supplies DEMO_MODEL1_ROOT.");
            return;
        }
        Assert.True(Directory.Exists(root), $"Demo Model 1 source directory does not exist: {root}");

        using var store = new DirectoryVirtualContentStore(root!);
        const string routePath = "ROUTES/SCE/SCE.trk";

        Assert.True(store.Contains(routePath), "The authorized package is missing ROUTES/SCE/SCE.trk.");
        Assert.True(store.Contains("ROUTES/SCE/SCE.tdb"), "The authorized package is missing the SCE track database.");
        Assert.True(store.Contains("ROUTES/SCE/TSECTION.DAT"), "The authorized package is missing its route-specific TSECTION.DAT.");
        Assert.True(store.Contains("GLOBAL/TSECTION.DAT"), "The authorized package is missing GLOBAL/TSECTION.DAT.");

        var metadata = new RouteMetadataReader().Read(store, routePath);
        Assert.Equal("SCE", metadata.RouteId);
        Assert.Equal("Scottish Capital Express", metadata.Name);
        Assert.False(metadata.Electrified);
        Assert.Equal(new RouteStartCoordinates(-6144, 15144, 877.176, -869.309), metadata.RouteStart);

        var route = new RouteLoader().Load(store, routePath, metadata, RouteLoadOptions.ViewportStreaming);

        Assert.True(route.TrackDatabase.NodeCount > 1000,
            $"Expected the real SCE track database, got {route.TrackDatabase.NodeCount} nodes.");
        Assert.True(route.TrackNetwork.VectorPathCount > 0,
            "The real SCE track database produced no connected vector paths.");
        Assert.True(route.TrackNetwork.TotalTrackLengthMeters > 0,
            "The real SCE track network has no positive track length.");
        Assert.Equal(route.TrackNetwork.VectorPaths.Count, route.Signalling.TrackCircuits.Count);
        Assert.NotEmpty(route.Signalling.TrackCircuits);
        Assert.True(route.TerrainTiles.Found.Count > 0, "No SCE terrain tiles were discovered.");
        Assert.True(route.TrackShapeAssets.Found.Count > 0, "No SCE track-shape assets were discovered.");
        Assert.True(route.WorldTiles.Found.Count > 0, "No SCE WORLD tiles were discovered.");
        Assert.Empty(route.TerrainMeshes.Meshes); // Terrain mesh decoding is deferred to the camera viewport session.
        Assert.Empty(route.WorldScenery.Loaded); // WORLD decoding is deferred to the camera viewport session.

        // Exercise camera-scoped terrain source loading against the real route.
        // Moving outside the route must evict the previous CPU-side tile working set.
        var terrainAnchor = route.TerrainTiles.Found[0];
        await using var terrainStreaming = new RouteTerrainViewportStreamingSession(
            maximumResidentTiles: 16,
            maximumResidentBytes: 1024L * 1024 * 1024,
            estimatedBytesPerTile: 64L * 1024 * 1024);
        var streamedTerrain = await terrainStreaming.UpdateCameraAsync(
            store, route.Assets.RouteRoot, route.TerrainTiles,
            terrainAnchor.TileX * RouteViewportTileSelector.TileSizeMeters,
            terrainAnchor.TileZ * RouteViewportTileSelector.TileSizeMeters,
            radiusMeters: 0);
        Assert.NotEmpty(streamedTerrain.Meshes);
        Assert.True(streamedTerrain.ResidentTileCount <= 16);
        Assert.True(streamedTerrain.ResidentBytes <= 1024L * 1024 * 1024);

        var outsideRoute = await terrainStreaming.UpdateCameraAsync(
            store, route.Assets.RouteRoot, route.TerrainTiles,
            cameraX: 10_000_000, cameraZ: 10_000_000, radiusMeters: 0);
        Assert.Empty(outsideRoute.Meshes);
        Assert.Equal(0, terrainStreaming.ResidentTileCount);
        Assert.Equal(0, terrainStreaming.ResidentBytes);

        // Exercise viewport-scoped WORLD parsing, shape/texture decoding and eviction
        // against the authorized SCE WORLD archive entries.
        var worldAnchor = route.WorldTiles.Found[0];
        await using var worldStreaming = new RouteWorldViewportStreamingSession(
            maximumResidentTiles: 16,
            maximumResidentBytes: 2L * 1024 * 1024 * 1024,
            estimatedBytesPerTile: 16L * 1024 * 1024);
        var streamedWorld = await worldStreaming.UpdateCameraAsync(
            store, route.Assets.RouteRoot, route.WorldTiles,
            worldAnchor.TileX * RouteViewportTileSelector.TileSizeMeters,
            worldAnchor.TileZ * RouteViewportTileSelector.TileSizeMeters,
            radiusMeters: 0);
        Assert.NotEmpty(streamedWorld.Loaded);
        Assert.True(streamedWorld.ResidentBytes <= 2L * 1024 * 1024 * 1024);
        Assert.True(streamedWorld.ResidentTileCount <= 16);

        var outsideWorld = await worldStreaming.UpdateCameraAsync(
            store, route.Assets.RouteRoot, route.WorldTiles,
            cameraX: 10_000_000, cameraZ: 10_000_000, radiusMeters: 0);
        Assert.Empty(outsideWorld.Loaded);
        Assert.Equal(0, worldStreaming.ResidentTileCount);
        Assert.Equal(0, worldStreaming.ResidentBytes);

        // Exercise the production interlocking and timetable together on real SCE
        // track-circuit IDs. Two services request the same block at the same
        // departure time: exactly one may dispatch, and the other must remain pending.
        var interlocking = route.Signalling.CreateInterlockingController();
        var firstCircuit = route.Signalling.TrackCircuits
            .Select(circuit => circuit.TrackNodeIndex)
            .First();
        var firstService = new AiServiceInterlockingSession("SCE-service-A", interlocking);
        var secondService = new AiServiceInterlockingSession("SCE-service-B", interlocking);
        var timetable = new AiTimetableDispatcher();
        var sharedRoute = new[] { firstCircuit };
        var noJunctionOverrides = new Dictionary<uint, uint>();
        timetable.AddService("SCE-service-A", 0, firstService, sharedRoute, noJunctionOverrides);
        timetable.AddService("SCE-service-B", 0, secondService, sharedRoute, noJunctionOverrides);

        var firstDispatch = timetable.Advance(0);
        Assert.Equal(new[] { "SCE-service-A" }, firstDispatch);
        Assert.Equal(AiTimetableServiceStatus.Dispatched,
            timetable.GetServiceStatus("SCE-service-A"));
        Assert.Equal(AiTimetableServiceStatus.Pending,
            timetable.GetServiceStatus("SCE-service-B"));
        Assert.True(firstService.IsDispatched);
        Assert.False(secondService.IsDispatched);

        // Occupancy is tied to the dispatched train identity; a second train
        // cannot claim the same real route circuit while the first occupies it.
        Assert.True(interlocking.SetOccupied("SCE-service-A", firstCircuit, true));
        Assert.False(interlocking.SetOccupied("SCE-service-B", firstCircuit, true));
        Assert.False(interlocking.TryReleaseRoute("SCE-service-A"));
        Assert.True(interlocking.SetOccupied("SCE-service-A", firstCircuit, false));
        Assert.True(interlocking.TryReleaseRoute("SCE-service-A"));

        var retryDispatch = timetable.Advance(0);
        Assert.Equal(new[] { "SCE-service-B" }, retryDispatch);
        Assert.Equal(AiTimetableServiceStatus.Dispatched,
            timetable.GetServiceStatus("SCE-service-B"));
        Assert.True(secondService.TryComplete());

        // Build two real rolling-stock consists from the authorized package and
        // drive them through the AI dispatcher, physics runtime and train-following
        // governor on an actual SCE vector path.
        var consistCatalog = new ConsistCatalogReader().Read(store);
        var operationalConsist = consistCatalog.Consists.FirstOrDefault(consist =>
            consist.Cars.Any(car => car.IsEngine && !car.IsEndOfTrain) &&
            consist.Cars.Where(car => !car.IsEndOfTrain).All(car =>
                consistCatalog.CarAssets.Any(asset => asset.ConsistName == consist.Name &&
                    asset.Uid == car.Uid && asset.Metadata is not null)));
        Assert.NotNull(operationalConsist);
        Assert.Contains(consistCatalog.CarAssets, asset =>
            asset.ConsistName == operationalConsist!.Name && asset.IsEngine &&
            asset.TractionMetadata is not null);

        var consistLengthMeters = operationalConsist!.Cars
            .Where(car => !car.IsEndOfTrain)
            .Sum(car =>
            {
                var length = consistCatalog.CarAssets.First(asset =>
                    asset.ConsistName == operationalConsist.Name && asset.Uid == car.Uid).Metadata!.LengthM;
                return (double)(float.IsFinite(length) && length > 0 ? length : 10f);
            });
        var drivingPath = route.TrackNetwork.VectorPaths
            .Where(path => path.Sections.Count > 0 &&
                path.LengthMeters > 2 * consistLengthMeters + 100)
            .OrderByDescending(path => path.LengthMeters)
            .FirstOrDefault();
        Assert.NotNull(drivingPath);
        var selectedPath = drivingPath!;
        var motionRoute = new TrackNetworkRoute(
            new[] { new TrackNetworkRouteSegment(selectedPath, IsReversed: false) },
            Array.Empty<string>(), selectedPath.LengthMeters);
        var pathStart = selectedPath.Sections[0].Start;
        var setupBuilder = new TrainConsistSimulationSetupBuilder();
        var followerSetup = setupBuilder.Build(
            operationalConsist, consistCatalog.CarAssets, selectedPath.LengthMeters,
            startDistanceMeters: 0);
        var leaderSetup = setupBuilder.Build(
            operationalConsist, consistCatalog.CarAssets, selectedPath.LengthMeters,
            startDistanceMeters: consistLengthMeters + 40);
        var followerDriver = new BrowserTrainDrivingController(followerSetup, route: motionRoute);
        var leaderDriver = new BrowserTrainDrivingController(leaderSetup, route: motionRoute);
        var followerInitialDistance = followerDriver.State.Cars[0].DistanceMeters;
        var leaderInitialDistance = leaderDriver.State.Cars[0].DistanceMeters;
        Assert.True(leaderInitialDistance > followerInitialDistance);

        ServiceRouteStartPosition MakeServicePosition(double distance) =>
            new(distance, 0, pathStart,
                new[] { distance, selectedPath.LengthMeters }, 1,
                new[]
                {
                    new ServiceRouteWaypoint(distance, false, false),
                    new ServiceRouteWaypoint(selectedPath.LengthMeters, false, false)
                });
        var followerRoute = MakeServicePosition(followerInitialDistance);
        var leaderRoute = MakeServicePosition(leaderInitialDistance);

        var multiTrainInterlocking = route.Signalling.CreateInterlockingController();
        var multiTrainCircuits = route.Signalling.TrackCircuits
            .Select(circuit => circuit.TrackNodeIndex).Distinct().ToArray();
        Assert.True(multiTrainCircuits.Length >= 3);
        var followerCircuit = multiTrainCircuits[0];
        var leaderCircuit = multiTrainCircuits.First(circuit => circuit != followerCircuit);
        var spareCircuit = multiTrainCircuits.First(circuit =>
            circuit != followerCircuit && circuit != leaderCircuit);
        var followerService = new AiServiceInterlockingSession("SCE-consist-follower", multiTrainInterlocking);
        var leaderService = new AiServiceInterlockingSession("SCE-consist-leader", multiTrainInterlocking);
        var multiTrainTimetable = new AiTimetableDispatcher();
        var noJunctions = new Dictionary<uint, uint>();
        multiTrainTimetable.AddService("SCE-consist-follower", 0, followerService,
            new[] { followerCircuit }, noJunctions);
        multiTrainTimetable.AddService("SCE-consist-leader", 0, leaderService,
            new[] { leaderCircuit }, noJunctions);
        var multiDispatch = multiTrainTimetable.Advance(0);
        Assert.Equal(2, multiDispatch.Count);
        Assert.True(followerService.IsDispatched);
        Assert.True(leaderService.IsDispatched);

        var aiRuntime = new AiTimetableSimulationRuntime(multiTrainTimetable,
            new[]
            {
                new AiTimetableTrainRuntimeBinding(
                    "SCE-consist-follower", "SCE-main", followerService,
                    followerDriver, followerRoute, TargetSpeedMetersPerSecond: 60),
                new AiTimetableTrainRuntimeBinding(
                    "SCE-consist-leader", "SCE-main", leaderService,
                    leaderDriver, leaderRoute, TargetSpeedMetersPerSecond: 8)
            });
        AiServiceControlDecision? followerDecision = null;
        for (var step = 0; step < 120; step++)
        {
            var tick = aiRuntime.Advance(1d / 30d);
            var followerDecisionIndex = tick.DecisionServiceIds
                .ToList().FindIndex(serviceId => serviceId == "SCE-consist-follower");
            if (followerDecisionIndex >= 0)
                followerDecision = tick.Decisions[followerDecisionIndex];
        }

        Assert.True(leaderDriver.State.Cars[0].DistanceMeters > leaderInitialDistance);
        Assert.True(followerDriver.State.Cars[0].DistanceMeters > followerInitialDistance);
        Assert.NotNull(followerDecision);
        var followingConstraint = AiTrainFollowingResolver.Resolve(
            "SCE-consist-follower",
            followerDriver.State.Cars[0].DistanceMeters,
            direction: 1,
            new[]
            {
                new AiServiceTrainState("SCE-consist-leader",
                    leaderDriver.State.Cars[0].DistanceMeters,
                    Math.Max(0, leaderDriver.State.Cars[0].SpeedMetersPerSecond))
            });
        Assert.NotNull(followingConstraint);
        var safeFollowingSpeed = Math.Sqrt(
            Math.Pow(leaderDriver.State.Cars[0].SpeedMetersPerSecond, 2) +
            2 * 0.65 * Math.Max(0, followingConstraint!.DistanceAheadMeters - 20));
        Assert.True(followerDecision!.TargetSpeedMetersPerSecond <= safeFollowingSpeed + 1.0,
            "The follower's AI speed envelope must respect the leading consist's safe following distance.");
        Assert.True(followerService.TryChangeRoute(
            new[] { followerCircuit, spareCircuit }, noJunctions));
        Assert.True(followerService.TryComplete());
        Assert.True(leaderService.TryComplete());
        multiTrainTimetable.Advance(0);
        Assert.Equal(AiTimetableServiceStatus.Completed,
            multiTrainTimetable.GetServiceStatus("SCE-consist-follower"));
        Assert.Equal(AiTimetableServiceStatus.Completed,
            multiTrainTimetable.GetServiceStatus("SCE-consist-leader"));
        Assert.Empty(multiTrainInterlocking.GetDeadlockedTrainIds());

        Console.WriteLine(
            $"Demo Model 1 acceptance: nodes={route.TrackDatabase.NodeCount}; " +
            $"paths={route.TrackNetwork.VectorPathCount}; length={route.TrackNetwork.TotalTrackLengthMeters:N0}m; " +
            $"terrain={streamedTerrain.Meshes.Count}; trackShapes={route.TrackShapeMeshes.LoadedMeshes.Count}; " +
            $"world={streamedWorld.Loaded.Count}; signalCircuits={route.Signalling.TrackCircuits.Count}; " +
            $"signalHeads={route.Signalling.TrackCircuits.Sum(circuit => circuit.Signals.Count)}; " +
            $"signalDiagnostics={route.Signalling.Diagnostics.Count}; validationIssues={route.Validation.Issues.Count}; " +
            $"trackNetworkUsable={route.Validation.TrackNetworkUsable}.");
    }
}

/// <summary>Read-only directory adapter used only by the real-route acceptance test.</summary>
internal sealed class DirectoryVirtualContentStore : IVirtualContentStore, IDisposable
{
    private readonly string root;
    private readonly Dictionary<string, string> files;

    public IReadOnlyCollection<string> Paths { get; }

    public DirectoryVirtualContentStore(string root)
    {
        this.root = Path.GetFullPath(root);
        if (!Directory.Exists(this.root))
            throw new DirectoryNotFoundException(this.root);

        files = Directory.EnumerateFiles(this.root, "*", SearchOption.AllDirectories)
            .ToDictionary(
                file => VirtualPath.Normalize(Path.GetRelativePath(this.root, file).Replace(Path.DirectorySeparatorChar, '/')),
                file => file,
                StringComparer.OrdinalIgnoreCase);
        Paths = Array.AsReadOnly(files.Keys.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public bool Contains(string path) => files.ContainsKey(VirtualPath.Normalize(path));

    public Stream OpenRead(string path)
    {
        var normalized = VirtualPath.Normalize(path);
        return files.TryGetValue(normalized, out var file)
            ? File.OpenRead(file)
            : throw new FileNotFoundException($"Virtual content file not found: {normalized}", normalized);
    }

    public bool TryOpenRead(string path, out Stream? stream)
    {
        var normalized = VirtualPath.Normalize(path);
        if (files.TryGetValue(normalized, out var file))
        {
            stream = File.OpenRead(file);
            return true;
        }

        stream = null;
        return false;
    }

    public void Dispose()
    {
        // File streams are owned by the readers that open them.
    }
}

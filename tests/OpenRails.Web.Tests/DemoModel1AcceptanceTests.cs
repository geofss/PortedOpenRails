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

        var route = new RouteLoader().Load(store, routePath, metadata);

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
        Assert.True(route.TerrainMeshes.Meshes.Count > 0, "No SCE terrain meshes were built.");
        Assert.True(route.WorldScenery.Loaded.Count > 0, "No SCE WORLD scenery tiles were parsed.");

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

        Console.WriteLine(
            $"Demo Model 1 acceptance: nodes={route.TrackDatabase.NodeCount}; " +
            $"paths={route.TrackNetwork.VectorPathCount}; length={route.TrackNetwork.TotalTrackLengthMeters:N0}m; " +
            $"terrain={route.TerrainMeshes.Meshes.Count}; trackShapes={route.TrackShapeMeshes.LoadedMeshes.Count}; " +
            $"world={route.WorldScenery.Loaded.Count}; signalCircuits={route.Signalling.TrackCircuits.Count}; " +
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

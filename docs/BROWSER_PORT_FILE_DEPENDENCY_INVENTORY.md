# Browser-port file-level dependency inventory

**Scope:** Current `src/OpenRails.Web` implementation (browser port), plus upstream dependency seams identified in `docs/MILESTONE_2A_CONTENT_PIPELINE_AUDIT.md`.
**Purpose:** Record file responsibilities and their main dependency boundaries so future portability work can be targeted. This is an inventory of the browser port—not a claim that the entire upstream desktop parser/simulation tree has been audited line-by-line.

## Host and UI
- `App.razor` — Blazor app entry/layout composition.
- `Program.cs` — WebAssembly host and service configuration.
- `Components/RouteTerrainViewer.razor` — route/terrain preview lifecycle and rendering integration; depends on route-loading results and JS renderer.
- `Components/RollingStockViewer.razor` — rolling-stock mesh preview; consumes portable shape/texture assets.
- `Components/TrainDrivingControls.razor` — player control UI; invokes browser driving controller.
- `wwwroot/js/route-terrain-renderer.js` — WebGL2 rendering, browser canvas, GPU resources; intentionally JS boundary.
- `wwwroot/css/app.css`, `wwwroot/index.html`, `_Imports.razor` — styles, browser shell, Razor imports.

## Content and virtual IO
- `Content/IZipContentStore.cs` — virtual-content abstraction and ZIP package access contracts.
- `Content/BrowserZipPackageLoader.cs` — browser-selected ZIP input adapter; browser file APIs are the boundary.
- `Content/CompositeContentStore.cs` — layered namespace, deterministic first-layer precedence, optional ownership/disposal.
- `Content/VirtualPath.cs` — logical path normalization and relative include resolution; security-sensitive traversal checks.
- `Content/VirtualContentStreamReaderFactory.cs` — UTF-8/BOM and SIMIS-signature handling, virtual stream factory for STF parsing, include resolver.
- Browser content is user-imported and read-only; no filesystem or registry discovery. Package entry size/path constraints belong to ZIP indexing and virtual-path validation.

## Text parsers and format readers
- `Parsing/STFReader.cs`, `Parsing/StfReaderAccess.cs`, `Parsing/TokenID.cs` — upstream STF text parsing, token processing and controlled factory access; STFReader remains upstream and uses a shared stream-reader factory seam.
- `Parsing/RouteMetadataReader.cs` — route TRK metadata parser; uses STF parser + virtual content factory.
- `Parsing/ConsistReader.cs`, `ConsistFileReader.cs`, `ConsistCatalogReader.cs` — consist list/file parsing and catalog discovery; depend on STF/virtual content and rolling-stock metadata.
- `Parsing/RollingStockMetadataReader.cs`, `LocomotiveTractionMetadataReader.cs` — vehicle metadata and traction fields; feed vehicle and simulation models.
- `Parsing/TrackDatabaseReader.cs` — TDB node/vector/junction/pin text data; uses STFReader, virtual store and portable route-domain records.
- `Parsing/TrackSectionGeometryReader.cs` — global/route TSECTION geometry catalogs; input to topology/route construction.
- `Parsing/RouteLoader.cs`, `RouteAssetLocator.cs` — orchestration and path discovery; combines route metadata, TDB, TSECTION, terrain, track shapes, textures and WORLD scenery.
- `Parsing/RouteTerrainTileLocator.cs`, `TerrainTileReader.cs`, `TerrainRawFileReaders.cs`, `TerrainTileSampleResolver.cs`, `TerrainPatchMaterialBuilder.cs`, `TerrainMeshBuilder.cs`, `RouteTerrainMeshLoader.cs` — terrain tile discovery, SIMISA/SBR stream decoding, samples/patches, cross-tile sample resolution and mesh/material generation.
- `Parsing/TerrainTextureAssetLocator.cs`, `TerrainTextureAssetLoader.cs`, `AceTextureReader.cs` — terrain texture discovery and supported ACE decoding.
- `Parsing/TrackShapeAssetLocator.cs`, `TrackShapeAssetLoader.cs`, `TrackShapeFileReader.cs`, `ShapeFile.cs`, `TrackShapeMeshBuilder.cs`, `TrackShapeRenderGeometryBuilder.cs`, `TrackShapePlacementBuilder.cs`, `TrackShapeTextureAssetLocator.cs`, `TrackShapeTextureAssetLoader.cs` — shape parsing, portable mesh conversion, track placement, texture lookup and render batch generation. Shape binary parsing uses SBR stream APIs; mesh output is portable data.
- `Parsing/RouteWorldTileLocator.cs`, `WorldTileReader.cs`, `WorldSceneryTileLoader.cs`, `WorldShapeAssetLocator.cs`, `WorldObjectPlacementBuilder.cs`, `WorldSceneryRenderGeometryBuilder.cs` — WORLD scenery parsing, asset discovery and portable placement/render geometry.
- `Parsing/RollingStockShapeAssetLoader.cs`, `RollingStockTextureAssetLocator.cs`, `RollingStockTrackPlacementBuilder.cs` — vehicle shape/texture loading and route-aligned placements.
- `Parsing/SBR.cs` — SIMISA binary reader with stream entry point; stream compression/format variants still require broader route-content validation.
- `Parsing/Interpolator.cs` — small interpolation utility used by parsing/simulation code.
- Text and binary readers rely on System.IO/Compression and upstream parser code; portable browser content is supplied by IVirtualContentStore rather than desktop paths.

## Simulation core
- `Simulation/FixedStepSimulationScheduler.cs` — fixed-step clock, bounded catch-up, pause/resume/reset and dropped-time accounting.
- `Simulation/TrainCarMotionIntegrator.cs`, `TrainCarForceModel.cs`, `TrainCarResistanceModel.cs`, `TrainCarSimulationLoop.cs` — single-car motion, force aggregation/resistance and loop integration.
- `Simulation/TrainCarBrakeModel.cs`, `TrainBrakePipeModel.cs`, `TrainBrakeCylinderModel.cs` — brake force evaluator and prototype pneumatic pipe/cylinder response.
- `Simulation/TrainCarAdhesionModel.cs` — quasi-static traction ceiling (not wheel-slip dynamics).
- `Simulation/LocomotiveTractionModel.cs`, `LocomotiveSimulationSession.cs`, `AccelerationController.cs` — traction metadata/curve evaluation and single-car session/controller.
- `Simulation/TrainConsistMotionIntegrator.cs`, `TrainConsistSimulationLoop.cs`, `TrainConsistSimulationSession.cs`, `TrainConsistSimulationSetupBuilder.cs` — multi-car state, fixed-step loop, per-car traction/braking/resistance, adhesion, coupler force modes and setup mapping.
- `Simulation/TrainConsistCouplerDynamics.cs`, `TrainConsistCouplerForceDistributor.cs`, `TrainCouplerSlackModel.cs`, `TrainCouplerForceLimit.cs` — coupler forces, distribution, slack approximation and ratings; native Open Rails parameter parsing/validation remains incomplete.
- `Simulation/TrackGeometryTessellator.cs`, `TrackNetworkBuilder.cs`, `TrackNetworkRouteBuilder.cs`, `TrackNetworkRouteProfile.cs` — portable topology/route geometry and sampled grade/heading profiles.
- `Simulation/BrowserTrainDrivingController.cs` — browser driving command orchestration and telemetry; depends on setup, simulation models, route profile and UI-facing state.

## Dependency and portability boundaries
- Browser host: Blazor WebAssembly / .NET 10; current project references WebAssembly packages only.
- Imported route files: local, user-selected ZIP content accessed through virtual streams. Route data is untrusted; path normalization, size limits, parser bounds and failure isolation are critical.
- Parser inheritance: upstream STFReader and SBR are reused/adapted; avoid allowing desktop file paths, Registry, WindowsDX/MonoGame, or graphics types to leak into browser-only parser seams.
- Renderer: JavaScript/WebGL2 is isolated under `wwwroot/js`; C# parsing and simulation output portable data rather than direct GPU calls.
- Native Open Rails dependencies still not ported: broad desktop parser/format dependencies, full physics/control systems, signal/activity/timetable/AI systems, native UI/cab, sound and multiplayer. The upstream dependency map remains a separate audit task; this document is scoped to the current browser-port files.

## Inventory completeness and next audit
This inventory covers all source files currently under `src/OpenRails.Web` from the repository tree and groups them by role. It records the browser port's direct seams and known upstream dependency risks. A full upstream file-level audit must still enumerate every parser/format/IO/simulation-core source file and its exact external references before deciding which assemblies or source units can be ported.

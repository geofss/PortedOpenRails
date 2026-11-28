# Native route, terrain, and rolling-stock parity audit

Branch: `port/axle-drive-state-hardening`

This is a source audit, not a claim of completed parity or a substitute for a build and route-runtime test. The browser port must preserve the upstream Open Rails data model and integrate every subsystem with the runtime; avoid placeholder implementations.

## Current source-confirmed route pipeline

- `RouteLoader.Load` currently orchestrates route metadata, TDB, global/route TSECTION catalogs, track network, signal configuration/scripts/network, terrain tile discovery/loading/mesh generation, track-shape mesh discovery/loading/placement, WORLD tile discovery/loading/scenery, texture resolution, and validation.
- `TrackDatabaseReader` currently recognizes these track-item table blocks: crossover, signal, speedpost, platform, sound region, empty, level crossing, siding, hazard, and pickup. Audit against upstream TDB item types and ensure all upstream types are retained, typed, and runtime-consumed. Do not silently drop unknown or newly encountered record types; capture them as diagnostics/raw data where feasible.
- Track-item parsing retains fields including position, platform/siding name, station name, waiting-time/passenger metadata, speedpost flags/value/display number/angle, signal type/flags/directions, crossover node/shape, sound-region fields, and linked-item IDs. Validate semantics and connect to downstream UI/simulation consumers.
- `RouteValidationReport` counts terrain, track-shape, WORLD, world-shape, and texture failures; it is a structural validation report, not a proof of visual or simulation parity.

## Required native-route closure

- Compare parser token coverage with upstream Open Rails TDB/route classes and supported record variants.
- Verify all TrItem types and metadata are represented and consumed: signal aspects/scripts and junction directions, speed restriction and warning/milepost variants, platform/station facilities, siding links, road-crossing gates/road database, sound regions, pickup/hazard/event items, and route-specific extensions.
- Check topology invariants, pin directionality, junction state/shape, crossover geometry, path/route connectivity, and link resolution.
- Ensure route asset resolver handles case-insensitive paths, aliases, relative references, texture variants, and missing-asset diagnostics consistently.
- Route-wide acceptance must exercise the authorized Demo Model 1 content, not only fixtures.

## Terrain and scenery parity closure

- Compare terrain parser, tile discovery, boundary sample resolution, patch LOD mesh generation, normals, texture/material/shader metadata, seasonal variants, and forest/vegetation behavior with upstream.
- Verify WORLD object parsing, placement transforms, shape hierarchy, materials, ACE texture handling, animation, visibility, and tile boundaries.
- Route-wide streaming needs bounded memory, progressive loading, cancellation, and deterministic unload/reload. Large desktop route packages are the target.
- Acceptance: terrain/scenery visual comparison in multiple route locations and seasons; report all missing/failed assets without masking them.

## Rolling-stock parity closure

- Audit `.eng`, `.wag`, `.s`, `.ace`, .sms` and related config parsing against upstream. Ensure every native asset reference resolves through the content store.
- Complete shape hierarchy, matrices/transforms, material/texture state, animation tracks, wheels/axles/bogies, suspension, couplers, locomotive/wagon metadata, and consist placement.
- Integrate cab interiors, gauges, controls, audio definitions/assets, and runtime state with the simulation—not just the viewer.
- Acceptance: representative locomotive and wagon assets loaded, animated, placed, driven by the browser simulation, and validated in browser runtime.

## Integration and verification gate

1. Implement missing upstream coverage in the existing modules; avoid parallel simplified parsers/renderers.
2. Add regression tests for every added record/asset and malformed inputs.
3. Build and run test suites in a real build environment; this session cannot execute `dotnet` or browser end-to-end tests.
4. Import Demo Model 1, verify full route discovery and asset diagnostics, inspect distant tiles and route features, then exercise rolling-stock/cab/simulation integration.
5. Do not call any subsystem complete until runtime integration and acceptance evidence exist.


## Additional source-audit findings (verified by file inspection)

- `TrackShapeMeshBuilder.BuildVertices` now retains every source UV channel. The WebGL2 shape renderer currently consumes up to four channels; additional channels are preserved in portable geometry but not yet rendered.
- `TrackShapeMeshBuilder.BuildPrimitive` now retains all primitive texture slots and material descriptors. The WebGL2 renderer currently consumes up to four texture stages and only a subset of native UV/material/render-state operations; lighting, reflection maps, border color, mip bias and complete depth/blend state remain open.
- Rolling-stock metadata reader retains a focused subset of wagon/engine fields (dimensions, mass, axles/wheels, brakes, coupler and Davis resistance, shape reference). Expand upstream field coverage and runtime consumers, including locomotive-specific controls/forces and animation/cab/audio data.
- `RollingStockShapeAssetLoader` reuses the track-shape parsing and mesh builder; this is a good shared foundation, but shared parser reuse alone does not demonstrate rolling-stock-specific hierarchy/animation/material behavior.


## Implemented during the ongoing parity work

- Added portable native animation clip/node/controller/keyframe extraction, including TCB key parameters.
- Added linear-position interpolation and quaternion slerp between adjacent tcb_rot keys, matching the upstream Viewer3D Shapes.cs implementation. TCB parameters remain preserved in parsed key data; the native rotation path does not apply a generic TCB spline.
- Added a simulation-time-to-frame clock with looping and non-looping behavior.
- Added named-node pose evaluation and applied sampled transforms in track-shape and WORLD render hierarchy traversal.
- Added elapsed-simulation-time inputs to both render builders. Added a rolling-stock render-frame orchestrator that connects consist motion state, route placement, elapsed simulation time, animation sampling, season selection, and camera-distance LOD selection.
- Added distance-based LOD selection for track shapes and WORLD scenery, choosing the nearest viewing-distance threshold and handling unordered source levels.
- Added camera-driven runtime refresh: the WebGL renderer reports its world camera position to RouteTerrainViewer after camera movement; the component rebuilds terrain patchsets, track-shape LODs and WORLD scenery LODs for that camera position.
- Added terrain patchset LOD selection based on camera-to-tile distance and regression tests for unordered thresholds, fallback, empty levels and invalid distances.
- The application passes the live BrowserTrainDrivingController simulation clock into the route viewer. Rolling-stock updates use RollingStockRenderFrameBuilder on connected routes, apply simulation-time shape animation and select vehicle LOD from camera distance. Multi-UV/material/texture-slot data is forwarded in initial and dynamic render batches.
- Rolling-stock placements retain car UID and route chainage. `.wag` wheel radius and `.eng` driver-wheel radius are parsed, and WHEELS matrix nodes below the native height cutoff receive route-distance-driven rotation in initial rendering, camera LOD rebuilds and simulation updates.
- BOGIE/BOGIE1/BOGIE2 matrices receive route-tangent yaw from their longitudinal offset, with flipped-car offset handling. Wheel and bogie matrix overrides are merged with shape animation poses before hierarchy transforms are composed.
- Shape mesh extraction now preserves every source vertex UV channel, all primitive texture slots with filter/LOD-bias/border metadata, UV operation metadata, light-material values, and additional primitive/vertex/light-state flags. Track-shape and WORLD render batches also carry per-channel UV arrays and primitive material descriptors, and material-state differences are kept in separate batches.
- Added tests for upstream tcb_rot slerp behavior, simulation-time frame selection, loop/clamp behavior, and LOD threshold selection.
- The WebGL2 shape renderer now consumes up to four UV channels and four texture slots per batch, applies copy/uniform-scale/nonuniform-scale UV operations, uses per-stage address/filter settings, and respects alpha-test and the available Z-buffer write state.
- Signal snapshot evaluation now merges occupancy with reservations owned by other trains before fixed-block aspect evaluation. The evaluating train's own reserved circuits remain available to its native signal-script context. Add integration coverage for overlapping reservations, route release, and live multi-train signal transitions.
- AI speed restriction handling now uses a distance-aware braking envelope for each upcoming configured SIGCFG speed limit. Validate against native Open Rails speedpost/signal braking behavior, including multiple restrictions, service deceleration changes, and reverse path direction.
- Terrain preview geometry budget now prioritizes the nearest camera tiles before selecting patches, with stable coordinate tie-breaking. This improves local coverage when the geometry cap is reached; it does not replace progressive route tile loading, cancellation, unload/reload, bounded decoded-asset memory, or GPU resource eviction.
- **Still incomplete:** compare frame selection, quaternion coordinate conversion and endpoint behavior against upstream reference fixtures; driven axle angular state/wheel slip, suspension, coupler/air-hose articulation, freight/control-driven animation and native rolling-stock sound playback remain. Browser visual acceptance for representative consists is still required. Camera-driven LOD currently rebuilds geometry from already loaded route assets; asynchronous tile/asset loading, cancellation, unload/reload and bounded GPU resource eviction remain outstanding. Reflection-map UV operations, native shader variants, lighting/light-material evaluation, border-color behavior, mip-bias handling, full Z-buffer modes and GPU reference-image parity remain outstanding.
- **Verification:** commit a467d8c765b19414d4c24977fa716fa7933c1de3 passed WebAssembly publish and preview (run 46089953902), 711 controller regressions, 147 route/terrain/scenery/LOD/rolling-stock regressions and authorized Demo Model 1 load/validation acceptance (run 46089953875). These results do not establish full browser visual parity, route-scale streaming, playable train simulation, or cab/audio acceptance.

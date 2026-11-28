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

- `TrackShapeMeshBuilder.BuildVertices` currently takes only the first UV entry for each vertex (or defaults to UV 0,0 when none exists). Confirm upstream multi-UV semantics and port all required channels where the rendering/material pipeline needs them.
- `TrackShapeMeshBuilder.BuildPrimitive` currently resolves the first texture index from primitive state. Audit multiple-texture and material/shader combinations, texture address modes, light configurations, and alpha/render states against upstream before claiming shape parity.
- Rolling-stock metadata reader retains a focused subset of wagon/engine fields (dimensions, mass, axles/wheels, brakes, coupler and Davis resistance, shape reference). Expand upstream field coverage and runtime consumers, including locomotive-specific controls/forces and animation/cab/audio data.
- `RollingStockShapeAssetLoader` reuses the track-shape parsing and mesh builder; this is a good shared foundation, but shared parser reuse alone does not demonstrate rolling-stock-specific hierarchy/animation/material behavior.


## Implemented during the ongoing parity work

- Added portable native animation clip/node/controller/keyframe extraction, including TCB key parameters.
- Added linear-position interpolation and quaternion slerp between adjacent tcb_rot keys, matching the upstream Viewer3D Shapes.cs implementation. TCB parameters remain preserved in parsed key data; the native rotation path does not apply a generic TCB spline.
- Added a simulation-time-to-frame clock with looping and non-looping behavior.
- Added named-node pose evaluation and applied sampled transforms in track-shape and WORLD render hierarchy traversal.
- Added elapsed-simulation-time inputs to both render builders. Added a rolling-stock render-frame orchestrator that connects consist motion state, route placement, elapsed simulation time, animation sampling, season selection, and camera-distance LOD selection.
- Added distance-based LOD selection for track shapes and WORLD scenery, choosing the nearest viewing-distance threshold and handling unordered source levels.
- Shape mesh extraction now preserves every source vertex UV channel, all primitive texture slots with filter/LOD-bias/border metadata, UV operation metadata, light-material values, and additional primitive/vertex/light-state flags. Track-shape and WORLD render batches also carry per-channel UV arrays and primitive material descriptors, and material-state differences are kept in separate batches.
- Added tests for upstream tcb_rot slerp behavior, simulation-time frame selection, loop/clamp behavior, and LOD threshold selection.
- The WebGL2 shape renderer now consumes up to four UV channels and four texture slots per batch, applies copy/uniform-scale/nonuniform-scale UV operations, uses per-stage address/filter settings, and respects alpha-test and the available Z-buffer write state.
- **Still incomplete:** compare frame selection, quaternion coordinate conversion, and endpoint behavior against upstream reference fixtures; the browser application must supply its live simulation clock and camera to the render-frame builder; train/wheel/cab-specific clip binding and animation controllers are not yet complete. Reflection-map UV operations, native shader variants, lighting/light-material evaluation, border-color behavior, mip-bias handling, full Z-buffer modes and GPU reference-image parity remain outstanding. Native TCB parity requires comparison against upstream reference outputs across irregular frame intervals and rotation edge cases.
- **Verification:** source changes and test source are committed, but this environment cannot execute dotnet or browser end-to-end tests. No build/test pass is asserted.

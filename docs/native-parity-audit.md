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

- Added portable native animation clip/node/controller/keyframe data extraction for the upstream shape model.
- Preserved linear-position keyframes and rotation keyframes (slerp and TCB key records) in the portable mesh model.
- Added position interpolation and quaternion slerp sampling utility, with unit tests for interpolation, clamping, and invalid controller-kind usage.
- Added a pose evaluator that maps named animation nodes to shape-matrix indices and samples per-node position/rotation transforms for a selected clip/frame.
- Wired sampled poses into both untextured and textured track-shape render geometry hierarchy transforms. The builder accepts an animation clip index and frame; omitted/default values preserve static geometry.
- Added regression tests for named-node mapping, sampled translation, unmatched helper nodes, missing clips, and non-finite frame rejection.
- **Still incomplete:** WORLD scenery geometry does not yet consume animation poses; runtime clock/frame advancement, train/wheel/cab animation binding, TCB rotational interpolation, and material/UV parity remain outstanding. The render builder currently selects the first LOD and does not yet implement dynamic LOD selection.
- Test source has been added but cannot be executed in this environment; no build/test pass is asserted.

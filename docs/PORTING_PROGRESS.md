# PortedOpenRails — coordinated porting status

Last updated: 2026-11-28
Working branch: port/axle-drive-state-hardening
Draft integration PR: https://github.com/geofss/PortedOpenRails/pull/4
Related draft PR: https://github.com/geofss/PortedOpenRails/pull/3
Latest implementation checkpoint: 353c8bcf4d9e531f1f361790b708c9662fecf372

## Project rules

- Genuine desktop-browser port of Open Rails, not a redesign or mock simulation.
- Preserve the original Open Rails UI and interaction model.
- Use real, human-made route content. Demo Model 1 permission was granted by the Open Rails Management Team; acknowledge Making Tracks Ltd copyright.
- AI-assisted implementation and mathematical work are allowed.
- Keep GPL-3.0-or-later and upstream copyright notices intact.
- Desktop browsers are the target. Large imported route packages may exceed 5 GB; mobile is only a preview/test surface unless the user changes scope.
- Do not merge PR #4 or PR #3, change main, or deploy without explicit user authorization and completed integration/release checks.

## Completion contract: three systems, six closure tracks

All three systems are to be developed together. A parser, fixture test, audit, or isolated demo does not make a system complete.

### Route system
- TDB/TSECTION parsing, topology, connectivity and path traversal.
- Junctions, signals, signal scripts/aspects, occupancy/interlocking.
- Platforms, sidings, speed restrictions, sound regions, hazards, pickups, crossings and route events.
- Real route asset resolution and route-wide Demo Model 1 validation.
- Browser route operation with track geometry, signals and route features connected to runtime consumers.

### Terrain and scenery system
- Terrain tile/sample parsing, boundary resolution, mesh generation and normals.
- Terrain materials, textures, shader state and season switching.
- WORLD tile parsing, object placement, track objects, forests and shape hierarchy.
- Dynamic LOD, progressive streaming, bounded memory, cancellation and unload/reload.
- Visual comparison against the authorized route at multiple locations and seasons.

### Rolling-stock system
- ENG, WAG, S, ACE, SMS and related metadata/assets.
- Full shape hierarchy, transforms, materials, UV sets, animations, wheels, bogies and suspension.
- Train physics, traction, adhesion, couplers/slack, pneumatic brakes and route-constrained movement.
- Cab interior, instruments, controls, audio definitions/assets and simulation bindings.
- Actual browser simulation using real locomotive and wagon assets.

### Six named closure tracks
1. Runtime-driven animation, including simulation clock, vehicle/scenery clip binding and wheel/cab controllers.
2. Native TCB rotation interpolation, validated against upstream reference results and irregular keyframe intervals.
3. Full material and multi-UV rendering: all texture slots, UV operations, shader variants, lighting, alpha and Z-buffer state.
4. Dynamic LOD and route-scale streaming, with camera-driven selection, progressive load/unload and performance acceptance.
5. Broader route integration: all TDB item classes, topology/junctions, signals/interlocking, crossings, platforms, restrictions and route events consumed by runtime.
6. Broader rolling-stock integration: native assets, train physics, route placement, vehicle animations, cab and audio all connected in browser runtime.

## Current implementation snapshot

### Animation and rendering foundations added
- Portable native animation clip/node/controller/keyframe extraction, including TCB parameters.
- Linear-position interpolation and adjacent-key quaternion slerp for tcb_rot, matching the upstream Viewer3D Shapes.cs implementation. TCB key parameters remain preserved, but the native rotation path uses slerp rather than a generic TCB spline.
- Animation frame clock driven by elapsed simulation seconds, with looping and non-looping behavior.
- Named animation nodes mapped to shape matrix indices and sampled poses applied in track-shape and WORLD shape hierarchy transforms.
- Track-shape and WORLD geometry builders accept elapsed simulation time.
- Rolling-stock render-frame builder connects consist motion state, route placement, simulation time, shape animation, season selection and camera-distance LOD.
- Distance-based LOD selector integrated into track-shape and WORLD scenery geometry builders.
- Route viewer now receives the WebGL camera's world position through a JS-to-.NET callback. Camera movement rebuilds selected terrain patchsets, track-shape LODs and WORLD scenery LODs instead of leaving the initial LOD frozen.
- Terrain LOD selection chooses the source patchset whose distance threshold contains the camera-to-tile distance; a dedicated regression suite covers unordered levels, nearest-threshold selection, empty inputs and invalid distances.
- The live browser simulation clock is passed from BrowserTrainDrivingController into RouteTerrainViewer. Rolling-stock render updates now use RollingStockRenderFrameBuilder for connected routes, simulation-time animation and camera-distance LOD.
- Render batch payloads now carry per-slot texture indexes, every retained UV channel and material descriptors through initial draw and live rolling-stock updates.
- Track-shape and WORLD render batches carry full UV-channel arrays, per-slot texture indexes and primitive material descriptors; material-state differences are split into separate batches.
- The WebGL2 renderer consumes up to four UV channels and four texture slots per batch, supports copy/uniform-scale/nonuniform-scale UV operations, per-stage addressing/filter settings, alpha test and the available Z-buffer write state.
- Rolling-stock render-frame builder has an overload that accepts the live TrainConsistSimulationSession and reads its simulation time/state directly.

### Shape material and UV preservation
- Every source vertex UV channel is retained in portable mesh data.
- All primitive texture slots and texture filter/LOD-bias/border metadata are retained.
- UV operation descriptors, light-material values, primitive flags, Z-buffer mode, light-configuration flags, vertex-state flags and secondary matrix index are retained.
- Not complete: reflection-map UV operations, native shader variants, lighting/light-material evaluation, border-color behavior, mip-bias handling, full Z-buffer modes and GPU reference-image parity remain outstanding.

### Route and terrain foundations
- RouteLoader orchestrates route metadata, TDB, global/route TSECTION, track network, signal configuration/scripts/network, terrain discovery/mesh, track-shape discovery/loading/placement, WORLD discovery/loading/scenery, texture loading and validation.
- RouteTrackItemPlacementBuilder maps referenced TDB items to world-space positions/headings along vector geometry, resolves linked item references, and reports invalid/unhosted items. RouteLoader includes these placements in RouteLoadResult and RouteValidationReport.
- TrackDatabaseReader currently recognizes crossover, signal, speedpost, platform, sound-region, empty, level-crossing, siding, hazard and pickup records, and preserves multiple item references/metadata.
- TrackNetworkBuilder and TrackNetworkRouteBuilder build vector paths and connected route traversal; ambiguous junction continuations are surfaced rather than guessed.
- Terrain and WORLD mesh pipelines exist, with seasonal texture selection.
- Not complete: route-wide operational and visual parity, complete track-item consumers, all signalling behavior, route-scale asynchronous tile/asset streaming and unload/reload, and visual acceptance over the authorized route.

### Rolling-stock foundations
- Native ENG/WAG metadata and consist readers, shared shape loader, route placement and motion-state placement exist.
- Simulation foundations include fixed-step consist motion, traction, resistance, adhesion, coupler force/slack and pneumatic brake models.
- A render-frame orchestrator now links motion-state placement to animated textured geometry.
- Not complete: native vehicle/material parity, dynamic wheel/bogie/suspension behavior, train-specific animation-controller binding beyond the shape's time-based clips, full native cab controls, native rolling-stock audio assets, and complete Demo Model 1 browser operation.

## Verification policy and latest evidence

- Implementation checkpoint before this status refresh: `353c8bcf4d9e531f1f361790b708c9662fecf372`.
- The first build attempt on `d79ee7e32c874bfd43e2095e01b05152b6628256` failed because the Razor component lacked the `System.Numerics` import for `Vector3`; the import was added immediately afterward.
- Corrected checkpoint `282cc08721ca901a326355fc2604b4819ad7c0ce`:
  - WebAssembly publish and branch preview: run 46087463225 — success.
  - Duplicate WebAssembly publish run: 46087458752 — success.
  - Route regression suite: run 46087463217 — success.
  - Authorized Demo Model 1 route acceptance: run 46087463217 was still in progress when this record was refreshed.
- Current implementation checkpoint `353c8bcf4d9e531f1f361790b708c9662fecf372`:
  - WebAssembly workflow: run 46087581405 — in progress at last check.
  - Route workflow: run 46087581409 — in progress at last check.
- The route regression workflow now explicitly includes `TerrainLodSelectorTests`.
- These automated results do not establish visual parity at all LOD distances, stable performance on the full route, real locomotive/wagon animation parity, cab/audio acceptance, or async route-scale streaming.
- The current environment can inspect and update GitHub source but cannot execute dotnet or browser end-to-end tests directly.
- Do not claim current tests/builds pass until CI reports success for the current commit.
- Do not claim any system complete until the authorized Demo Model 1 route and real rolling stock run through the browser runtime with visible/operational acceptance evidence.

## Next bulk integration work

1. Validate camera-driven terrain/track/WORLD LOD in a desktop browser at close, medium and far distances; profile rebuild cost and avoid redundant geometry work.
2. Implement route-scale async tile/asset streaming, cancellation, unload/reload and bounded GPU/CPU memory.
3. Complete native shape material behavior: reflection-map UV operations, shader variants, lighting/light-material evaluation, border colors, mip bias, full Z-buffer modes and GPU reference-image parity.
4. Complete rolling-stock-specific runtime behavior: wheel/bogie/suspension controllers, native cab control animation, native sound definitions/assets and representative locomotive-plus-coach acceptance.
5. Continue route, signal/interlocking, AI, simulation and original Open Rails UI closure tracks.
6. Run full Demo Model 1 browser acceptance and record exact browser/runtime evidence; CI parser acceptance alone is insufficient.

## Continuity instruction

At the start of a new project chat, read this file and continue from the latest branch state. Do not restart the audit, repeat completed foundations, or treat this status file as proof of feature completion.

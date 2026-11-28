# PortedOpenRails — coordinated porting status

Last updated: 2026-11-28
Working branch: port/axle-drive-state-hardening
Draft integration PR: https://github.com/geofss/PortedOpenRails/pull/4
Related draft PR: https://github.com/geofss/PortedOpenRails/pull/3
Latest verified implementation checkpoint: a467d8c765b19414d4c24977fa716fa7933c1de3

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
- The live browser simulation clock is passed from BrowserTrainDrivingController into RouteTerrainViewer. Rolling-stock render updates use RollingStockRenderFrameBuilder for connected routes, simulation-time shape animation and camera-distance LOD.
- Rolling-stock placement now retains consist vehicle UID and route chainage. Native `.wag`/`.eng` wheel-radius values are parsed, and WHEELS matrix nodes rotate from travelled route distance. BOGIE/BOGIE1/BOGIE2 pivots yaw to the local route tangent at their longitudinal offsets, including flipped cars. Wheel and bogie poses are included in initial rendering, camera-driven geometry rebuilds and live simulation updates.
- Render batch payloads now carry per-slot texture indexes, every retained UV channel and material descriptors through initial draw and live rolling-stock updates.
- Track-shape and WORLD render batches carry full UV-channel arrays, per-slot texture indexes and primitive material descriptors; material-state differences are split into separate batches.
- The WebGL2 renderer consumes up to four UV channels and four texture slots per batch, supports copy/uniform-scale/nonuniform-scale UV operations, per-stage addressing/filter settings, alpha test and the available Z-buffer write state.
- Rolling-stock render-frame builder has an overload that accepts the live TrainConsistSimulationSession and reads its simulation time/state directly.
- AI service speed control now converts every upcoming SIGCFG speed limit into a distance-aware braking envelope. Distant low-speed restrictions no longer force premature crawling; the controller begins limiting speed according to the configured service deceleration and remaining distance, for both forward and reverse services.
- Signal aspect evaluation now treats circuits reserved by a different train as unavailable before physical occupancy, while retaining the evaluating train's own reservations for native ENABLED / ROUTE_SET script behavior. This closes a permissive-aspect gap between dispatcher route reservations and signal evaluation.
- Terrain patch selection now sorts candidate patches by camera-to-tile distance before applying the geometry vertex budget, with deterministic tile-coordinate tie-breaking. This prevents archive directory order from consuming the preview budget on distant tiles before nearby terrain. This is render-budget prioritization, not yet route-scale asset streaming or CPU/GPU eviction.
- Signal interlocking now supports atomic active-route replacement for an AI train. It checks every new circuit and junction before mutating state, preserves the old route on conflict, releases only superseded reservations, and refuses to drop any circuit still occupied by a leading or trailing vehicle. AI service sessions can request this replan without first releasing their existing route.
- AI timetable dispatch now exposes Pending / Dispatched / Completed / Cancelled lifecycle states, supports cancellation before departure, detects completion after the session releases its route, retries blocked departures, and dispatches equal-time services in deterministic service-ID order.
- AI speed profiles now combine configured signal speed limits with route-chainage track speed restrictions. Both feed one distance-aware braking envelope; invalid/non-finite restrictions are rejected, and reverse-direction route distances are handled.
- Added a cancellable camera-visibility tile cache with bounded entry and resident-byte budgets, load concurrency limits, generation cancellation, unload of tiles leaving the visible set, and async disposal for asset wrappers (including GPU-resource owners). Over-budget visible assets are returned as transient caller-owned assets rather than silently exceeding the cache budget.
- The staged ZIP index context cache is now LRU-bounded to two archive contexts. Archive deletion releases the matching in-memory context and removes its persisted `.index.json`, avoiding stale multi-gigabyte route indexes remaining retained after package removal.
- Terrain streaming cache foundations are implemented with tests for visible-set changes, cancellation, byte/entry caps, transient over-budget assets, and asynchronous disposal. The current RouteLoader still eagerly constructs the full terrain/WORLD result before RouteTerrainViewer consumes it. The new cache is not yet wired into a viewport-driven tile loader, and route-scale browser acceptance is still required; do not count terrain streaming as closed yet.
- The latest controller CI run (46091560300) exposed three regression-test issues in route replacement expectations, timetable cancellation timing, and an empty cache-eviction candidate set. The corresponding fixes are now committed after that run; a fresh run against the current head is required before accepting the fixes.

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
- Remaining vehicle-fidelity work is narrower: driven axle angular state and wheel-slip parity, suspension, coupler/air-hose articulation, freight/control-driven animation, native sound definition/playback, and visual acceptance of representative locomotive-plus-coach consists. Cab bitmap/control binding foundations exist but full native behavior belongs with the remaining simulation/UI closure work.

## Verification policy and latest evidence

- Latest verified implementation checkpoint: `a467d8c765b19414d4c24977fa716fa7933c1de3`.
- WebAssembly build/publish and downloadable branch preview: run 46089953902 — success.
  - Controller regression suite: 711 passed, 0 failed.
  - JavaScript renderer syntax validation: passed.
  - WebAssembly app publish: passed.
  - Branch preview artifact upload: passed.
- Route pipeline: run 46089953875 — success.
  - Route, terrain, scenery, LOD and rolling-stock regression suite: 147 passed, 0 failed.
  - Authorized Demo Model 1 / Scottish Capital Express load-and-validation acceptance: passed.
- The verified checkpoint includes camera-driven terrain/track/WORLD LOD, native bounding-sphere and multi-control LOD selection, live simulation-time rolling-stock rendering, wheel rotation, route-curvature-driven bogie yaw, and a trailing-edge camera callback so the final camera position is not lost inside the throttle interval.
- Test totals at this checkpoint: 711 controller/build regressions and 147 route/terrain/scenery/LOD/rolling-stock regressions, all passing.
- Authorized Demo Model 1 / Scottish Capital Express load-and-validation acceptance passed.
- Preview artifacts: [desktop app preview](https://github.com/geofss/PortedOpenRails/actions/runs/46089953902/artifacts/15240379262) and [Demo Model 1 desktop preview bundle](https://github.com/geofss/PortedOpenRails/actions/runs/46089953902/artifacts/15240408928).
- Automated route acceptance proves that the authorized route loads and validates. It does not establish browser visual parity at every LOD, stable performance on the full route, full wheel-slip/suspension/coupler behavior, native sound, or a complete driving session.
- The current environment can inspect and update GitHub source but cannot execute dotnet or browser end-to-end tests directly.
- Do not claim any subsystem complete until the authorized Demo Model 1 route and real rolling stock run through the browser runtime with visible/operational acceptance evidence.

## Remaining major closure tracks

1. Signal systems and interlocking.
2. Terrain systems, including route-scale async tile/asset streaming and memory eviction.
3. AI systems and dispatcher behavior.
4. Simulation systems, including remaining native vehicle physics, sound and activity runtime.
5. Original Open Rails UI port and full cab/control parity.

## Continuity instruction

At the start of a new project chat, read this file and continue from the latest branch state. Do not restart the audit, repeat completed foundations, or treat this status file as proof of feature completion.

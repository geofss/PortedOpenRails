# PortedOpenRails — coordinated porting status

Last updated: 2026-11-28
Working branch: port/axle-drive-state-hardening
Draft integration PR: https://github.com/geofss/PortedOpenRails/pull/4
Related draft PR: https://github.com/geofss/PortedOpenRails/pull/3
Latest recorded commit: 323db0caaeeebf6cb1f5799b68140a61ba58f6fe

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
- Linear-position interpolation and TCB quaternion interpolation using neighboring quaternion logarithms and squad-style controls.
- Animation frame clock driven by elapsed simulation seconds, with looping and non-looping behavior.
- Named animation nodes mapped to shape matrix indices and sampled poses applied in track-shape and WORLD shape hierarchy transforms.
- Track-shape and WORLD geometry builders accept elapsed simulation time.
- Rolling-stock render-frame builder connects consist motion state, route placement, simulation time, shape animation, season selection and camera-distance LOD.
- Distance-based LOD selector integrated into track-shape and WORLD scenery geometry builders.
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
- Not complete: route-wide operational and visual parity, complete track-item consumers, all signalling behavior, route-scale streaming, and visual acceptance over the authorized route.

### Rolling-stock foundations
- Native ENG/WAG metadata and consist readers, shared shape loader, route placement and motion-state placement exist.
- Simulation foundations include fixed-step consist motion, traction, resistance, adhesion, coupler force/slack and pneumatic brake models.
- A render-frame orchestrator now links motion-state placement to animated textured geometry.
- Not complete: native vehicle/material parity, dynamic wheel/bogie/suspension behavior, full train-specific controls, cab/audio runtime binding, and complete Demo Model 1 browser operation.

## Verification policy and latest evidence

- Previous checkpoint at commit f0cf1eab89b3cb08c61205f3ecac5346633e855a had successful reported workflows:
  - WebAssembly build: run 46083452284
  - Route pipeline regression: run 46083452283
- Those workflow results predate the later TCB, LOD, material-preservation, route-item placement and rolling-stock render-frame changes. A later run on commit b5d9351705ca90a2d9e05f2d71f77d96e1d16448 failed at compile time due to a variable-scope collision in the WORLD animation builder; that collision was fixed in commit 3afa3b2dd06d3143980a4ec7de9711b13c722003. The subsequent code additions have not yet been verified by a successful run.
- The current environment can inspect and update GitHub source but cannot execute dotnet or a browser end-to-end test directly.
- Do not claim current tests/builds pass until CI reports success for the current commit.
- Do not claim any of the three systems complete until the authorized Demo Model 1 route and real rolling stock run through the browser runtime with visible/operational acceptance evidence.

## Next bulk integration work

1. Finish rendering the retained material/UV data: expand material batching and GPU vertex layout without silently dropping additional texture slots or UV channels; implement supported UV operations and native render-state mapping.
2. Bind the application render loop's simulation clock and camera to the rolling-stock/WORLD render-frame APIs; connect clip selection to actual vehicle/scenery states and wheel/cab controllers.
3. Compare TCB interpolation against upstream Open Rails implementation/reference samples; cover irregular frame spacing, quaternion sign flips, endpoint behavior and degenerate keys.
4. Make LOD selection and route streaming operate at tile/asset lifecycle level, not only per-mesh geometry selection; add cancellation and unload/reload.
5. Complete route item-to-runtime bindings for junctions, signals, platforms, sidings, crossings, restrictions, sound regions, pickups and hazards.
6. Complete rolling-stock native model, physics, cab and audio integration and exercise a real locomotive-plus-coach consist on the authorized route.
7. Run all CI checks and the Demo Model 1 browser acceptance flow; update this file with exact commit and run IDs.

## Continuity instruction

At the start of a new project chat, read this file and continue from the latest branch state. Do not restart the audit, repeat completed foundations, or treat this status file as proof of feature completion.

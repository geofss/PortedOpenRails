# PortedOpenRails roadmap

**Project target:** A full Open Rails simulator running in a web browser. This is not a menu mock-up or a standalone train demo.

## Milestone 0 — Browser foundation
- [x] Create a .NET WebAssembly host project
- [x] Build and launch it in a browser
- [x] Record initial compatibility results

## Milestone 1 — Compatibility investigation
- [x] Inspect upstream target frameworks and project references
- [x] Identify major Windows/desktop and graphics dependencies
- [x] Select and port a dependency-light original C# component
- [x] Add regression tests and run them in CI
- [x] Complete the initial full-simulator architecture audit
- [x] Inventory current browser-port parser, format, virtual IO, and simulation files with their roles and dependency boundaries (upstream desktop source-wide dependency enumeration remains pending; see [browser-port dependency inventory](BROWSER_PORT_FILE_DEPENDENCY_INVENTORY.md))

See [Milestone 2A Content Pipeline Audit](MILESTONE_2A_CONTENT_PIPELINE_AUDIT.md) for parser, format, IO, and browser virtual-filesystem findings.

## Milestone 2 — Browser content pipeline
- [x] Define a browser-independent virtual content store interface
- [x] Normalize MSTS/OR logical paths and safely resolve relative includes
- [x] Index read-only ZIP packages with path-traversal and size-limit checks
- [x] Connect user-selected browser ZIP files to the virtual content store
- [x] Combine multiple packages with deterministic overlay precedence
- [x] Add package reorder and removal controls (up/down priority and removal actions; package-store ownership is preserved while rebuilding the overlay)
- [x] Confirm precedence against upstream Open Rails content lookup behavior (confirmed mismatch: upstream uses category-specific paths and local-to-global sound fallback, not universal overlays; details in [MILESTONE_2D_MULTI_PACKAGE_CONTENT_MANAGER.md](MILESTONE_2D_MULTI_PACKAGE_CONTENT_MANAGER.md))
- [x] Integrate upstream STFReader with the virtual content store and nested include resolver (see Milestone 2E)
- [x] Adapt SBR binary readers to browser-provided streams through `SBR.Open(Stream, filename)` and virtual-content terrain loading; regression fixtures verify compressed and uncompressed SIMISA binary blocks from ZIP-backed streams
- [x] Parse real route metadata from user-imported ZIP content using upstream STFReader
- [x] Add route selection UI: built-in preview or import a route ZIP and select parsed .trk metadata
- [x] Parse TDB track-node topology and resolve global/route-specific TSECTION geometry (fixture-validated; real-route validation remains pending)

See [Milestone 3D Traction and Consist Simulation Sessions](MILESTONE_3D_TRACTION_SESSION.md), [Milestone 2E STFReader Integration](MILESTONE_2E_STF_READER_INTEGRATION.md), [Milestone 2F Route Track Network](MILESTONE_2F_ROUTE_TRACK_NETWORK.md), [Milestone 2B Virtual Content Prototype](MILESTONE_2B_VIRTUAL_CONTENT_PROTOTYPE.md), [Milestone 2C Browser ZIP Import](MILESTONE_2C_BROWSER_ZIP_IMPORT.md), and [Milestone 2D Multi-Package Content Manager](MILESTONE_2D_MULTI_PACKAGE_CONTENT_MANAGER.md).

## Milestone 3 — Portable simulation core
- [x] Isolate the initial route-data domain from MonoGame and Windows APIs
- [x] Parse TDB TrackNodes, vector sections, junction metadata, and TrPins using the upstream STFReader
- [x] Build connected vector-path topology and resolve section lengths from global/route TSECTION catalogs
- [x] Parse global TSECTION TrackShapes and resolve original track shape filenames
- [x] Discover route/global SHAPES assets referenced by the track network
- [x] Port the original `.s` shape parser to virtual SBR streams
- [x] Convert original shape geometry to portable vertex/index/material/LOD mesh data
- [x] Sample straight and curved centerlines using the Open Rails heading convention
- [x] Add a browser-safe fixed-step simulation scheduler with bounded catch-up, pause/resume, and reset
- [x] Add a longitudinal train-car motion integrator using signed total force divided by vehicle mass (upstream traction, braking, resistance, adhesion, and coupler-force systems remain separate and incomplete)
- [x] Aggregate supplied motive/gravity forces and rolling, brake, curve, wind, and tunnel resistance with direction-aware stopping behavior (force-generation curves remain pending)
- [x] Connect the fixed-step scheduler, force aggregator, and motion integrator in a testable single-car simulation loop
- [x] Add a portable brake-force evaluator and integrate per-car brake commands into single-car and multi-car simulation sessions (full train-brake systems remain pending)
- [x] Connect parsed locomotive traction curves and limits to the fixed-step single-car simulation session
- [x] Connect parsed locomotive traction to a multi-car consist session with caller-supplied coupler forces
- [x] Calculate algebraic coupler boundary forces for a target common consist acceleration, expand them to equal/opposite per-car forces, integrate each fixed simulation tick, and enforce optional compression/tension ratings; common-acceleration mode also accepts per-car brake commands (coupler slack/break dynamics remain pending)
- [x] Integrate the prototype simulation session with route-aligned multi-car rendering and live browser controls (connected multi-node route profiles, per-car placement, throttle/brake controls, and live telemetry; native Open Rails behavior remains incomplete)
- [x] Discover the terrain tiles touched by the loaded track network
- [x] Read SIMISA terrain sample and patch metadata through browser-provided SBR streams
- [ ] Validate the route pipeline against a real, licensed user-imported route package — permission to use and distribute Demo Model 1 has now been granted by the Open Rails Management Team, subject to Making Tracks Ltd. copyright acknowledgement; the package still needs to be supplied/imported and tested. Follow the [real-route validation checklist](REAL_ROUTE_VALIDATION.md) and [attribution requirements](DEMO_MODEL_1_ATTRIBUTION.md)
- [x] Generate terrain meshes from the original terrain patch/sample data, including cross-tile sample resolution and patch LODs (preview rendering remains limited)
- [x] Load original rolling-stock `.s` assets and supported ACE textures from TRAINSET folders
- [x] Place consist vehicles along the selected connected TDB route using sampled straight/curved centerline geometry, vehicle lengths, tangent headings, reversed-node handling, and consist flip flags; simulated per-car route distances drive live placement
- [x] Apply configurable per-car wheel-rail adhesion ceilings to locomotive tractive effort across direct, brake-command, common-acceleration, and automatic-coupler consist simulation modes (quasi-static limit; wheel-slip dynamics remain pending)
- [ ] Port full braking, power supply, adhesion/wheel-slip, and locomotive-specific control systems (a standalone signed brake-force evaluator is tested; brake pipe propagation, fade, and vehicle-specific systems remain pending; traction-force parsing/evaluation is partial)
- [ ] Port signalling, track circuits, switches, activities, and timetable/AI logic
- [x] Replace desktop threading assumptions with a browser-safe fixed-step scheduler driven by browser frame time, with bounded catch-up and dropped-time accounting (native Open Rails threading behavior remains outside the port)
- [ ] Add desktop-reference regression scenarios

## Milestone 4 — Browser rendering
- [x] Evaluate and integrate a WebGL2 rendering backend against original route terrain assets (preview renderer; not full simulator renderer)
- [x] Load and display terrain patches, ACE textures, route track overlays, and original track-shape meshes in the preview (limited tile/geometry caps; basic shape-texture batches and WORLD scenery meshes now render, but full material/shader variants and seasonal texture handling remain incomplete)
- [x] Build texture-aware track-shape primitive batches with original UV/material references
- [x] Render basic RGBA8 track-shape ACE texture batches with UVs and alpha-test support (full shader/material variants and seasonal textures remain incomplete; border addressing is approximated)
- [x] Expose usable track-shape texture and render-batch counts in preview diagnostics
- [x] Map original texture addressing modes to WebGL wrap/mirror/clamp (border mode is approximated)
- [x] Release transient WebGL buffers and vertex arrays after redraws to avoid GPU resource accumulation during camera interaction
- [x] Dispose renderer programs, textures, and event listeners when the preview component is removed; redraw on window and canvas/container resize
- [x] Add orbit/tilt drag controls and wheel/pinch zoom to the route preview, with touch gestures enabled on the canvas and tilt projection in both renderer paths
- [x] Report unknown WORLD block/field tokens as deduplicated route-preview diagnostics
- [x] Keep valid scenery tiles loaded when another WORLD tile is malformed, with regression coverage
- [x] Validate the WebGL renderer JavaScript syntax in CI with Node.js
- [x] Render WORLD scenery placements with verified upstream matrix/quaternion transforms and basic RGBA8 texture batches (full material features remain incomplete)
- [x] Render first-LOD rolling-stock meshes in the route WebGL scene with supported RGBA8 ACE textures, UVs, alpha-test materials, and route-aligned transforms (animation, full shader variants, and continuous multi-node traversal remain incomplete)
- [ ] Add cameras and cab views
- [x] Add responsive graphics quality and performance controls (Low/Medium/High presets scale WebGL pixel resolution, terrain tile count, track sampling, and geometry budgets)

## Milestone 5 — Player controls, UI, and audio
- [x] Build browser route/activity/consist selection (route, consist, and route-folder `.act` file selection UI; activities are not executable yet)
- [x] Add focused keyboard throttle/brake/reverser shortcuts (Up/Down adjust throttle, Space toggles full brake, F/N/R select direction, Escape releases throttle and brake)
- [x] Add a forward/neutral/reverse traction-direction selector with a stopped-train and zero-throttle interlock
- [x] Complete the current prototype driving command map across pointer/touch, focused keyboard, and standard-mapped gamepad inputs (see [input reference](INPUT_CONTROLS.md))
- [ ] Expand input mapping to the full Open Rails simulator command set, including cab, signalling, train-protection, and activity commands
- [x] Connect throttle, brakes, reverser, doors, and relevant cab controls (prototype controller/UI commands are connected through pointer, keyboard, and supported gamepad inputs; door opening is stopped-train interlocked and blocks traction)
- [x] Add browser audio output driven by simulation events (optional user-enabled synthesized engine tone and brake hiss; not authentic Open Rails recordings)
- [x] Persist browser preview-quality and prototype driving preferences, with reset controls and storage-error fallbacks
- [x] Provide loading indicators and actionable errors for route/package import and connected-route build failures
- [ ] Implement simulator-state save/restore and broader simulation loading/error UX (initial in-memory single-car/consist loop and session motion plus fixed-step clock snapshot APIs are implemented and regression-tested, including atomic rejection of invalid/null restore snapshots without changing live motion or clock state; browser persistence, control state, pneumatic/brake/traction controller state, and broader loading/error recovery remain pending)
- [ ] Port the original Open Rails UI system (menu/navigation, route/activity/train selection, driving HUD and cab interaction, options, dialogs, pause/in-game menus, preserving original UI behaviour while adapting responsively; see [original UI port plan](ORIGINAL_UI_PORT_PLAN.md)

## Milestone 6 — Full simulator integration
- [ ] Run a real supported activity from loaded content
- [ ] Integrate weather, AI traffic, dispatching, and advanced train systems
- [x] Investigate multiplayer compatibility (browser transport and upstream protocol constraints documented in [MULTIPLAYER_COMPATIBILITY_INVESTIGATION.md](MULTIPLAYER_COMPATIBILITY_INVESTIGATION.md); no implementation claimed)
- [ ] Validate desktop and mobile browser performance
- [x] Publish documented supported-content and browser compatibility (requirements, supported-content subset, and unverified browser/device matrix in [WEB_SUPPORT_MATRIX.md](WEB_SUPPORT_MATRIX.md))

## Current browser-port status (verified 2026-11-23)

The browser host and route-import pipeline are working as an experimental vertical slice. The selected route scene renders terrain patches, supported ACE textures, vector-track overlays, original track-shape geometry, WORLD scenery meshes, and first-LOD rolling-stock meshes. The consist pipeline resolves original vehicle shape files and supported ACE textures from each owning TRAINSET folder. When a route and consist are loaded together, vehicles are positioned along the selected connected TDB route, including reversed vector nodes and explicit branch selection, using sampled straight/curved centerline geometry, per-vehicle length metadata, local tangent headings, and consist flip flags. The route-distance profile supplies per-car elevation grade and heading to the driving simulation. Rolling stock shares the route's WebGL scene and renderer; diagnostics report placement and texture issues. Browser driving now has a prototype fixed-step multi-car simulation with parsed locomotive traction, per-car resistance, route-grade effects, pneumatic brake-pipe/cylinder response, and dynamic spring/damper coupler forces recomputed from current consist state on every executed simulation tick. Pneumatic subsystems advance only for accepted fixed simulation time, and regression tests cover dropped frame time, per-tick coupler sampling, initial boundary telemetry, and pressure-state validation/immutability. This is not yet full Open Rails physics: native ORTS coupler parameter parsing, wheel/animation controllers, full brake/traction/adhesion systems, signalling, activities, and authentic Open Rails cab/UI remain incomplete. The browser driving prototype now exposes forward/neutral/reverse traction direction, keyboard direction shortcuts, and a safety interlock that prevents changing direction while the consist is moving or throttle remains applied, and reports rejected keyboard commands in the controls. Left/right passenger-door commands are available by pointer, keyboard, and gamepad shoulder buttons; opening is interlocked until the consist is stopped with throttle at zero, open doors block traction, and reset closes both sides. Emergency stop is now latched against subsequent throttle/brake inputs until an explicit release action; the UI exposes that release separately and reset clears the latch. The latched stop also drives a faster brake-pipe emergency vent below the normal service-pressure floor, with front-to-rear propagation into the existing per-car cylinder response. The route preview also exposes overview, trackside, and top-down camera presets alongside manual orbit/tilt/zoom; these are scene-preview viewpoints, not cab views. Terrain preview remains limited to eight tiles and first LOD; advanced material/shader modes and seasonal variants remain incomplete. Consist parsing includes car identity/type, flips, optional loads, durability, speed limits, serial/UID metadata, TCS references, package provenance, and validation diagnostics. The browser package manager supports layered precedence, reorder/removal, and ownership-safe composite views. CI run #896 passed all 245 regression tests, WebAssembly publish, and GitHub Pages deployment. This is **not yet a release-ready browser simulator**.

## Full-port acceptance criteria

A full port means real route and train content can be loaded, a real activity/timetable can run using Open Rails simulation behavior, and the browser renders and controls that simulation. A successful build, a static menu, or a scripted train animation alone does not satisfy this goal.

## Engineering principles

- Preserve original C# and reuse upstream behavior wherever technically possible.
- Keep the simulation core independent from Blazor UI and browser-specific APIs.
- Put browser file, storage, input, audio, timing, and networking behind adapters.
- Do not assume WindowsDX/MonoGame desktop rendering can compile unchanged to WebAssembly.
- Evaluate WebGL2 as the broad-compatibility baseline; treat WebGPU as optional.
- Preserve Open Rails copyright and GPL-3.0-or-later notices in copied or modified upstream files.
- Keep third-party dependency and asset licensing visible.
- Test each extracted subsystem independently and compare behavior with upstream Open Rails.
- Keep mobile browser performance in scope from the beginning.

## Architecture record

See [Full Simulator Architecture Audit](MILESTONE_2_ARCHITECTURE_AUDIT.md) for the inspected source-tree scope, dependency blockers, subsystem classification, and implementation order.

### Latest implementation update (2026-11-23)

Route-aligned rolling-stock rendering is integrated into the selected route's WebGL scene. Vehicle placements follow the selected connected TDB route, including reversed vector nodes, using sampled straight/curved geometry, vehicle lengths, tangent headings, and flip flags. The route profile supplies continuous distance-indexed position, heading, and grade across connected nodes; regression coverage now checks reversed traversal, grade sign, connected-node stitching, and endpoint clamping. The simulation core combines a bounded fixed-step scheduler, direction-aware force aggregation, parsed locomotive traction curves/limits, and single-/multi-car traction sessions with pause/resume/reset. The browser-driving controller now advances brake-pipe and brake-cylinder state only inside executed fixed simulation ticks; a regression test verifies pneumatic response does not run ahead when frame time is dropped. Dynamic coupler spring/damper forces and signed per-boundary telemetry are recalculated from current consist motion on each fixed tick, with initial telemetry entries created for every adjacent-car boundary. Pneumatic pressure snapshots are immutable and invalid pressure data is rejected before conversion to brake commands. The standalone algebraic coupler-force distributor remains available for common-acceleration mode, including optional compression/tension ratings. Native ORTS coupler parameter parsing, detailed brake-valve/reservoir behavior, wheel-slip dynamics/sanding, coupler slack/breakage fidelity, and full live cab/control integration remain incomplete. Focused keyboard throttle/brake shortcuts now complement the pointer-based sliders. GitHub Actions run #896 passed 245 regression tests, WebAssembly publish, and GitHub Pages deployment.

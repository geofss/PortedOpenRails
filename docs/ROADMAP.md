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
- [ ] Complete file-level dependency inventory for parser, format, IO, and simulation core

See [Milestone 2A Content Pipeline Audit](MILESTONE_2A_CONTENT_PIPELINE_AUDIT.md) for parser, format, IO, and browser virtual-filesystem findings.

## Milestone 2 — Browser content pipeline
- [x] Define a browser-independent virtual content store interface
- [x] Normalize MSTS/OR logical paths and safely resolve relative includes
- [x] Index read-only ZIP packages with path-traversal and size-limit checks
- [x] Connect user-selected browser ZIP files to the virtual content store
- [x] Combine multiple packages with deterministic overlay precedence
- [x] Add package reorder and removal controls (up/down priority and removal actions; package-store ownership is preserved while rebuilding the overlay)
- [ ] Confirm precedence against upstream Open Rails content lookup behavior
- [x] Integrate upstream STFReader with the virtual content store and nested include resolver (see Milestone 2E)
- [ ] Adapt SBR binary readers to browser-provided streams
- [x] Parse real route metadata from user-imported ZIP content using upstream STFReader
- [x] Add route selection UI: built-in preview or import a route ZIP and select parsed .trk metadata
- [x] Parse TDB track-node topology and resolve global/route-specific TSECTION geometry (fixture-validated; real-route validation remains pending)

See [Milestone 2E STFReader Integration](MILESTONE_2E_STF_READER_INTEGRATION.md), [Milestone 2F Route Track Network](MILESTONE_2F_ROUTE_TRACK_NETWORK.md), [Milestone 2B Virtual Content Prototype](MILESTONE_2B_VIRTUAL_CONTENT_PROTOTYPE.md), [Milestone 2C Browser ZIP Import](MILESTONE_2C_BROWSER_ZIP_IMPORT.md), and [Milestone 2D Multi-Package Content Manager](MILESTONE_2D_MULTI_PACKAGE_CONTENT_MANAGER.md).

## Milestone 3 — Portable simulation core
- [x] Isolate the initial route-data domain from MonoGame and Windows APIs
- [x] Parse TDB TrackNodes, vector sections, junction metadata, and TrPins using the upstream STFReader
- [x] Build connected vector-path topology and resolve section lengths from global/route TSECTION catalogs
- [x] Parse global TSECTION TrackShapes and resolve original track shape filenames
- [x] Discover route/global SHAPES assets referenced by the track network
- [x] Port the original `.s` shape parser to virtual SBR streams
- [x] Convert original shape geometry to portable vertex/index/material/LOD mesh data
- [x] Sample straight and curved centerlines using the Open Rails heading convention
- [x] Discover the terrain tiles touched by the loaded track network
- [x] Read SIMISA terrain sample and patch metadata through browser-provided SBR streams
- [ ] Validate the route pipeline against a real, licensed user-imported route package — use the [real-route validation checklist](REAL_ROUTE_VALIDATION.md)
- [x] Generate terrain meshes from the original terrain patch/sample data, including cross-tile sample resolution and patch LODs (preview rendering remains limited)
- [x] Load original rolling-stock `.s` assets and render a first-LOD consist preview with supported ACE textures (straight preview lineup; route placement, animation, and full material variants remain pending)
- [ ] Integrate rolling-stock rendering into the route-aligned simulation scene
- [ ] Port braking, traction, power supply, and control systems
- [ ] Port signalling, track circuits, switches, activities, and timetable/AI logic
- [ ] Replace desktop threading assumptions with a browser-safe simulation scheduler
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
- [x] Render rolling-stock shape meshes in a consist preview with supported RGBA8 ACE textures and alpha-test materials (first LOD and straight lineup; route placement, animation, and full shader variants remain incomplete)
- [ ] Add cameras and cab views
- [ ] Add responsive graphics quality and performance controls

## Milestone 5 — Player controls, UI, and audio
- [ ] Build browser route/activity/consist selection
- [ ] Map keyboard, pointer, touch, and gamepad input to simulator commands
- [ ] Connect throttle, brakes, reverser, doors, and relevant cab controls
- [ ] Add browser audio output driven by simulation events
- [ ] Add settings, save/restore, and loading/error UX

## Milestone 6 — Full simulator integration
- [ ] Run a real supported activity from loaded content
- [ ] Integrate weather, AI traffic, dispatching, and advanced train systems
- [ ] Investigate multiplayer compatibility
- [ ] Validate desktop and mobile browser performance
- [ ] Publish documented supported-content and browser compatibility

## Current browser-port status (verified 2026-11-22)

The browser host and route-import pipeline are working as an experimental vertical slice. The preview renders terrain patches, supported ACE textures, vector-track overlays, original track-shape geometry, WORLD scenery shape meshes using upstream matrix/quaternion conversions, and a first-LOD rolling-stock consist preview. Rolling-stock preview resolves vehicle `.s` files and ACE textures from the owning TRAINSET folder, then renders supported RGBA8 texture batches with UVs and alpha-test support. The consist is currently arranged in a straight preview lineup; route-aligned placement, animation, full material/shader modes, seasonal variants, and train simulation remain incomplete. Terrain preview remains limited to eight tiles and first LOD. Route parsing and loading diagnostics are shown in the preview. Latest confirmed deployed run #545 passed JavaScript syntax validation, all 111 regression tests, WebAssembly publish, and GitHub Pages deployment. Consist parsing reads car type/name/folder/UID, flip orientation, optional load records with typed upstream position/state enums, durability, max-velocity values, serial, next-wagon UID, and the train-control-parameter filename. The TCS filename is resolved under the upstream `TRAINS/CONSISTS/SCRIPT` directory with package provenance; TCS traversal safety is now under CI. Catalog diagnostics cover malformed files, duplicate UIDs, and misplaced EOT devices. Rolling-stock references resolve upstream Trainset .eng/.wag paths and ORTS_EOT .eot paths, report missing assets, and identify the package supplying each resolved file. Overlay priority, case-insensitive path resolution, and virtual-path traversal rejection are regression-tested. Run #477 is validating the consist metadata display update. Earlier failures were caused by fixture syntax and are superseded by repaired commits. WORLD transform tests cover original matrix and quaternion rotations, tile-local texture mapping avoids repeated UID collisions across WORLD tiles, and geometry caps are regression-tested. This is **not yet a release-ready browser simulator**.

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

### Latest implementation update (2026-11-22)

The browser consist preview now loads original rolling-stock `.s` geometry and resolves the referenced ACE textures relative to each owning TRAINSET folder. Supported RGBA8 images are decoded through the shared ACE reader and submitted to the WebGL shape-material pipeline with UVs, alpha testing, and texture addressing. Regression tests cover local and `TEXTURES`-folder lookup plus unsafe/missing references. GitHub Actions run #545 passed JavaScript syntax validation, all 111 regression tests, WebAssembly publish, and GitHub Pages deployment. Package priority also has explicit move-up, move-down, and remove controls; rebuilding the composite namespace preserves stores still owned by the package manager.

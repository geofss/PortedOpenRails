# Remaining upstream port gaps — 2026-11-27

This is a source-and-feature inventory for the browser port at branch `port/axle-drive-state-hardening`. It separates implemented prototype slices from actual upstream parity. A parser, renderer, or test with a similar name is not counted as a full port unless the corresponding Open Rails behavior is consumed and verified.

## 1. Original UI / MainForm

See [ORIGINAL_UI_PORT_AUDIT.md](ORIGINAL_UI_PORT_AUDIT.md) for the control-by-control comparison with upstream `Source/Menu/MainForm.Designer.cs` and `MainForm.cs`.

Ported: MainForm two-column shell, Activity/Timetable mode switch, Explore path selectors, locomotive/consist selectors, native timetable-set grouping and train-column selection, ten Options tab surfaces, Tools/Documents dropdowns, Logging preference, and an explicit disabled Multiplayer panel. Browser preference persistence and Start eligibility checks are wired.

Remaining: multiple isolated installation profiles; exact upstream activity selection dependencies and environment overrides; Explore path-to-TDB traversal binding; timetable day/season/weather/weather-file selection and runnable train resolution; true Start/Resume/Replay save lifecycle; actual native Options settings model and application to renderer/simulation/audio/keyboard; TestingForm; live notification feed; localization; full keyboard/accessibility parity; and screenshot/interaction regression tests. See [ORIGINAL_UI_PORT_AUDIT.md](ORIGINAL_UI_PORT_AUDIT.md).

## 2. Route and content pipeline

**CI-validated against authorized Demo Model 1:** SCE route metadata, TDB connectivity, positive route length, terrain tile discovery and mesh generation, track-shape discovery and mesh loading, WORLD tile discovery and scenery loading, and signal-circuit topology assertions.

Remaining:
- Complete browser ZIP-picker import and large-archive end-to-end validation on target desktop browsers.
- Remove/replace the staged ZIP compatibility materialization bottleneck (2 GiB aggregate / 512 MiB per entry) so large uncompressed route packages can be read without expanding the whole route into WASM memory.
- Finish category-aware content resolution to match upstream MSTSPath semantics. Current first-package-wins overlay is explicitly not equivalent to upstream route/trainset/sound lookup.
- Parse TDB `TrItemTable`: signals, platforms, sidings, speed posts, sound regions, and their native position/direction metadata.
- Add road database (`.rdb`) support and route object families not yet covered.
- Validate every relevant SCE terrain/WORLD tile, missing asset diagnostics, texture variants, route boundaries, and malformed-neighbor recovery.
- Complete terrain/track/scenery LOD, material/shader, seasonal texture, forest, and visual placement parity.
- Run manual desktop browser preview, real consist placement, and end-to-end route driving. CI RouteLoader acceptance is not a playable-session test.

## 3. Signals, track circuits, switches, interlocking

Implemented slices include signal configuration/script parsing, a bounded interpreter subset, vector-node occupancy, consist-wide occupancy, route reservations, junction locking, and AI dispatch integration.

Remaining:
- Native signal-position track circuits and circuit segmentation; vector-node approximations are not equivalent.
- Parse and resolve native signal TrackItems from TDB.
- Implement remaining signal-script externals and semantics. `THIS_SIG_LR/MR`, `NEXT_SIG_LR/MR`, `NEXT_NSIG_LR/MR`, `OPP_SIG_LR/MR`, signal-ID queries, route-set checks, and distance-based multi-signal aspect queries have browser implementations; timing triggers, call-on, train-path queries, and C# signal scripts remain open and require upstream-reference tests.
- Complete native signal aspect/speed/draw-state semantics and route-wide evaluation.
- Complete switch state/throw behavior, route locking, release rules, overlap/approach locking, and interlocking edge cases.
- Validate signal placement and behavior across the actual SCE route, not just parser/topology fixtures.

## 4. Train simulation and controls

Implemented prototype slices include fixed-step browser scheduling, consist motion, traction metadata, resistance, prototype pneumatic brake response, coupler forces, adhesion ceilings, route-aligned vehicle motion, and browser throttle/brake/reverser controls.

Remaining:
- Port native locomotive-specific power and control systems, independent/dynamic brakes, train brake valves, brake pipe propagation, reservoirs, distributors, brake fade, and vehicle equipment.
- Port adhesion and wheel-slip dynamics, sanding, wheels/axles, motors, transmission, power supply, steam/diesel/electric-specific behavior, and locomotive control state machines.
- Replace prototype defaults for coupler slack/spring/damping with native parsed parameters and validated behavior.
- Complete train control/protection systems and full Open Rails input command map.
- Add desktop-reference regression scenarios for force, braking, stopping distance, grades, coupler behavior, and control state.

## 5. Rolling stock, cab, audio

Implemented slices include ENG/WAG/consist metadata, original shape mesh loading, supported ACE texture decoding, vehicle placement, original CVF cab artwork, and a subset of cab control bindings.

Remaining: complete shape hierarchy/LOD/material/animation parity; all texture variants; multi-controller ControlID mapping; animated cab gauges and indicators; native engine, traction, brake, horn, bell, wheel, track, and environment sounds; audio mixing and state transitions.

## 6. Activity, timetable, AI traffic

Implemented slices include activity metadata/events, selected native event execution, passenger stop timing/session pieces, service/path parsing, timetable row parsing, and initial AI dispatch/interlocking integration.

Remaining: full activity event/action catalog and side effects; activity train assembly/pickup/drop-off; complete station and passenger operations; native timetable selection/runtime; multi-service scheduling; AI train acceleration/braking/path progression, waits, station stops, traffic conflicts, following, deadlock recovery and dispatch priorities; player/AI interaction and completion semantics.

## 7. Rendering and browser platform

Implemented: WebGL2 route preview, terrain patch meshes, track overlays, supported track/WORLD/rolling-stock meshes, camera interaction, quality presets, browser package storage, keyboard/pointer/gamepad controls, and optional synthesized prototype audio.

Remaining: full Open Rails 3D viewer/render pipeline, complete material/shader/weather/lighting/sky/fog/season behavior, cab camera/viewpoint behavior, route-wide streaming and culling, GPU memory/performance profiling on desktop browsers, robust recovery from device/context loss, and full native sound parity.

## 8. Multiplayer

Not implemented. Upstream uses raw TCP client/server transport and thread-based receive loops, which browser pages cannot directly use. A WebSocket relay/translator and validated protocol subset would be a separate project after single-player parity; do not expose nonfunctional Multiplayer controls.

## 9. Upstream-source integration (project-wide blocker)

The upstream port audit explicitly states that migration is not complete. The current repository has a small set of upstream-derived parser files, while many route, terrain, shape, simulation, and rendering components are browser-specific adapters/prototype implementations. Continue the pinned-source audit and migrate portable upstream implementations with original notices, browser-safe IO abstractions, and parity tests. Do not equate similarly named browser classes with a port of the upstream subsystem.

## 10. Deployment and acceptance

- Latest completed CI checkpoint: test-and-build passed on commit `d2d8d2c`.
- The current UI stylesheet commit `7fd01d6` has a build/test run in progress; verify its final result before distributing that artifact.
- The working branch remains a draft PR; do not merge to `main` without review.
- GitHub Pages deployment is not established by a successful PR preview artifact. Production deployment remains a separate acceptance gate.
- Required final acceptance: browser ZIP import, full real-route diagnostics, visual route review, real consist placement, playable single-player activity/timetable, original UI workflow, and sustained desktop browser performance.

## Primary evidence

- [Upstream port audit](UPSTREAM_PORT_AUDIT.md)
- [Upstream source provenance](UPSTREAM_SOURCE_PROVENANCE.md)
- [Browser support matrix](WEB_SUPPORT_MATRIX.md)
- [Real-route validation checklist](REAL_ROUTE_VALIDATION.md)
- [Original UI parity audit](ORIGINAL_UI_PORT_AUDIT.md)
- [Multiplayer compatibility investigation](MULTIPLAYER_COMPATIBILITY_INVESTIGATION.md)

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
- [ ] Preserve package references and override/load order
- [ ] Adapt STFReader's stream/factory seam to the virtual content store
- [ ] Adapt SBR binary readers to browser-provided streams
- [ ] Parse real route and track metadata from a supported content sample

See [Milestone 2B Virtual Content Prototype](MILESTONE_2B_VIRTUAL_CONTENT_PROTOTYPE.md) and [Milestone 2C Browser ZIP Import](MILESTONE_2C_BROWSER_ZIP_IMPORT.md).

## Milestone 3 — Portable simulation core
- [ ] Isolate portable simulation/domain code from MonoGame and Windows APIs
- [ ] Port train and train-car models
- [ ] Port braking, traction, power supply, and control systems
- [ ] Port signalling, track circuits, switches, activities, and timetable/AI logic
- [ ] Replace desktop threading assumptions with a browser-safe simulation scheduler
- [ ] Add desktop-reference regression scenarios

## Milestone 4 — Browser rendering
- [ ] Evaluate a WebGL2 rendering backend against real Open Rails assets
- [ ] Load and display route geometry, terrain, and track
- [ ] Render scenery and rolling stock
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

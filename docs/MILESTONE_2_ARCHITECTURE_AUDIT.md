# Full simulator architecture audit

**Status:** Initial source-tree and project-reference audit  
**Upstream:** https://github.com/openrails/openrails  
**Upstream branch inspected:** `master`  
**Upstream tree commit inspected:** `4b5b34e9a53bf03350c857ccfbe69f390ccdbcf2`  
**Upstream license:** GPL-3.0-or-later

This audit defines the scope of the browser port. The target is the real Open Rails simulator running in a browser, including its simulation, content loading, rendering, controls, and audio—not a visual mock-up or a standalone train toy.

## 1. What the source tree contains

The inspected upstream `Source/` tree contains **1,619 files and 36 C# project files**. The main runtime and library projects include:

- `Orts.Common`
- `Orts.IO`
- `Orts.Content`
- `Orts.Settings`
- `Orts.Parsers.Msts` and `Orts.Parsers.OR`
- `Orts.Formats.Msts` and `Orts.Formats.OR`
- `Orts.Simulation`
- `Orts.Menu`, `Menu`, and `RunActivity`

The remaining projects include launchers, updater utilities, content tools, editors, multiplayer server components, and tests. They are not all required to run the browser player. The first target is the player runtime and the dependencies it actually needs; desktop-only authoring and maintenance tools are separate scope.

The upstream architecture document describes the player as coordinated around formats, game, input, multiplayer, parsers, simulation, sound, UI, and viewer responsibilities.

## 2. Subsystem classification

The labels below are an initial engineering classification, not a claim that every file in a project has already been ported or proven compatible.

| Subsystem | Upstream areas | Browser-port treatment |
|---|---|---|
| Simulation and train physics | `Orts.Simulation` | **Preserve and port incrementally.** Train, TrainCar, brake systems, power supplies, AI/timetable trains, signalling, activities, weather, and control logic are core functionality. Audit platform calls and rendering references at file level before importing. |
| MSTS and OR content formats | `Orts.Formats.Msts`, `Orts.Formats.OR` | **High-priority reuse.** Keep real route, rolling-stock, activity, and asset format semantics. Replace graphics types or file access only where dependencies require it. |
| Parsers | `Orts.Parsers.Msts`, `Orts.Parsers.OR` | **High-priority reuse after dependency isolation.** Parser logic is a promising early porting target, but both project files currently reference MonoGame WindowsDX, so they are not automatically browser-safe. |
| Common utilities | `Orts.Common` | **Selective reuse.** Pure maths, data models, and simulation helpers can often be retained. Separate hardware, Windows, and graphics integrations. |
| Content and virtual filesystem | `Orts.Content`, `Orts.IO` | **Browser adapter required.** Implement package/route access over user-selected files, browser storage, and supported archive readers. Preserve virtual paths and package load/override order. |
| Settings and configuration | `Orts.Settings` | **Browser adapter required.** Use browser storage and explicit import/export instead of assuming desktop profile folders and registry access. |
| Input and controls | `Orts.Common/Input`, player UI | **Browser adapter required.** Map keyboard, pointer, touch, and gamepad input to the existing command model. RailDriver hardware support is not a browser baseline. |
| Rendering and viewer | `RunActivity` and graphics-related runtime code | **Major replacement/integration.** The WindowsDX/MonoGame rendering path cannot be assumed to run in WebAssembly. Build a browser renderer and progressively connect real scene, cab, camera, terrain, track, scenery, and rolling-stock data. |
| Sound | Sound responsibilities in the player architecture and simulation | **Browser audio backend required.** Preserve sound events and simulation-driven parameters; route output through browser audio APIs with user-gesture startup and lifecycle handling. |
| Menu and player UI | `Menu`, `Orts.Menu`, `RunActivity` | **Rebuild UI for browser.** Reuse domain models and selection logic where practical; replace Windows Forms screens and desktop window lifecycle with responsive web UI. |
| Multiplayer | `Orts.Simulation/MultiPlayer`, multiplayer server project | **Later-stage port.** First establish deterministic local simulation and browser networking boundaries; then assess protocol compatibility and server requirements. |
| Launcher, updater, desktop tools | `Launcher`, `Updater`, editors and utilities | **Not part of the first browser runtime.** Replace relevant user-facing flows with web UI; keep desktop-only tools out of the browser dependency graph unless a concrete need emerges. |

## 3. Compatibility blockers confirmed from project files

The inspected upstream library projects currently target `net6-windows`. The current browser host targets `net10.0` with Blazor WebAssembly. This is a real target-framework and dependency boundary, not a simple project-reference change.

Specific findings:

- `Orts.Simulation.csproj` references Common, Content, both format libraries, both parser libraries, and Settings. It also references `MonoGame.Framework.WindowsDX`, Roslyn packages, Newtonsoft.Json, and GNU.Gettext.
- `Orts.Common.csproj` references `RailDriver`, `MonoGame.Framework.WindowsDX`, and `System.Management`, plus GNU.Gettext.
- Both format projects reference `MonoGame.Framework.WindowsDX`; the parser projects also reference it.
- `Orts.IO` is a small separate project and is a natural place to investigate portable file abstractions, but its full source still needs API-level review.
- The upstream architecture describes Render, Updater, Loader, Sound, and Watchdog processes/threads. Browser WebAssembly has a different threading, lifecycle, and scheduling environment. We must preserve simulation timing and safe state handoff without blindly copying the desktop thread model.
- The upstream virtual-filesystem design explicitly supports multiple packages, ZIP-backed packages, MSTS and OR layouts, references across packages, and user-controlled load order. A browser implementation must preserve these semantics rather than merely opening one loose file.

**Conclusion:** Referencing the existing desktop projects directly is not a viable first strategy. The port needs a compatibility layer and selective source/project extraction. Do not remove upstream dependencies globally until their actual use has been mapped.

## 4. Target browser architecture

Keep the existing Blazor WebAssembly host as the application shell, but organize the port around explicit boundaries:

1. **Core simulation:** portable C# domain and simulation logic, isolated from browser APIs and graphics.
2. **Content pipeline:** virtual filesystem interfaces, archive/package indexing, parsers, format readers, and asset conversion.
3. **Platform adapters:** browser file import, persistent storage, clock/scheduling, input, audio, and networking.
4. **Rendering backend:** browser graphics implementation, initially evaluated against WebGL2 for broad device support; WebGPU may be an optional future backend, not a requirement.
5. **Web UI:** route/activity/consist selection, loading progress, simulation controls, overlays, settings, and error reporting.
6. **Validation:** automated unit tests for extracted logic plus comparison fixtures against the desktop Open Rails behavior.

Avoid letting Blazor components become the simulation engine. The simulation should be independently testable and should communicate with the UI/renderer through stable state snapshots or command interfaces.

## 5. Recommended implementation order

### Phase A — Make source reusable
- Create a source inventory of the simulation dependency graph.
- Identify MonoGame types used by parser, format, common, and simulation code.
- Separate pure data/math/parser code from platform-specific code.
- Keep upstream notices and record source provenance for every imported file.

### Phase B — Real content loading
- Design browser-facing virtual filesystem interfaces.
- Support a user-selected route/content package and ZIP-backed content.
- Implement MSTS/OR package indexing, virtual paths, and load-order overrides.
- Port a minimal real route/track metadata parsing path and test it with lawful sample fixtures.

### Phase C — Simulation core
- Port the dependency-light simulation types in dependency order.
- Bring in train/car models, braking, traction/power, signalling, switches, activities, and timetable/AI logic.
- Replace desktop scheduling assumptions with a controlled browser simulation loop.
- Compare deterministic scenarios and numeric outputs against upstream.

### Phase D — Browser renderer and controls
- Prototype WebGL2 rendering and asset upload using real parsed route data.
- Add camera, track/terrain/scenery, rolling stock, and cab rendering in stages.
- Add keyboard, touch, pointer, and gamepad command adapters.
- Add browser audio after the simulation event model is connected.

### Phase E — Complete player experience
- Add route/activity/consist selection and loading UX.
- Integrate weather, AI traffic, dispatching/signalling, cab systems, and remaining player features.
- Investigate multiplayer and performance scaling.
- Validate on desktop and mobile browsers, including constrained devices.

## 6. Definition of “full browser port”

The project should not be called a full port merely because it builds or displays a 3D train. Full-port acceptance requires all of the following to work together:

- Load real supported Open Rails/MSTS route and train content through the browser content pipeline.
- Start and run a real activity or supported timetable.
- Execute the original simulation behavior for train movement, braking, traction, signalling, switches, and relevant AI traffic.
- Render the route, track, scenery, rolling stock, and cab views in the browser.
- Accept player controls and show meaningful simulator state.
- Provide simulation-driven sound where browser capabilities permit.
- Preserve save/settings/content behavior to the extent supported by the browser platform.
- Pass regression tests and documented compatibility checks on supported browsers.

A feature may be marked unsupported only when the limitation is documented and the scope is explicit. A menu-only or scripted demonstration is not full-port completion.

## 7. Next concrete engineering task

Before importing a large subsystem, perform a file-level dependency audit of `Orts.Parsers.Msts`, `Orts.Formats.Msts`, `Orts.IO`, and the simulation's core model folders. Record exact Windows/MonoGame APIs and choose the smallest real route-data loading vertical slice. This creates the first end-to-end path from browser-selected content to parsed simulator data without prematurely attempting the renderer.

## 8. Licensing and provenance

Open Rails is GPL-3.0-or-later. Preserve original copyright and license notices in copied or modified upstream source files, keep the applicable license available in this repository, and clearly distinguish upstream-derived code from new browser adapter code. Review third-party dependency licenses before redistributing browser bundles.

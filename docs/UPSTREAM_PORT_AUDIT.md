# Open Rails upstream port audit

**Audit date:** 2026-11-25  
**Upstream repository:** https://github.com/openrails/openrails  
**Audited upstream revision:** `dfb5246ca9da46e352cfcd2a365d284ac55c1a1f`  
**Browser host:** .NET 10 Blazor WebAssembly (`src/OpenRails.Web`)

## Direction

Port the real Open Rails codebase in dependency-sized layers. Keep the existing browser prototype as a temporary compatibility harness and visual test bed; do not treat its independently implemented parsers or simulation models as the long-term source of truth when equivalent upstream implementations can be adapted.

This is an audit and migration plan, not a claim that upstream code has already been integrated.

## Initial project compatibility findings

The upstream projects inspected at the audited revision are Windows-targeted:

| Upstream project | Current target | Direct dependencies observed | Browser-port assessment |
| --- | --- | --- | --- |
| `Orts.Parsers.Msts` | `net6-windows` | MonoGame `WindowsDX` package | Strong early candidate, after identifying and removing or abstracting any actual platform-specific type usage |
| `Orts.Common` | `net6-windows` | MonoGame `WindowsDX`, RailDriver, System.Management, GNU.Gettext assembly | Shared code is valuable, but hardware/OS integrations must be isolated |
| `Orts.Formats.Msts` | `net6-windows` | `Orts.Parsers.Msts`, `Orts.Common`, `Orts.IO`, MonoGame `WindowsDX` | High-value route/content layer; port after its dependency closure is browser-safe |
| `Orts.Simulation` | `net6-windows` | Common, Content, MSTS/OR formats and parsers, Settings, MonoGame WindowsDX, Roslyn, Newtonsoft.Json | Long-term core target; substantial platform and rendering separation likely required |
| `Orts.Menu` | `net6-windows` | MSTS formats, Common, Content, OR formats, Settings | Useful source for original menu behaviour, but not a first compile target |

The existing browser app targets `net10.0`. A Windows-targeted project cannot simply be referenced by the WebAssembly app. Retargeting alone is also insufficient: package references and source-level calls to Windows APIs, hardware devices, file-system assumptions, graphics APIs and process APIs must be audited.

## Migration sequence

### 1. Establish a clean upstream source baseline

- Pin the upstream revision recorded above.
- Bring upstream source into a clearly separated `vendor/openrails` or equivalent directory, retaining upstream copyright and GPL notices.
- Record the exact upstream revision and any local patches.
- Do not copy only isolated snippets without their required notices or dependency context.

### 2. Port the content-reading dependency chain

- Audit `Orts.Parsers.Msts`, `Orts.IO`, and the route-relevant subset of `Orts.Common`.
- Inventory every MonoGame and platform API usage before changing project targets.
- Separate pure parsing/data structures from rendering, hardware and OS services.
- Retarget only the proven portable subset to a browser-compatible target framework.
- Add upstream-derived fixture tests for route metadata, track database, terrain, world, shape and rolling-stock files.
- Compare parsed output against the existing browser implementation; keep the prototype only as a temporary fallback until parity is demonstrated.

### 3. Port route and asset formats

- Reuse upstream MSTS format readers and content models wherever possible.
- Adapt asset access behind browser package/virtual-file-system interfaces rather than assuming unrestricted desktop paths.
- Preserve original route, terrain, track-shape, texture and rolling-stock semantics.
- Keep Demo Model 1 as the end-to-end route validation package, with its separate permission and attribution requirements.

### 4. Port simulation independently of rendering

- Identify the simulation's dependency on MonoGame and Windows services.
- Extract simulation state, physics, train control and timetable/activity logic from desktop presentation and device services.
- Use browser timing/input/audio adapters at the boundary; do not replace upstream physics with prototype equations merely to make a demo move.
- Validate behaviour against upstream tests and known desktop reference behaviour before claiming simulation parity.

### 5. Rebuild the presentation boundary around the original UI

- Audit the original menu and viewer source for layout, wording, state transitions and workflows.
- Preserve the original Open Rails UI behaviour and visual structure.
- Implement browser rendering through WebGL/WebGPU-compatible adapters where the original desktop graphics backend cannot run.
- Keep rendering changes separate from route parsing and simulation state.

## Acceptance gates

A layer is considered ported only when:

1. Its upstream source revision and license notices are recorded.
2. Browser-compatible build succeeds without Windows-only target dependencies.
3. Relevant upstream-derived tests pass in the browser-port solution.
4. The browser implementation consumes the upstream layer rather than a parallel reimplementation.
5. Existing prototype code is removed or explicitly marked as temporary compatibility code when parity is established.

The route milestone remains separate: Demo Model 1 must load from its actual supplied package, render its real terrain/track/scenery, place the real rolling stock and support a train moving along connected track in the browser. A successful parser test or WebGL preview alone does not meet that gate.

## Important constraints

- Upstream Open Rails is GPL-3.0-or-later. Preserve notices and satisfy source-distribution obligations for derivative work.
- Demo Model 1 is separately copyrighted by Making Tracks Ltd.; use only under the permission granted by the Open Rails Management Team and include the required acknowledgement.
- Do not commit Demo Model 1 assets into this source repository.
- This audit is intentionally an initial dependency-level review. Each project's source files still need a systematic API/dependency scan before porting.

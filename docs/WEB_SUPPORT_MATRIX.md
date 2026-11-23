# Browser support and content compatibility matrix

**Status:** Published compatibility expectations; browser/device runtime validation remains outstanding.

This matrix describes the experimental browser port's current implementation surface. "Supported" means a format or feature is parsed or rendered in the prototype, not that it matches the full Open Rails desktop simulator.

## Browser runtime requirements

| Requirement | Status / note |
|---|---|
| .NET 10 WebAssembly runtime | Required by the host project |
| WebAssembly support | Required; the app is published as WASM |
| WebGL 2 | Required for route/terrain preview rendering |
| JavaScript modules | Used by the renderer and browser gamepad reader |
| ZIP file import | Browser-side user-selected content; files are not uploaded by the port |
| Graphics quality presets | Low/Medium/High controls reduce WebGL pixel scale, terrain tile cap, track sampling density, and geometry budgets |\n| Pointer/touch interaction | Route preview orbit/zoom and pointer-driven control sliders |
| Keyboard input | Focused driving-control pad shortcuts |
| Standard Gamepad API | Optional; standard-mapped trigger/D-pad driving controls |\n| Web Audio API | Optional low-volume synthesized engine/brake audio; requires explicit enable click |

## Content formats and scope

| Content | Prototype status | Limitations |
|---|---|---|
| Route ZIP packages | Import and local virtual-content indexing | Not all route files are parsed |
| `.trk` route metadata | Parsed and route selection displayed | Full route/activity metadata parity is incomplete |
| TDB/TSECTION track data | Track topology and geometry subset parsed | Signalling, activities, timetable and full switch logic are not implemented |
| SIMISA/SBR terrain streams | Stream-based reader; compressed/uncompressed fixtures covered by regression tests | Not every SBR variant or route package is validated |
| Terrain `.t` data | Terrain samples/patch metadata and mesh preview | Preview is capped to a small tile count and first LOD |
| WORLD scenery | Placements, supported shapes and basic supported textures rendered | Many block variants/material features are incomplete |
| Track shapes (`.s`) | Geometry subset loaded and rendered | Full shape/material/animation support is not implemented |
| ACE textures | Supported RGBA8 subset for preview | Seasonal variants and unsupported pixel formats are excluded; material/shader parity is partial |
| TRAINSET rolling stock | Consist/ENG/WAG metadata and shape assets; first LOD mesh rendering | Cab views, animation, full materials, native control systems and sound remain incomplete |
| Consist files | Metadata and vehicle placement across selected route | Activity/service integration and full desktop rules remain incomplete |
| Activity files (`.act`) | Route-folder activity file selector; selected file is displayed | Activity parsing, services, timetable, event logic, and execution are not implemented |

## Browser/device validation status

| Environment | Validation status |
|---|---|
| GitHub Actions Linux runner | Regression tests and WebAssembly publish are CI-validated when the latest workflow passes |
| Desktop Chromium-family browser | Expected candidate where WebAssembly and WebGL 2 are enabled; runtime/device validation not yet recorded |
| Desktop Firefox | Expected candidate where WebAssembly and WebGL 2 are enabled; runtime/device validation not yet recorded |
| Desktop Safari | Expected candidate where WebAssembly and WebGL 2 are enabled; runtime/device validation not yet recorded |
| Android browsers | WebAssembly/WebGL 2 capability is required; performance and touch behavior not yet device-validated |
| iOS/iPadOS browsers | WebAssembly/WebGL 2 capability is required; performance, memory, and touch behavior not yet device-validated |

A browser that disables WebGL 2, blocks required WASM execution, or runs under restrictive resource limits may not be able to use the scene preview. Browser/version minimums cannot be responsibly claimed until the app is exercised on actual browser builds and devices.

## Validation records

- Automated tests: see GitHub Actions workflow for controller and parsing regression suites.
- Publish validation: WebAssembly build/publish is part of CI.
- Real route-package validation: still pending; use [real-route validation checklist](REAL_ROUTE_VALIDATION.md).
- Browser/device runtime matrix: intentionally marked unverified above; no manual browser testing is claimed.

## Compatibility policy

Report limitations honestly. Passing CI validates automated tests and compilation/publishing, not browser graphics, input latency, mobile memory usage, or real-route fidelity. Add a tested browser/device row only after recording the exact browser/device version and test result.

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
| ZIP file import | Small ZIPs use the bounded in-memory importer; large ZIPs up to 30 GB can be streamed into OPFS without buffering the compressed archive |
| Graphics quality presets | Low/Medium/High controls reduce WebGL pixel scale, terrain tile cap, track sampling density, and geometry budgets; preset persists in browser storage with reset-to-High |
| Pointer/touch interaction | Route preview orbit/zoom and pointer-driven control sliders |
| Keyboard input | Focused driving-control pad shortcuts |
| Standard Gamepad API | Optional; standard-mapped trigger/D-pad driving controls |
| Web Audio API | Optional low-volume synthesized engine/brake audio; requires explicit enable click |

## Content formats and scope

| Content | Prototype status | Limitations |
|---|---|---|
| Route ZIP packages | Small ZIP import; large OPFS ZIP indexing; staged `.trk` metadata parsed with upstream STFReader; staged route can be loaded through the existing route pipeline | Staged-route compatibility currently materializes expanded entries into memory with a 2 GiB aggregate ceiling; archives above that ceiling remain indexed but cannot yet enter the synchronous route readers |
| `.trk` route metadata | Parsed and route selection displayed | Full route/activity metadata parity is incomplete |
| TDB/TSECTION track data | Track topology, geometry, signal heads and junction adjacency parsed | Track circuits remain vector-node approximations; full switch interlocking and signal-position segmentation are not implemented |
| SIMISA/SBR terrain streams | Stream-based reader; compressed/uncompressed fixtures covered by regression tests | Not every SBR variant or route package is validated |
| Terrain `.t` data | Terrain samples/patch metadata and mesh preview | Preview is capped to a small tile count and first LOD |
| Route path files (`.pat`) | TrackPDP and TrPathNode tables parsed; main/siding links traced; service and activity paths are matched by PathID | Branch choice and path-following train movement are not yet a complete AI/player path runtime |
| Service files (`.srv`) | Service definition, consist reference, path reference, efficiency and initial speed parsed; duplicate/ambiguous references rejected; selected service binds its consist and initial state to a validated connected-route projection of every `.pat` waypoint; single-service AI path-follow mode follows waypoint order, stops at native waiting points until released, brakes for the path end, stops for evaluated Stop signals and applies configured Approach speed limits | Native route speed-limit profiles, reversal-point transitions (AI mode is withheld for paths containing them), timetable scheduling, multi-train traffic conflict handling and independent concurrent AI consists are not implemented |
| Signal configuration (`sigcfg.dat`) | Signal types, custom functions, lights, draw states, aspects, clear-ahead counts and script-file references parsed; native SIGSCR assignments, integer arithmetic, IF/ELSEIF/ELSE, comparisons, logical operators and RETURN execute with a bounded instruction budget; BLOCK_STATE, ROUTE_SET and DEF_DRAW_STATE are bound to browser signal context | Native signal-topology functions such as NEXT_SIG_LR, timing triggers, call-on, train-path queries and C# signal scripts are not yet implemented; unsupported functions fail restrictive |
| Route signalling runtime | Signal heads attached to vector-node circuits; all consist vehicles contribute occupancy; selected branches required at junctions; native SIGSCR execution integrated into aspect evaluation; unsupported script functions or invalid aspects fail to Stop | Circuits are approximate vector-node blocks, not native signal-position circuits; full route interlocking and native signal-position circuit segmentation remain incomplete |
| WORLD scenery | Placements, supported shapes and basic supported textures rendered | Many block variants/material features are incomplete |
| Track shapes (`.s`) | Geometry subset loaded and rendered | Full shape/material/animation support is not implemented |
| ACE textures | Structured RGBA8 and packed BGR565/BGRA5551/BGRA4444 plus DXT1/3/5 base-mip decoding for cab artwork | Full mip/shader/material parity, seasonal variants and every legacy texture variant are not verified |
| TRAINSET rolling stock | Consist/ENG/WAG metadata, shape assets, traction/resistance, coupler springs/damping/slack, brake-shoe and handbrake ratings; original CVF cab artwork is rendered; native throttle, train-brake and direction levers bind to the browser controller; speed, throttle, brake, brake-pipe, brake-cylinder, door and emergency-brake telemetry are exposed | Independent engine/dynamic brake, multi-controller ControlID mapping, animated artwork/gauges, full materials, complete native brake equipment and sound parity remain incomplete |
| Consist files | Metadata, vehicle placement, per-car spring/damper coupler forces, pneumatic prototype brakes and rated mechanical handbrake; consist length is checked against remaining connected route length before simulation | Full desktop consist rules, native brake-valve/equipment parity and AI train movement remain incomplete |
| Activity files (`.act`) | Header and time/location/action event parsing; time, location, reach-speed and AllStops events execute; AllStops uses native `.pat` waiting points; reversible action events re-arm after their trigger condition clears; activation/outcome references are validated as a complete graph before execution to prevent partial state mutation from malformed events | Passenger pickup, wagon pickup/drop-off, train assembly, station/service side effects and full activity timetable logic remain incomplete |

## Browser/device validation status

| Environment | Validation status |
|---|---|
| GitHub Actions Linux runner | Regression tests and WebAssembly publish are CI-validated when the latest workflow passes |
| Desktop Chromium-family browser | Target environment where WebAssembly and WebGL 2 are enabled; runtime/device validation not yet recorded |
| Desktop Firefox | Target environment where WebAssembly and WebGL 2 are enabled; runtime/device validation not yet recorded |
| Desktop Safari | Target environment where WebAssembly and WebGL 2 are enabled; runtime/device validation not yet recorded |
| Mobile browsers | Outside the current target; this project targets desktop PC browsers only |

A browser that disables WebGL 2, blocks required WASM execution, or runs under restrictive resource limits may not be able to use the scene preview. Browser/version minimums cannot be responsibly claimed until the app is exercised on actual browser builds and devices.

## Validation records

- Automated tests: see GitHub Actions workflow for controller and parsing regression suites.
- Publish validation: WebAssembly build/publish is part of CI.
- Real route-package validation: still pending; use [real-route validation checklist](REAL_ROUTE_VALIDATION.md).
- Browser/device runtime matrix: intentionally marked unverified above; no manual browser testing is claimed.

## Compatibility policy

Report limitations honestly. Passing CI validates automated tests and compilation/publishing, not browser graphics, input latency, desktop memory usage, or real-route fidelity. Add a tested browser/device row only after recording the exact browser/device version and test result.

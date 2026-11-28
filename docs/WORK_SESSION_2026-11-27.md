# Work session — 2026-11-27

## Working branch and safety

- Current working branch: `port/axle-drive-state-hardening`.
- Current head at session start: `8e237d42f32edcef3c2de4b70d9c90476c788548`; this branch is a direct successor to `port/upstream-core-files`.
- Session changes: axle-drive snapshot regression tests and CI trigger coverage for all `port/*` branches.
- Draft PR #4 targets `main`; do not merge it until its CI and divergence review are complete.
- Existing `main` remains untouched. No branches were deleted or force-pushed.
- CI was observed running for this branch after the workflow trigger update; result pending at the time of this report.

## Verified implementation surface

The browser prototype already has route ZIP ingestion and content-store layers, STF/SBR readers, route metadata/TDB/TSECTION parsing, route track geometry, terrain patch parsing/mesh generation, WORLD scenery parsing/rendering, rolling-stock shape/texture loading, route-aligned consist placement, and a prototype multi-car simulation with fixed-step scheduling, traction ramps, resistance, pneumatic state, coupler dynamics, save/restore and controls.

These are a working subset, not full upstream Open Rails parity. The detailed matrix and roadmap are the status sources:
- `WEB_SUPPORT_MATRIX.md`
- `ROADMAP.md`
- `UPSTREAM_PORT_AUDIT.md`
- `REAL_ROUTE_VALIDATION.md`
- `ORIGINAL_UI_PORT_PLAN.md`

## Work completed in this session

1. Added regression tests for axle-drive snapshot read-only wrappers, null snapshot collections, and non-finite snapshot state rejection without mutation.
2. Expanded workflow push triggers from the one named port branch to `port/*`, so follow-up port branches can receive CI.
3. Opened draft PR #4 for the hardened branch. It is explicitly draft and not merged.

## Required next engineering gates (priority order)

1. **CI and branch integration:** complete PR #4 CI; review its 135-file/290-commit diff against `main`, avoid accidental duplication with PR #3, and only merge after conflicts, test outcomes, and source provenance are understood.
2. **Real-route E2E:** import the authorized Demo Model 1 package in the browser, record import/parse diagnostics and renderer results, and complete the checklist in `REAL_ROUTE_VALIDATION.md`. The route asset must be supplied through authorized user access; it is not stored in the repo.
3. **Upstream simulation parity:** port/adapt source-backed Open Rails systems rather than treating the prototype equations as parity. Remaining scope includes locomotive-specific traction/power and controls, full pneumatic brake propagation/distributor/fade variants, wheel slip/adhesion dynamics, coupler slack/break behavior and native parameter parsing, and stateful train simulation.
4. **Route systems:** native track circuits and signal-position segmentation, switch/interlocking rules, train path progression/reversal, route speed limits, route-specific validation, and activity/service path handling.
5. **Rolling stock:** complete shape/material/texture and animation-controller variants; wheels, bogies, suspension, cab view/camera and sound asset parity; asset-loading and large-route performance.
6. **Activities and traffic:** activity event/action execution, timetable, dispatching, AI trains and conflict handling, weather/environment, station/passenger/traffic systems.
7. **Original UI:** inventory and port the original Open Rails menus, setup screens, HUD, cab, options, dialogs and pause flows with original layout and behavior; wire controls only to implemented systems.
8. **Audio and input:** source-authentic sound asset and event pipelines, full command map, train protection and cab/signal commands; keyboard/controller bindings and settings parity.
9. **Rendering:** complete original material/shader variants, seasonal textures, animation, lighting, cameras, scenery object compatibility, LOD and resource/performance behavior.
10. **Desktop-browser acceptance:** validate browser-specific memory, graphics and large-package behavior; maintain the compatibility matrix and regression tests. No full parity claim until all gates have evidence.

## Acceptance discipline

- Mark a subsystem complete only after source provenance, implementation, tests, browser build and end-to-end evidence exist.
- Keep licensed route assets out of git; retain required Making Tracks Ltd. acknowledgement for Demo Model 1.
- Preserve original Open Rails UI design; AI assistance may be used for internal engineering but not redesign the UI.

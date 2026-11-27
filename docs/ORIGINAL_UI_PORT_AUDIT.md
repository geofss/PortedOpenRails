# Original Open Rails MainForm UI parity audit

**Upstream baseline:** Open Rails commit `dfb5246ca9da46e352cfcd2a365d284ac55c1a1f`  
**Primary source:** `Source/Menu/MainForm.Designer.cs` and `Source/Menu/MainForm.cs`  
**Browser implementation:** `src/OpenRails.Web/App.razor`, component Razor files, and `wwwroot/css/app.css`

This audit compares the browser interface with the actual upstream MainForm. It is deliberately not a redesign specification. The browser port must preserve upstream labels, control grouping, selection behavior, and enable/disable rules wherever the browser platform permits.

## Present in the browser UI

- Open Rails title, Start, Resume / Replay, Options, Tools, and Logging command strip.
- Route ZIP selection, stored archive indexing, route selection, and route metadata.
- Consist and service selection, activity selection, route diagnostics, and route validation.
- Terrain/world preview, original cab panel, driving controls, and live signal diagnostics.
- Content package import, ordering, removal, and resolved namespace diagnostics.
- Footer Open Rails attribution and the separate `Ported by geofss` credit.

## MainForm controls still missing or not behaviorally equivalent

| Upstream MainForm control / behavior | Browser status | Required port work |
|---|---|---|
| Installation profile combo box | Partial substitute only | Browser profile selector with named profiles and persistent package roots; do not expose a fake filesystem path |
| Route combo box | Partial | Preserve route selection semantics and profile-dependent availability |
| Activity / Timetable radio mode | Missing | Add native mode switch and mutually exclusive mode panels |
| Activity mode: Activity, Locomotive, Consist | Partial | Activity list exists; locomotive/consist dependency and enable rules are incomplete |
| Starting at / Heading to | Missing as native selectors | Resolve start locations and destination choices from activity/route data |
| Time, Duration, Season, Weather, Difficulty | Partial | Activity metadata is displayed, but editable native controls and upstream defaults are not ported |
| Timetable set, timetable, day, train, season, weather, weather file | Missing UI | Connect native timetable reader and timetable runtime to the MainForm selection panel |
| Singleplayer Start / Resume / Replay | Partial | Start is currently an in-page navigation; resume/replay persistence and dialog flow need native behavior |
| Multiplayer client/server, host/port, username, Start MP / Resume MP | Missing | Port only after browser-compatible multiplayer transport and session lifecycle exist |
| Content button | Partial | Existing content package manager is linked under Tools, but native content workflow is not complete |
| Tools dropdown | Partial | Current command is an anchor, not the upstream menu/context actions |
| Documents dropdown | Missing | Port document/help links and native menu grouping |
| Notifications indicator/pages | Missing | Port update/notification count and page navigation when corresponding browser services exist |
| Options dialog | Partial | Driving settings exist; full upstream options pages and persistence are not ported |
| MainForm responsive layout | Partial | Current page is a linear browser layout; upstream two-pane profile/route and details layout is not yet matched |

## Port constraints

- Keep original Open Rails terminology and control semantics. Do not invent substitute simulator features.
- Browser profile storage replaces direct Windows installation-folder browsing; this is a platform adaptation, not a reason to remove the profile concept.
- Do not show Multiplayer Start as enabled until a real compatible session transport and lifecycle are implemented.
- Keep route, activity, timetable, and consist selections data-driven from imported content.
- Keep UI behavior separate from parser/simulation internals so the original UI can be ported without changing their semantics.
- The current stylesheet now moves the page toward the upstream WinForms visual baseline, but visual alignment alone does not constitute UI parity.

## Acceptance gates

1. Match the upstream MainForm grouping and control inventory.
2. Match selection dependencies, defaults, enabled states, and command behavior.
3. Verify Activity and Timetable modes against upstream selection logic.
4. Verify Options, Tools, Documents, and Notifications workflows.
5. Keep unsupported browser-only capabilities visibly unavailable rather than presenting inert controls as functional.
6. Compare browser screenshots with the pinned upstream MainForm at the same logical scale.


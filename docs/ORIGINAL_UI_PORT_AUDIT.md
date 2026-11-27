# Original Open Rails MainForm UI parity audit

**Upstream baseline:** Open Rails commit `dfb5246ca9da46e352cfcd2a365d284ac55c1a1f`  
**Primary sources:** `Source/Menu/MainForm.Designer.cs`, `Source/Menu/MainForm.cs`, `Source/Menu/Options.Designer.cs`, and `Source/Menu/Options.cs`  
**Browser implementation:** `src/OpenRails.Web/App.razor`, component Razor files, and `wwwroot/css/app.css`

This is a source-based parity audit, not a redesign brief. The browser UI must preserve upstream labels, grouping, selection dependencies, and enable/disable behavior wherever browser APIs allow.

## Implemented in the browser UI

- Original Open Rails title and footer attribution, with separate `Ported by geofss` credit.
- Two-column MainForm-style layout with installation profile, route, mode, singleplayer, multiplayer, and details/commands groups.
- Activity / Timetable mode switch.
- Activity selection backed by route activity files and native activity metadata.
- Locomotive and consist selectors backed by parsed rolling-stock content; locomotive selection resolves its owning consist.
- Timetable file discovery, native delimiter-grid parsing, timetable train-column selection, description and per-train briefing display.
- Start action gated by a usable route network, connected traversal, resolved consist and initialized simulation; Resume / Replay remains disabled until native save selection and restore are implemented.
- Multiplayer Client / Server controls are shown disabled; browser transport and session lifecycle are not falsely represented as available.
- Options dialog with all ten upstream page names: General, Audio, Video, Simulation, Keyboard, RailDriver, Data logger, Evaluation, System, Experimental.
- Browser preference persistence for Logging, browser autosave preference/interval, audio output preference, and selected mode. These are UI preferences; they are not yet equivalent to native simulator settings.
- Tools dropdown with content-package and route-diagnostics navigation; Documents dropdown with Open Rails documentation/source links.
- Notifications area with explicit unavailable state rather than a fabricated feed.
- Route ZIP import, large archive staging/indexing, route selection, route metadata, activity/service diagnostics, cab view, driving controls, signal diagnostics and content package priority/removal.
- Classic Windows Forms-style palette, compact controls and responsive two-column-to-single-column layout.

## Remaining behavioral parity work

| Upstream control / behavior | Current browser status | Remaining work |
|---|---|---|
| Installation profiles | One browser content profile | Multiple named browser profiles, independent package namespaces, import/export and profile persistence |
| Activity / Explore workflow | Activity selection and locomotive/consist selectors exist | Native Explore activity mode; start-at and heading-to path selectors; exact locomotive-to-consist dependency; upstream enable rules |
| Activity environment | Native metadata displayed | Editable time, duration, season, weather and difficulty defaults/overrides; apply them to the simulation/activity runtime |
| Timetable set and timetable | Native timetable files are discovered and parsed | Native timetable-set grouping, timetable/day model, weather/season/weather-file selection, selection persistence and full timetable runtime integration |
| Timetable train | Train columns are selectable and briefing is displayed | Resolve train path/consist/direction/start into a runnable player service and enable Start only when all references validate |
| Singleplayer Start | Navigates to initialized browser driving panel | Match upstream action lifecycle and start semantics for activity and timetable sessions |
| Resume / Replay | Disabled intentionally | Port native save enumeration, compatibility checks, restore, replay selection and state migration |
| Multiplayer | Client/server panel shown disabled | Browser-compatible transport, protocol/session lifecycle, username/host validation, resume and start actions |
| Options — General | Basic browser preferences persist | Port native settings model, all General controls, validation, telemetry/update integration and migration |
| Options — Audio | Browser output preference only | Connect audio mixer and port sound volume/detail/external balance |
| Options — Video | Route viewer has separate camera/quality controls | Map upstream viewing distance, FOV, LOD, object density, antialiasing, shadows, wire, signal glow, VSync and environment controls to renderer |
| Options — Simulation | Page inventory and descriptions shown | Port and apply native locomotive/activity/physics/adhesion/braking/TCS/alerter options |
| Options — Keyboard | Existing driving key bindings shown elsewhere | Native command map, remapping, export, defaults and conflict validation |
| Options — RailDriver | Unavailable | Browser device support and native calibration/mapping |
| Options — Data logger / Evaluation | Inventory shown | Native log/event collection, configuration and downloadable output |
| Options — System | Browser limitations described | Port applicable language/update/window/performance preferences; replace OS-only settings with browser equivalents |
| Options — Experimental | Inventory shown | Port supported experimental settings and apply them to matching simulation systems |
| Tools | Content and route diagnostics navigation | Port TestingForm and remaining upstream tool menu entries with real behavior |
| Documents | Documentation and source links | Match exact upstream menu inventory and localized destinations |
| Notifications | Explicit unavailable state | Port update/notification feed, unread count, page/detail navigation and dismissal |
| Details panel | Route/activity/consist diagnostics are present | Match upstream detail selection, ordering, description and briefing behavior |
| Localization | Not yet ported as a UI system | Port resource catalog/localization and culture selection |
| UI automation/accessibility | Basic semantic labels and keyboard focus | Full keyboard navigation, focus order, dialogs, screen-reader labels and parity tests |

## Browser adaptation rules

- Browser content profiles replace direct Windows installation-folder browsing; they must still preserve profile isolation and precedence semantics.
- Do not enable Multiplayer, Resume, Testing, or settings until the corresponding browser behavior exists.
- Keep route/activity/timetable/consist selection data-driven from imported content.
- A visible control is not considered ported merely because it has the same label. It must preserve source behavior and state transitions.
- Do not redesign the Open Rails UI. Preserve upstream terminology, grouping, order and defaults; adapt only filesystem, windowing, device and browser-runtime boundaries.

## Acceptance gates

1. Complete the remaining behavioral parity work above against the pinned upstream handlers.
2. Add automated tests for selection dependencies, enabled states, settings persistence, timetable selection and Start eligibility.
3. Compare screenshots at the same logical scale against the upstream MainForm and Options dialogs.
4. Test Activity and Timetable workflows against the authorized Demo Model 1 route.
5. Verify all menu, dialog, keyboard and accessibility workflows in supported desktop browsers.
6. Keep unsupported capabilities unavailable until their backend is implemented and tested.

**Status:** MainForm shell and a substantial set of selection/command surfaces have been ported. This is not yet full UI parity; the table above is the remaining completion list.

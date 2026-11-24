# Original Open Rails UI port plan

## Principle
Preserve the original Open Rails UI system, layout language, interaction flow, and behavior. Do not replace it with a newly invented web-game interface. Adapt platform-specific interactions only when browser or mobile constraints require it, and document deviations.

## Scope to inventory and port
- Main menu, navigation, and menu transitions.
- Route selection, route details, and route loading.
- Activity and timetable selection and setup.
- Train/consist selection and setup.
- Driving HUD, cab display, cab controls, and camera/view controls.
- Pause and in-game menus.
- Options/settings pages and their persistence.
- Loading/progress screens, warnings, confirmations, and error dialogs.
- Keyboard, pointer/touch, and controller focus/navigation behavior.
- Accessibility and responsive behavior for small screens, without changing the original desktop UI unnecessarily.

## Implementation sequence
1. Inventory original UI source components, forms, dialogs, screens, and navigation transitions; record upstream file references and dependencies.
2. Build a screen/state map and identify shared controls, typography, layout primitives, and assets.
3. Port shared UI primitives and navigation behavior first, with regression tests for transitions and selection.
4. Port menu and setup flows, wiring them to existing browser route/consist/activity data APIs.
5. Port driving HUD/cab controls and bind them to simulation commands; do not show unsupported controls as functional.
6. Port pause/options/loading/error flows, including persistence and recovery.
7. Validate desktop layout and browser/mobile adaptations against upstream screenshots and behavior, documenting unavoidable differences.
8. Keep each screen parity status in a UI validation report; avoid marking overall UI parity complete until the end-to-end screen and interaction checklist passes.

## Acceptance criteria
- Every in-scope original UI screen is inventoried and represented.
- Screen transitions and selection flows match upstream behavior where supported.
- UI commands are wired to actual simulator or browser-port capabilities; unsupported behavior is clearly disabled or identified.
- Original visual structure and interaction behavior are preserved, with explicit documented browser/mobile adaptations.
- Desktop and mobile UI regression checks pass for the supported screen flows.
- UI parity is reviewed and evidence/results recorded.

## Status
Planning only. No original UI parity is claimed by this document. The current browser UI is experimental and incomplete.

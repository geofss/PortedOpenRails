# Original Open Rails UI source map

The browser UI is a port of the upstream Menu application, not a redesign. Upstream references below are pinned to the Open Rails `master` source tree and must be rechecked against the source revision used for each port.

## Main form

| Upstream source | Native responsibility | Browser-port mapping |
|---|---|---|
| [Source/Menu/MainForm.Designer.cs](https://github.com/openrails/openrails/blob/master/Source/Menu/MainForm.Designer.cs) | Main form layout and control inventory: installation profile, route, activity/timetable mode, consist, start location/destination, start time, weather/season/difficulty, Start, Resume/Replay, Options, Tools, Documents, Content, Logging, multiplayer | `App.razor` currently hosts route/package import, route selection, activity metadata, consist/service selection, route start vector, and simulation controls. The original form's complete screen layout and transitions are not yet ported. |
| [Source/Menu/MainForm.cs](https://github.com/openrails/openrails/blob/master/Source/Menu/MainForm.cs) | Main-form state, asynchronous route/activity/consist loading, selected activity and timetable state, start/resume command handling | Browser-side route and package loading currently lives in `App.razor`; native loading and launch semantics still require parity work. |
| [Source/Orts.Menu/Routes.cs](https://github.com/openrails/openrails/blob/master/Source/Orts.Menu/Routes.cs) | Route discovery and route metadata | Browser route metadata is read from imported packages through the existing route readers. |
| [Source/Orts.Menu/Activities.cs](https://github.com/openrails/openrails/blob/master/Source/Orts.Menu/Activities.cs) | Activity discovery and activity-derived consist/service metadata | Browser activity discovery and metadata parsing are present; selection and launch parity remain incomplete. |
| [Source/Orts.Menu/Consists.cs](https://github.com/openrails/openrails/blob/master/Source/Orts.Menu/Consists.cs) | Consist discovery and locomotive resolution | Browser consist catalog and rolling-stock metadata readers are present; the full native selection workflow remains incomplete. |
| [Source/Orts.Menu/Paths.cs](https://github.com/openrails/openrails/blob/master/Source/Orts.Menu/Paths.cs) | Route path discovery | Browser path readers and service/path binding are present; native start-location and destination selection parity remains incomplete. |

## Options and driving screens

- [Source/Menu/Options.Designer.cs](https://github.com/openrails/openrails/blob/master/Source/Menu/Options.Designer.cs) defines the native Options tabs: General, Audio, Video, Simulation, Keyboard, RailDriver, Data Logger, Evaluate, System, and Experimental. Browser settings currently cover only a subset of driving/audio/gamepad preferences.
- `Components/TrainDrivingControls.razor` is a browser adaptation of driving controls; it is not yet a complete port of the native driving HUD or all simulator commands.
- `Components/CabViewPanel.razor` reads original cab-view assets and binds supported instruments and levers to the browser train controller.
- `Components/RouteTerrainViewer.razor` and `Components/RollingStockViewer.razor` provide browser rendering surfaces, not a claim of full native viewer parity.

## Porting rules

1. Preserve native screen order, labels, control semantics, and selection dependencies from the upstream source.
2. Bind every enabled control to a real browser-port capability. Unsupported native commands remain disabled or explicitly unavailable.
3. Keep browser-specific package import/storage adaptations separate from the native menu flow.
4. Do not replace original cab, route, rolling-stock, or menu assets with AI-generated substitutes.
5. Do not claim UI parity until each screen and its transitions pass an explicit comparison checklist.

## Current status

- Source inventory: completed for the main form, route/activity/consist/path discovery, and Options tabs.
- Browser data and control surfaces: partially implemented in `App.razor` and the existing components.
- Main-form layout and transition parity: not complete.
- Options-tab parity: not complete.
- End-to-end UI parity: not claimed.

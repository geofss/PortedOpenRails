# PortedOpenRails 🚂🌍

**Open Rails, being ported to run in a web browser — while keeping its original C# code wherever possible.**

This repository is the experimental browser-port foundation. The first milestone uses **.NET 10 Blazor WebAssembly** as a host for browser-compatible C#.

> This is an early technical experiment, not a playable simulator yet. The desktop renderer and platform-specific APIs will need compatibility work; direct compilation of the entire desktop application is not assumed.

## Repository layout

- `src/OpenRails.Web/` — browser-hosted .NET WebAssembly app
- `docs/ROADMAP.md` — project milestones and engineering principles
- `docs/INPUT_CONTROLS.md` — keyboard, pointer/touch, and standard-mapped gamepad reference

## Upstream port strategy

The project is now explicitly prioritizing reuse and browser-porting of the real Open Rails source over expanding parallel prototype implementations. See the [upstream port audit and migration sequence](docs/UPSTREAM_PORT_AUDIT.md) for the pinned upstream revision, dependency findings, and acceptance gates. This is a migration plan; upstream code integration is not yet complete. See [existing upstream source provenance](docs/UPSTREAM_SOURCE_PROVENANCE.md) for files already derived from Open Rails and the remaining parity audit.

## First build

With the .NET 10 SDK installed:

```sh
dotnet restore src/OpenRails.Web/OpenRails.Web.csproj
dotnet build src/OpenRails.Web/OpenRails.Web.csproj
dotnet run --project src/OpenRails.Web/OpenRails.Web.csproj
```

The development server prints a local URL to open in a browser.

## Upstream project and licensing

Open Rails upstream: https://github.com/openrails/openrails

Open Rails is licensed under **GPL-3.0**. Any upstream source copied or modified for this project must retain its applicable copyright and license notices, and derivative distributions must comply with the GPL-3.0. This initial browser-host scaffold is a separate experimental layer; check each upstream component's notices before porting it.

## Demo Model 1 permission and attribution

The Open Rails Management Team has granted permission to use Demo Model 1 to test this browser port and to distribute it to project users, subject to acknowledging the copyright of Making Tracks Ltd. The required acknowledgement and handling conditions are recorded in [Demo Model 1 attribution and permission](docs/DEMO_MODEL_1_ATTRIBUTION.md). The route package still needs to be supplied/imported and validated; permission does not mean integration is complete. Keep the copyrighted route assets out of this source repository.

## Status

See [browser support and content compatibility matrix](docs/WEB_SUPPORT_MATRIX.md) for the prototype's supported subset and explicitly unverified browser/device expectations.

The browser prototype currently imports route ZIP content, parses route metadata and TDB/TSECTION track topology, loads terrain, original track shapes, and WORLD scenery, and renders a WebGL2 scene. It loads original rolling-stock meshes and supported ACE textures from TRAINSET folders. Route, activity-file, and consist selection are available (activities are selected only, not runnable); route and consist selection support vehicle placement across selected connected multi-node routes, including reversed vector nodes and explicit branch selection. A distance-indexed route profile supplies per-car position, heading, and grade to live simulation and rendering.

Browser driving is an experimental simulation slice with fixed-step multi-car motion, parsed locomotive traction metadata, per-car Davis resistance, pointer-based throttle/brake sliders, focused keyboard throttle/brake shortcuts, standard-mapped gamepad trigger throttle/brake and D-pad reverser controls, optional low-volume synthesized engine/brake audio, brake-pipe and brake-cylinder response models, route-grade resistance, per-car live track-grade telemetry, coupler force ratings, and dynamic coupler force telemetry reported per adjacent-car boundary with signed tension/compression states and independent limit handling. Pneumatic brake state and dynamic coupler forces advance/recompute inside each accepted fixed simulation tick, so dropped browser-frame time does not advance those subsystems ahead of vehicle motion. Coupler telemetry is initialized for every adjacent-car boundary before the first simulation tick. The current spring, damping, and slack values remain prototype defaults pending native ORTS parameter parsing and validation. These systems are not yet a complete or validated Open Rails simulation.

Package handling includes import, removal, and reorder controls. Route traversal supports connected track vectors and explicit branch selection at the first ambiguous junction. Terrain preview now has Low/Medium/High quality presets that adjust render resolution, tile limits, track sampling, and geometry budgets for more responsive devices; selected preview quality is saved in browser storage and can be reset to High. Terrain, scenery, track-shape, and rolling-stock asset pipelines remain experimental and have limited format/material support.

**Not yet complete:** authentic Open Rails UI parity, full route/activity/signalling support, complete rolling-stock animation and cab systems, authentic rolling-stock sound (the current generated audio is a prototype), weather, multiplayer, full upstream simulation integration, and production-quality performance. The project remains an experimental browser port and is not yet a complete playable Open Rails simulator.

The current CI run is validating the gamepad input integration and recent regression-test updates. See GitHub Actions for the latest verified status.

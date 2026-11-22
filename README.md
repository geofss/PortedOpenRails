# PortedOpenRails 🚂🌍

**Open Rails, being ported to run in a web browser — while keeping its original C# code wherever possible.**

This repository is the experimental browser-port foundation. The first milestone uses **.NET 10 Blazor WebAssembly** as a host for browser-compatible C#.

> This is an early technical experiment, not a playable simulator yet. The desktop renderer and platform-specific APIs will need compatibility work; direct compilation of the entire desktop application is not assumed.

## Repository layout

- `src/OpenRails.Web/` — browser-hosted .NET WebAssembly app
- `docs/ROADMAP.md` — project milestones and engineering principles

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

## Status

The browser prototype currently imports route ZIP content, parses route metadata and TDB/TSECTION track topology, loads terrain, original track shapes, and WORLD scenery, and renders a WebGL2 scene. It loads original rolling-stock meshes and supported ACE textures from TRAINSET folders. Route and consist selection support route-aligned vehicle placement; the renderer also supports live vehicle-position updates.

Browser driving is an experimental simulation slice with fixed-step multi-car motion, parsed locomotive traction metadata, per-car Davis resistance, braking controls, brake-pipe and brake-cylinder response models, route-grade resistance, per-car live track-grade telemetry, coupler force ratings, dynamic coupler force telemetry, and per-car telemetry. Dynamic coupler slack/spring/damper force calculations are being integrated into browser driving; the current spring, damping, and slack values are prototype defaults pending native ORTS parameter parsing and validation. These systems are not yet a complete or validated Open Rails simulation.

Package handling includes import, removal, and reorder controls. Route traversal supports connected track vectors and explicit branch selection at the first ambiguous junction. Terrain, scenery, track-shape, and rolling-stock asset pipelines are experimental and have limited format/material support.

**Not yet complete:** authentic Open Rails UI parity, full route/activity/signalling support, complete rolling-stock animation and cab systems, sound, weather, multiplayer, full upstream simulation integration, and production-quality performance. The project remains an experimental browser port and is not yet a complete playable Open Rails simulator.

CI last confirmed passing: run #817 (tests/build and deployment). Subsequent coupler-dynamics integration changes are under CI verification; check GitHub Actions for the current status.

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

The current experimental browser slice imports route ZIP content, parses route metadata and TDB/TSECTION track topology, loads terrain, original track shapes, and WORLD scenery, and renders them in a WebGL2 scene. It also loads original rolling-stock `.s` meshes and supported ACE textures from each TRAINSET folder. When a route and consist are loaded together, first-LOD vehicles are placed along the longest available TDB vector path using sampled straight/curved centerline geometry, parsed vehicle lengths, tangent headings, and consist flip flags. Rolling stock shares the route renderer and texture pipeline. The browser virtual-content pipeline discovers `.con` consists, parses car identity, flips, optional loads, durability, maximum velocity, serial/UID and TCS metadata, resolves `.eng`/`.wag`, ORTS EOT `.eot` and TCS script references, reports missing assets, and tracks package provenance. Layered override behavior, asset resolution, texture lookup, and path-traversal rejection are regression-tested. This is static route-aligned placement, not a moving simulator yet. A browser-safe fixed-step scheduler and single-car longitudinal motion integrator are now available as simulation foundations, but upstream force generation, multi-car coupler dynamics, braking, traction, adhesion, vehicle animation, controls, signalling, activities, and the authentic Open Rails UI remain unfinished. Advanced material modes and seasonal textures also remain incomplete.

This is **not a playable simulator**: train movement/physics, continuous multi-node traversal, controls, the authentic Open Rails UI, activities, signalling, and full simulation integration remain unfinished. The preview is a development tool, not a substitute for the original Open Rails UI.

The latest confirmed CI deployment (run #563) passed JavaScript syntax validation, all 120 regression tests, WebAssembly publish, and GitHub Pages deployment.

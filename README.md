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

The current experimental browser slice can import route ZIP content, parse original route metadata and track topology, discover/load terrain and track-shape assets, and display a WebGL preview with terrain patches, route track overlays, original track-shape geometry, and WORLD scenery meshes using upstream matrix/quaternion transforms. It also loads original rolling-stock `.s` files for a first-LOD consist preview and resolves supported ACE textures from each vehicle's TRAINSET folder; RGBA8 material batches render with UVs and alpha-test support. The consist preview is a straight lineup, not yet aligned to route track or animated. The browser virtual-content pipeline also discovers `.con` consists, parses car entries/flips and optional loads using upstream position/state enums plus serial, next UID, durability, maximum-velocity and TCS metadata, resolves rolling-stock `.eng`/`.wag`, ORTS EOT `.eot` and TCS script references, reports missing assets, and shows package provenance. Layered override behavior, asset resolution, texture lookup, and path-traversal rejection are regression-tested. Advanced material modes and seasonal textures remain incomplete.

This is **not a playable simulator**: route-aligned train placement and animation, train physics and controls, the authentic Open Rails UI, activities, signalling, and full simulation integration remain unfinished. The preview is a development tool, not a substitute for the original Open Rails UI.

The latest confirmed CI deployment (run #545) passed JavaScript syntax validation, all 111 regression tests, WebAssembly publish, and GitHub Pages deployment.

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

The current experimental browser slice can import route ZIP content, parse original route metadata and track topology, discover/load terrain and track-shape assets, and display a WebGL preview with terrain patches, route track overlays, and original track-shape geometry, including basic RGBA8 ACE texture batches and alpha-test materials, plus WORLD scenery shape meshes using upstream matrix/quaternion transforms. The browser virtual-content pipeline also discovers .con consists, parses car entries/flips/optional loads, resolves rolling-stock .eng/.wag and ORTS EOT .eot references, reports missing assets, and shows which package supplies resolved files. Layered override behavior and asset resolution are regression-tested. Advanced material modes and seasonal textures remain incomplete.

This is **not a playable simulator**: train/consist rendering, authentic simulator UI, train physics and controls, activities, signalling, and full simulation integration remain unfinished. The preview is a development tool, not a substitute for the original Open Rails UI.

The latest confirmed CI deployment (run #396) passed JavaScript syntax validation, build, regression tests, and deployment. Run #399 has passed build and regression tests; deployment is pending.

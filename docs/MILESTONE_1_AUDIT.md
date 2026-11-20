# Milestone 1 — upstream source compatibility audit

## Upstream inspected

- Repository: https://github.com/openrails/openrails
- Branch inspected: `master`
- Architecture reference: `Docs/Architecture.md`
- Upstream license: GPL-3.0

## Findings

The upstream architecture separates formats, parsing, input, simulation, sound, UI, and viewer concerns. Its documented desktop threading model includes render, updater, loader, sound, watchdog, and web-server processes. A browser port cannot assume those desktop process/threading patterns transfer directly to WebAssembly.

The upstream `Source/Orts.Common/Orts.Common.csproj` targets `net6-windows` and references Windows-oriented dependencies including MonoGame WindowsDX and System.Management. The full upstream project therefore cannot be referenced directly by the current `net10.0` Blazor WebAssembly host.

## First component selected

Ported `ORTS.Common.AccelerationController` from:

`Source/Orts.Common/AccelerationController.cs`

into:

`src/OpenRails.Web/Simulation/AccelerationController.cs`

Why this component:
- It is real upstream C# simulation-control logic.
- Its implementation uses only `System` and basic floating-point arithmetic.
- It does not directly depend on Windows APIs, desktop rendering, audio, or MonoGame.
- It is a useful candidate for a later train throttle/brake control integration.

The source file retains its upstream copyright and GPL-3.0 notice. This project must continue to comply with GPL-3.0 for copied or modified upstream code.

## Validation boundary

A successful WebAssembly build proves this source compiles as part of the browser project. It does not yet prove the controller is connected to a train model or that its numerical behavior has been regression-tested against upstream. Those are follow-up tasks.

## Next engineering steps

1. Add automated tests for controller activation, coefficient adjustment, output clamping, and representative input sequences.
2. Compare test outputs with the upstream implementation.
3. Identify a small format/parser component with no Windows-only dependencies.
4. Design a browser rendering adapter; do not attempt to transplant the desktop MonoGame renderer wholesale.

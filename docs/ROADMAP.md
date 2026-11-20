# PortedOpenRails roadmap

## Milestone 0 — Browser foundation
- [x] Create a .NET WebAssembly host project
- [ ] Build and launch it in a browser
- [ ] Record the first compatibility results

## Milestone 1 — Compatibility investigation
- [ ] Inspect the upstream Open Rails target frameworks and project references
- [ ] Identify APIs that are unavailable in browser WebAssembly
- [ ] Select a small, dependency-light original C# component for the first port

## Milestone 2 — Simulation core
- [ ] Bring across compatible simulation code while preserving upstream notices
- [ ] Add browser-safe adapters for platform services
- [ ] Test simulation behavior independently of rendering

## Milestone 3 — Browser rendering
- [ ] Evaluate a WebGL/WebGPU rendering path
- [ ] Display a basic railway scene
- [ ] Add camera and input controls

## Milestone 4 — Playable prototype
- [ ] Connect train movement and simulation
- [ ] Add essential controls and audio where browser APIs permit
- [ ] Test performance and compatibility on mobile browsers

## Engineering principles
- Keep the original C# language and reuse upstream code where technically and legally possible.
- Do not assume the desktop MonoGame/XNA renderer can compile unchanged to WebAssembly.
- Keep browser-specific code behind small adapter interfaces.
- Preserve Open Rails copyright and GPL-3.0 notices in any copied or modified upstream files.

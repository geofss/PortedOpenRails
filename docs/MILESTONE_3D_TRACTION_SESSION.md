# Milestone 3D — Browser traction and consist simulation sessions

## Scope

This milestone adds browser-independent simulation sessions that connect parsed locomotive traction metadata to the fixed-step longitudinal motion core.

- `LocomotiveTractionMetadataReader` parses the locomotive's `Engine` block, maximum/continuous force, power, maximum speed, unloading speed, and original ORTS throttle/speed force tables.
- `LocomotiveTractionModel` evaluates force curves, basic-force fallback, throttle response, speed/power cap, unloading-speed power decay, power reduction, and continuous-force limiting.
- `LocomotiveSimulationSession` connects one powered vehicle to the fixed-step clock and accepts caller-provided gravity, rolling friction, brake, curve, wind, and tunnel force inputs.
- `TrainConsistSimulationSession` applies per-car traction models to a multi-car state and accepts externally supplied signed coupler-force values.

## Current limits

This is a traction/simulation integration slice, not the Open Rails locomotive subsystem. It does not yet port locomotive-specific controllers, axle force ramping, brake-pipe and train brake dynamics, adhesion/wheel slip, electrical supply limits, coupler slack/break logic, route-node traversal, or live browser input/render synchronization. The multi-car session's force provider is currently called with each car's state, but coupler-force arrays are supplied as frame input and need a future coupler model.

## Validation

Regression tests cover traction metadata parsing, force-curve interpolation, throttle response, power caps, continuous-force limit, basic-force fallback, fixed-step session advancement, pause/reset, and applying supplied coupler forces to a trailer. GitHub Actions is the validation authority for WebAssembly publish and Pages deploy.

# Milestone 3D — Browser traction and consist simulation sessions

## Scope

This milestone adds browser-independent simulation sessions that connect parsed locomotive traction metadata to the fixed-step longitudinal motion core.

- `LocomotiveTractionMetadataReader` parses the locomotive's `Engine` block, maximum/continuous force, power, maximum speed, unloading speed, and original ORTS throttle/speed force tables.
- `LocomotiveTractionModel` evaluates force curves, basic-force fallback, throttle response, speed/power cap, unloading-speed power decay, power reduction, and continuous-force limiting.
- `LocomotiveSimulationSession` connects one powered vehicle to the fixed-step clock and accepts caller-provided gravity, rolling friction, brake, curve, wind, and tunnel force inputs.
- `TrainConsistSimulationSession` applies per-car traction models to a multi-car state and accepts externally supplied signed coupler-force values. It validates input array sizes and rejects non-finite throttle, power, average-force, and coupler inputs before stepping.
- `TrainConsistCouplerForceDistributor` computes algebraic internal boundary forces required for a target common acceleration given external force breakdowns, then expands them into per-car signed forces with equal/opposite action-reaction pairs. It rejects targets that cannot be achieved from the net external force and supports optional per-boundary compression/tension ratings.
- `TrainCouplerForceLimit` validates rated compression/tension magnitudes and rejects force-balance requests that exceed a boundary's supplied rating. This is static rating enforcement, not dynamic coupler behavior.
- `TrainConsistSimulationSession.AdvanceWithCommonAcceleration` calculates the per-car force frame and coupler force distribution on every fixed simulation tick, then advances all cars with the common target acceleration (when physically feasible from supplied external forces).
- `TrainCarAdhesionModel` caps requested tractive effort from vehicle mass, gravity, and a configurable per-car adhesion coefficient (default 0.25) in the browser consist brake-command path. This is a quasi-static ceiling, not wheel-slip detection, axle-load transfer, sanding, or a native Open Rails adhesion controller.
- `TrainCarBrakeModel` converts a finite 0–100% command and rated brake force to signed retarding force; commands are clamped and the force opposes travel direction. It is a basic force evaluator, not a pneumatic or friction-brake subsystem.
- `LocomotiveSimulationSession.AdvanceWithBrakeCommand` combines commanded braking with existing gravity/resistance inputs and locomotive traction in one fixed-step update.
- `TrainConsistSimulationSession.AdvanceWithBrakeCommands` applies individual brake commands and ratings to each car alongside traction and supplied coupler force inputs.
- `AdvanceWithCommonAcceleration` can also accept optional per-car brake commands and ratings, so braking forces participate in its common-acceleration force balance and coupler rating checks.

## Current limits

This is a traction/simulation integration slice, not the Open Rails locomotive subsystem. It does not yet port locomotive-specific controllers, axle force ramping, brake-pipe and train brake dynamics or fade (the standalone brake force evaluator is only a basic foundation), wheel-slip detection/control, axle-load transfer, sanding, electrical supply limits, coupler slack/break/compliance dynamics, route-node traversal, or live browser input/render synchronization. The coupler distributor is a static force balance helper, not a time-domain coupler simulation. The multi-car session's force provider is currently called with each car's state, but coupler-force arrays are supplied as frame input and need a future coupler model.

## Validation

Regression tests cover traction metadata parsing, force-curve interpolation, throttle response, power caps, continuous-force limit, basic-force fallback, brake-command clamping and direction, brake-force integration through the motion loop and single-/multi-car sessions, fixed-step session advancement, pause/reset, supplied coupler force on a trailer, automatic common-acceleration session integration, coupler-force distribution, equal/opposite action-reaction, infeasible acceleration rejection, force-rating enforcement, brake input-array and rating validation, optional brake-input pairing/size checks for common-acceleration mode, and invalid input rejection. GitHub Actions is the validation authority for WebAssembly publish and Pages deploy.

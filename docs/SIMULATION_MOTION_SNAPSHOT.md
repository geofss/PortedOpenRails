# Simulation motion snapshot API

The browser-port simulation now exposes in-memory snapshots for its fixed-step clock and multi-car longitudinal motion loop.

## API

- `FixedStepSimulationScheduler.CaptureState()` records simulation time, dropped time, the residual accumulator, and pause state.
- `FixedStepSimulationScheduler.RestoreState(snapshot)` validates finite non-negative times and requires the accumulator to be below one fixed step.
- `TrainCarSimulationLoop.CaptureState()` and `RestoreState(snapshot)` support validated single-car motion and clock snapshots.
- `TrainConsistSimulationLoop.CaptureState()` copies the per-car motion records and clock snapshot.
- `TrainConsistSimulationLoop.RestoreState(snapshot)` validates the restored consist and clock, and reapplies route-boundary clamping when a route-length constraint is configured.

## Regression coverage

Tests verify clock restoration of simulation time, dropped time, interpolation phase, pause state, and reset of per-frame step counters; malformed clock snapshots are rejected. Multi-car tests verify motion/clock restore and that a captured snapshot remains independent of subsequent motion.

## Not yet full simulator save/restore

This is an in-memory foundational API, not a user-facing save game. It does not serialize to local storage/files, and does not capture the browser driving controller, throttle/brake/reverser and door state, brake-pipe/cylinder/reservoir state, traction/controller state, route selection, activity/timetable, weather, AI traffic, or renderer/UI state. Persistence format, versioning, migration, UI save/load, and recovery UX remain future work.

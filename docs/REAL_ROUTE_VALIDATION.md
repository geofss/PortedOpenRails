# Real-route validation checklist

This checklist is for validating the browser route-import pipeline against a real route package that the tester is authorized to use. Keep the route package private/local; do not commit copyrighted route assets, extracted files, screenshots containing proprietary content, or route-specific secrets.

## Test setup

- [ ] Verify the imported ZIP SHA-256 matches the published Demo Model 1 asset digest (`5e4a64e1cd44e23ee7833230348b6b76c43fc555060b152f6c6547e832a5189b`) before import, if checksum tooling is available.
- [x] Record route/package name, source, permission basis (Demo Model 1 / Scottish Capital Express / SCE; published release and permission documented)
- [x] Verify published package integrity and record SHA-256, archive CRC, route/activity file inventory (see package audit)
- [x] Inspect route/activity metadata and confirm route file, TDB, activity, WORLD tiles and rolling-stock package families are present (see package audit).
- [x] Include the required attribution from [Demo Model 1 attribution and permission](DEMO_MODEL_1_ATTRIBUTION.md) in project documentation, package audit, and published package provenance.
- [ ] Import the route ZIP through the browser's user-file picker.
- [ ] Record import warnings, rejected entries, size-limit outcomes, and package provenance.
- [ ] Select the route's parsed `.trk` metadata and confirm the UI reports the expected route identity.

## Import size compatibility

The published Demo Model 1 archive is 272,422,379 bytes, which exceeds the previous 256 MiB browser upload cap. The browser ZIP loader default was raised to 300 MiB so the authorized archive is not rejected solely by its compressed size. This still requires validation on supported devices: copying the ZIP into WebAssembly memory and indexing/decompressing it can be memory-intensive, and the virtual ZIP store retains separate uncompressed-entry limits.

## Parsing and virtual-content checks

- [ ] Confirm route/global metadata files resolve through virtual content paths (case-insensitive where intended). (The dedicated CI acceptance test loads the real route through the production virtual-content reader; full browser ZIP import remains unverified.)
- [ ] Confirm nested STF includes resolve relative to their parent and reject traversal outside the virtual root.
- [x] Confirm real-route TDB nodes, connected vector paths, and positive track length load without fatal errors in the dedicated CI acceptance test. (Detailed pin/junction diagnostics still need manual review.)
- [x] Confirm the real route's route-specific and global TSECTION catalogs are present and consumed by the production route loader in the dedicated CI acceptance test.
- [x] Confirm real-route track-shape assets are discovered and at least one track-shape mesh is loaded in the dedicated CI acceptance test. (Full material/LOD visual parity remains unverified.)
- [x] Confirm real-route terrain tiles are discovered and terrain meshes are generated in the dedicated CI acceptance test. (Visual preview and every tile/LOD remain unverified.)
- [x] Confirm real-route WORLD tiles are discovered and scenery tiles are parsed in the dedicated CI acceptance test. (Malformed-neighbor recovery and visual placement remain unverified.)
- [ ] Confirm missing/unsupported assets are reported with virtual path and package provenance.

## Automated real-route CI evidence\n\n- [x] Dedicated GitHub Actions acceptance job downloaded the authorized Demo Model 1 source, restored dependencies, and loaded the real Scottish Capital Express route through `RouteLoader` successfully on 2026-11-27.\n- [x] Route acceptance assertions passed for SCE metadata, track database size, connected vector paths, positive track length, terrain tile discovery, track-shape discovery, WORLD tile discovery, generated terrain meshes, and loaded WORLD scenery.\n- [x] Separate route regression job passed route parsing, geometry, terrain, and scenery tests.\n- [ ] Capture and review the acceptance job's numeric diagnostics (node/path/length/terrain/shape/WORLD counts and validation issue count) before treating the route diagnostics audit as complete.\n\nThe automated result validates the production route loader against the real authorized route package in CI. It does **not** yet validate importing the ZIP through the browser picker, rendering the route in an actual browser, placing the real consist, or driving a train end-to-end.\n\n## Preview checks

- [ ] Terrain patches render within the documented preview cap.
- [ ] Track overlays follow the parsed route topology and stay aligned with terrain.
- [ ] Original track-shape meshes render with expected LOD/texture batch diagnostics.
- [ ] WORLD scenery placement transforms are visually plausible; inspect matrix/quaternion rotation and tile-local texture mapping.
- [ ] Camera orbit, tilt, zoom, wheel, and touch gestures work on the tested device.
- [ ] Resize/redraw does not accumulate WebGL buffers; removing the preview releases resources.

## Evidence and report

Record results in a route-validation report without redistributing the route:

- Browser, version, OS/device, and date.
- Package size and import duration (avoid retaining private package details).
- Parsed counts: track nodes, vector sections, connected paths, shapes, terrain tiles, WORLD tiles, and scenery placements.
- Warning/error counts grouped by subsystem; list virtual paths only if permitted.
- Screenshots or short captures only when the route license permits sharing and they contain no restricted material.
- Issues found, reproducible steps, and whether they are parser, asset-resolution, geometry, renderer, performance, or device-specific.

A route passes this validation only when the import, parse, asset-resolution, and preview checks are completed and the results are recorded. This is route-content validation, not proof of a playable simulator or full browser port.

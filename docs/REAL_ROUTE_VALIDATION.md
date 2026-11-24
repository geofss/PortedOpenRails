# Real-route validation checklist

This checklist is for validating the browser route-import pipeline against a real route package that the tester is authorized to use. Keep the route package private/local; do not commit copyrighted route assets, extracted files, screenshots containing proprietary content, or route-specific secrets.

## Test setup

- [ ] Verify the imported ZIP SHA-256 matches the published Demo Model 1 asset digest (`5e4a64e1cd44e23ee7833230348b6b76c43fc555060b152f6c6547e832a5189b`) before import, if checksum tooling is available.
- [x] Record route/package name, source, permission basis (Demo Model 1 / Scottish Capital Express / SCE; published release and permission documented)
- [x] Verify published package integrity and record SHA-256, archive CRC, route/activity file inventory (see package audit)
- [x] Inspect route/activity metadata and confirm route file, TDB, activity, WORLD tiles and rolling-stock package families are present (see package audit).
- [ ] Include the required attribution from [Demo Model 1 attribution and permission](DEMO_MODEL_1_ATTRIBUTION.md) wherever the package is included or distributed.
- [ ] Import the route ZIP through the browser's user-file picker.
- [ ] Record import warnings, rejected entries, size-limit outcomes, and package provenance.
- [ ] Select the route's parsed `.trk` metadata and confirm the UI reports the expected route identity.

## Parsing and virtual-content checks

- [ ] Confirm route/global metadata files resolve through virtual content paths (case-insensitive where intended).
- [ ] Confirm nested STF includes resolve relative to their parent and reject traversal outside the virtual root.
- [ ] Confirm TDB nodes, vector sections, pins, junction metadata, and connected path topology parse without fatal errors.
- [ ] Confirm global and route-specific TSECTION catalogs resolve section lengths and track-shape references.
- [ ] Confirm referenced track-shape `.s` assets are discovered and parse into geometry/LOD data.
- [ ] Confirm terrain tile/sample/patch metadata is discovered and parseable.
- [ ] Confirm WORLD scenery files are discovered; malformed files produce diagnostics without discarding valid neighboring tiles.
- [ ] Confirm missing/unsupported assets are reported with virtual path and package provenance.

## Preview checks

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

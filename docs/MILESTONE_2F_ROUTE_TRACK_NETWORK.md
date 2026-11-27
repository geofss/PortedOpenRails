# Milestone 2F — Route track network and terrain data pipeline

**Status:** Browser-safe route data pipeline and structural validation report implemented. Dedicated CI acceptance against the authorized Demo Model 1 package passed on 2026-11-27; full browser ZIP-picker import, visual review, and playable-session validation remain pending.

## What is implemented

The route loader now joins the original MSTS/Open Rails data layers:

1. Read identifying route metadata from the selected `.trk` file, including its `FileName` field.
2. Locate the matching route `.tdb`, global `GLOBAL/TSECTION.DAT`, and optional route-specific `TSECTION.DAT` in the layered virtual content namespace.
3. Parse the TDB's `TrackNodes`, `TrVectorNode` sections, `TrJunctionNode` metadata, `UiD` positions, and `TrPins` connections.
4. Resolve each vector section against route-specific geometry first and global geometry second.
5. Parse the global TSECTION `TrackShapes` catalog, including original `.s` filenames, path section indices, offsets, and shape flags.
6. Build connected vector paths, preserve their endpoint node references, resolve each vector section to its original track shape, and calculate track lengths.
7. Sample straight and curved centerlines in world coordinates using the Open Rails heading convention: `AY` is radians measured right of North.
8. Discover the MSTS terrain tile files touched by the track network using the upstream `TileName.FromTileXZ` encoding.
9. Resolve original track shape `.s` assets from route `SHAPES` before global `GLOBAL/SHAPES` content.
10. Discover original `WORLD/W±xxxxxx±xxxxxx.w` tiles touched by the track network.
11. Parse world-tile `Static`, `CollideObject`, and `TrackObj` placements, including their UID, shape filename, position, quaternion, matrix, flags, and junction coordinates.
12. Load WORLD tiles on demand and resolve their static/track-object shape filenames against route and global `SHAPES` content.
13. Convert tile-relative static and track-object positions into the global route coordinate system while preserving original orientation matrices and junction positions.
14. Assemble a WORLD scenery tile on demand, loading its original `.s` meshes, resolving shape textures, decoding ACE textures, and isolating individual asset failures.
15. Read SIMISA terrain sample buffers, water offsets, and patch metadata through the stream-based SBR reader.
16. Resolve elevation and hidden-vertex samples across adjacent terrain tiles using the upstream TileManager coordinate conversion.
17. Parse original terrain shader definitions, texture slots, and UV calculations from each `.t` tile.
18. Build 17×17 terrain patch mesh data with original UV mapping, normals, alternating triangle winding, and hidden-triangle removal.
19. Preserve original terrain material selection and locate route `TerrTex` textures, seasonal variants, and implicit `microtex.ace` overlays.
20. Decode ACE textures into RGBA mip data or preserve raw DXT/BGR GPU payloads, including compressed SIMISA streams and one-bit alpha masks.
21. Resolve original shape primitive image references against route `Textures` and seasonal folders.

The browser UI now calls the shared `RouteLoader` service when a user selects an imported route. It reports TDB node count, connected vector path count, total parsed track length, terrain tile discovery results, and a structural `RouteValidationReport` aggregating track-network diagnostics and missing/failed terrain, track-shape, and WORLD assets. The viewer displays this report with a bounded issue list. The dedicated real-route acceptance workflow now exercises the production RouteLoader against the authorized Demo Model 1 package. This validates route parsing and selected asset/geometry pipelines in CI; it does not validate browser ZIP-picker import or visual parity.

## Upstream behavior references

- `Source/Orts.Formats.Msts/TrackDatabaseFile.cs`: TDB node, vector-section, junction, UID, and pin structure.
- `Source/Orts.Formats.Msts/TrackSectionsFile.cs`: global and route-specific section geometry and units.
- `Source/Contrib/TrackViewer/Drawing/DrawTrackDB.cs`: each vector section is drawn from its stored location, section index, and `AY` heading.
- `Source/Contrib/TrackViewer/Drawing/DrawArea.cs`: straight and curved drawing coordinate convention.
- `Source/Orts.Formats.Msts/TerrainFile.cs`: terrain samples, patch sets, and tile-name encoding.
- `Source/Orts.Parsers.Msts/SBR.cs`: SIMISA structured-block parsing.

The browser SBR adaptation adds a stream overload so files can be read directly from the virtual ZIP content store. The original parser behavior is retained; the XNA vector type is replaced with `System.Numerics.Vector3` in this parser-only source.

## Validation

Regression tests cover:

- TDB node and vector-section parsing, including junction metadata and pin references.
- Global TrackShapes parsing and resolution of original shape filenames and path section references.
- Route-first/global-fallback shape asset discovery and safe virtual path validation.
- Original `.s` shape parser reading from a ZIP-backed stream, with point and UV regression coverage.
- WORLD tile discovery and binary world-object parsing for static scenery and track objects.
- Lazy per-tile loading and route/global resolution of scenery shape assets.
- Tile-relative scenery and junction positions converted into global route coordinates; quaternion and matrix data remain intact.
- On-demand world scenery tile assembly connects placements, original shape meshes, route texture references, and ACE decoding with per-asset failure isolation.
- Portable track-shape mesh conversion, including a triangle fixture that verifies transformed positions, UVs, indices, shader/image references, and matrix hierarchy.
- Missing pin diagnostics.
- Route-specific section overrides and global fallback.
- World-coordinate conversion using the MSTS 2048-metre tile size.
- Positive and negative curve tessellation.
- Terrain tile name encoding and route tile discovery, including the required `_y.raw` altitude file and optional `_f.raw` flags file.
- Upstream-compatible adjacent-tile sample resolution and original terrain patch mesh generation, with regression coverage for tile edges, normals, UVs, and hidden triangles.
- Terrain shader, texture-slot, and UV calculation parsing; original patch material/overlay selection; default and seasonal TerrTex asset discovery.
- ACE texture decoding for structured RGBA channels, one-bit masks, compressed SIMISA streams, and raw DXT formats.
- Original shape primitive texture discovery in route Textures and seasonal folders.
- Little-endian altitude grid ordering and vertex-hidden flag decoding.
- Reading a binary SIMISA terrain tile from a ZIP-backed virtual stream.
- An end-to-end fixture loading `.trk`, `.tdb`, `TSECTION.DAT`, track network, and terrain tile references.

The tests use synthetic fixtures. They do not yet establish compatibility with every real-world route variant. CI verifies the test project and WebAssembly publish.

## Deliberately not claimed as complete

- TDB `TrItemTable` payloads such as signals, platforms, sidings, speed posts, and sound regions are not yet parsed. The route-acceptance job's signal-circuit topology assertion does not imply complete native signal-position circuits.
- Road database (`.rdb`) support is not included.
- Terrain patch mesh generation and texture/material asset loading are implemented as data pipelines; browser GPU rendering, full route tile coverage/LOD selection, and visual parity remain incomplete.
- Original `.s` shape structures are parsed and converted to portable mesh data, and WORLD scenery assembly resolves meshes and texture assets. Full browser GPU submission/rendering, forests, signals, and complete building/scenery parity remain incomplete.
- The route network, terrain patches, track-shape meshes, and WORLD scenery are loaded as browser-side data; a complete, visually faithful 3D route renderer is still pending.
- No route asset is bundled or redistributed as part of this milestone.

## Next route-building work

1. Validate the parser against a real route package whose redistribution and use permissions are clear.
2. Port the remaining terrain patch/sample semantics and generate terrain meshes matching Open Rails.
3. Load route world files and original shape/texture assets through the virtual content layer.
4. Port the original Open Rails rendering pipeline to a browser graphics backend without redesigning the Open Rails UI.
5. Connect the rendered track and terrain to the simulation runtime.

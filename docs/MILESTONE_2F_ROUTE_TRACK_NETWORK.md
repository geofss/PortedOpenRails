# Milestone 2F — Route track network and terrain data pipeline

**Status:** Initial browser-safe route data pipeline implemented; fixture-validated in CI. Real-route validation and rendering remain pending.

## What is implemented

The route loader now joins the original MSTS/Open Rails data layers:

1. Read identifying route metadata from the selected `.trk` file, including its `FileName` field.
2. Locate the matching route `.tdb`, global `GLOBAL/TSECTION.DAT`, and optional route-specific `TSECTION.DAT` in the layered virtual content namespace.
3. Parse the TDB's `TrackNodes`, `TrVectorNode` sections, `TrJunctionNode` metadata, `UiD` positions, and `TrPins` connections.
4. Resolve each vector section against route-specific geometry first and global geometry second.
5. Build connected vector paths, preserve their endpoint node references, and calculate their track lengths.
6. Sample straight and curved centerlines in world coordinates using the Open Rails heading convention: `AY` is radians measured right of North.
7. Discover the MSTS terrain tile files touched by the track network using the upstream `TileName.FromTileXZ` encoding.
8. Read SIMISA terrain sample buffers, water offsets, and patch metadata through the stream-based SBR reader.

The browser UI now calls the shared `RouteLoader` service when a user selects an imported route. It reports TDB node count, connected vector path count, total parsed track length, and terrain tile discovery results.

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
- Missing pin diagnostics.
- Route-specific section overrides and global fallback.
- World-coordinate conversion using the MSTS 2048-metre tile size.
- Positive and negative curve tessellation.
- Terrain tile name encoding and route tile discovery.
- Reading a binary SIMISA terrain tile from a ZIP-backed virtual stream.
- An end-to-end fixture loading `.trk`, `.tdb`, `TSECTION.DAT`, track network, and terrain tile references.

The tests use synthetic fixtures. They do not yet establish compatibility with every real-world route variant. CI verifies the test project and WebAssembly publish.

## Deliberately not claimed as complete

- TDB `TrItemTable` payloads such as signals, platforms, sidings, speed posts, and sound regions are not yet parsed.
- Road database (`.rdb`) support is not included.
- Terrain mesh generation, patch LOD selection, and terrain texture/material loading are not yet implemented.
- World-file scenery, shape (`.s`) geometry, ACE textures, forests, signals, and buildings are not yet rendered.
- The route network is data and centerline geometry, not yet a fully rendered 3D route.
- No route asset is bundled or redistributed as part of this milestone.

## Next route-building work

1. Validate the parser against a real route package whose redistribution and use permissions are clear.
2. Port the remaining terrain patch/sample semantics and generate terrain meshes matching Open Rails.
3. Load route world files and original shape/texture assets through the virtual content layer.
4. Port the original Open Rails rendering pipeline to a browser graphics backend without redesigning the Open Rails UI.
5. Connect the rendered track and terrain to the simulation runtime.

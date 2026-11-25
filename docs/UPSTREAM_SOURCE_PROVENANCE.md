# Existing Open Rails source provenance

**Audited against upstream revision:** `dfb5246ca9da46e352cfcd2a365d284ac55c1a1f`  
**Upstream:** https://github.com/openrails/openrails

This inventory prevents us from treating every current parser as newly invented or assuming every similarly named file is already an unmodified upstream port.

## Exact filename matches found

The following files under `src/OpenRails.Web/Parsing` have same-named source files in upstream `Source/Orts.Parsers.Msts`:

- `Interpolator.cs`
- `SBR.cs`
- `STFReader.cs`
- `ShapeFile.cs`
- `TokenID.cs`

A byte-for-byte comparison at the audited revision found:

- `TokenID.cs`: exact match.
- `STFReader.cs`: not an exact match; the browser copy is substantially shorter (about 85 KB versus about 178 KB upstream).
- `SBR.cs`: not an exact match.
- `Interpolator.cs` and `ShapeFile.cs`: same-named upstream sources exist; exact content equivalence has not yet been established.

This means the repository already contains upstream-derived MSTS parser code. The immediate task is not to blindly import a second copy. It is to identify the exact upstream baseline for each existing file, review local changes and omissions, and restore upstream behaviour where browser compatibility permits.

## Browser-specific layer

Files such as `RouteMetadataReader.cs`, `TrackDatabaseReader.cs`, `TrackSectionGeometryReader.cs`, `TerrainTileReader.cs`, `WorldTileReader.cs`, `TrackShapeFileReader.cs`, and the route/terrain/scenery asset loaders are browser-port integration or adapter code in this repository; they do not have same-named source files in the upstream MSTS parser project at the audited revision.

These adapters should be migrated to call upstream format readers and data models where equivalent upstream functionality exists. Avoid retaining duplicate parsers for the same file format once parity is proven.

## Next source-level work

1. Diff the existing parser files against the pinned upstream revision and record the exact local adaptations.
2. Inspect the upstream parser project's complete dependency closure, not just its project file. Separate pure parsing from MonoGame and Windows-specific APIs.
3. Identify the smallest upstream source subset that can compile under the browser app's .NET 10 target.
4. Port upstream route/terrain/shape data models and readers behind the existing browser virtual-content abstraction.
5. Add fixture tests that compare browser adapter output with upstream parser output for the same real MSTS files.
6. Only remove an adapter after equivalent route fixtures pass.

## Licensing

Keep each upstream file's original copyright and GPL-3.0-or-later notices intact. Record local modifications clearly. See [Open Rails upstream](https://github.com/openrails/openrails) and the repository's [upstream port audit](UPSTREAM_PORT_AUDIT.md).

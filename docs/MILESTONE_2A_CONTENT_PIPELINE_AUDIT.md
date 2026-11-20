# Milestone 2A — Content pipeline dependency audit

**Status:** Initial parser/format/IO audit complete; implementation design recorded  
**Target:** First real route-data loading vertical slice  
**Upstream:** Open Rails `master`, GPL-3.0-or-later

## Findings from the source

The upstream MSTS content path is not just a collection of text files. It includes structured text (STF), SIMIS signatures, binary data, include files, compressed data, virtual paths, and cross-package references. The browser port must preserve these semantics.

### 1. Parser entry point: `Orts.Parsers.Msts/STFReader.cs`

Positive finding:
- `STFReader` already has a constructor that accepts a `Stream`, a diagnostic filename, and an `Encoding`. This is a useful seam for browser-provided content.
- The parser exposes `IStreamReaderFactory`, whose purpose is to map a logical source name to a `StreamReader`; its documentation explicitly mentions alternative sources and test mocks.
- The parser's include preprocessor already routes included files through that factory.

Browser blockers:
- The filename constructor directly calls `Directory.Exists` and opens a `StreamReader` from a physical path.
- The default `StreamReaderFactoryFromFile` uses `Path`, `Directory.Exists`, and a physical file-backed `StreamReader`.
- Include path resolution uses Windows-style separators and rooted-path assumptions. A virtual content provider must normalize logical paths and resolve includes within the selected package/VFS.
- `STFReader.cs` and `SBR.cs` import `Microsoft.Xna.Framework`, so parser projects currently carry a MonoGame type dependency even though much of their parsing work is stream-oriented.

**Implication:** Keep the original parser logic where possible, but replace or isolate physical-file resolution and remove graphics-framework dependencies from parser code only after all usages are mapped. The stream constructor alone does not solve nested include resolution.

### 2. Binary parsing: `Orts.Parsers.Msts/SBR.cs`

- Uses `System.IO` and `System.IO.Compression`, including a physical `FileStream` constructor for filename-based loading and a `BinaryReader` for stream parsing.
- The browser route is to supply streams from the content provider and verify the required compression formats are available in the WebAssembly runtime.
- Keep the binary parsing behavior and endianness rules; do not replace it with ad-hoc browser-side parsing.

### 3. Real route and track formats

Inspected upstream format files include:
- `RouteFile.cs`
- `TrackDatabaseFile.cs`
- `TrackSectionsFile.cs`
- `TerrainFile.cs`

These use the MSTS parser and common types. `TrackDatabaseFile.cs` and `TrackSectionsFile.cs` directly import `Microsoft.Xna.Framework`, so their framework dependency must be traced before they can be compiled into a browser-safe format assembly. The route file itself has fewer direct framework imports, but depends on common/format models.

### 4. Content path discovery: `Orts.Content/MSTSPath.cs`

This class contains desktop-specific discovery:
- Windows Registry lookups for the installed MSTS location.
- Physical directory checks and file enumeration.
- Windows-style path composition.
- Train and global sound path resolution.

A browser cannot discover a local MSTS installation via the Windows Registry. Replace installation discovery with explicit user selection/import and resolve files through a virtual filesystem. Keep MSTS logical path conventions at the compatibility boundary.

### 5. IO helpers

The inspected `Orts.IO` files are small stream/encoding helpers:
- `BufferedInMemoryStream.cs`
- `ByteEncoding.cs`

They are promising candidates for direct reuse, subject to compile and test validation. The `Orts.IO` project itself has no package references in its project file.

### 6. Project-level blockers

- `Orts.Parsers.Msts`, `Orts.Formats.Msts`, `Orts.Common`, `Orts.Content`, and `Orts.Simulation` currently target `net6-windows`.
- The parser and MSTS format projects reference `MonoGame.Framework.WindowsDX`.
- `Orts.Content` references `Microsoft.Win32.Registry`.
- The simulation project references desktop-oriented dependencies including WindowsDX MonoGame and Roslyn packages.
- The current web host targets .NET 10 WebAssembly. A safe port should create browser-targeted assemblies from selected source files or refactored shared projects, rather than retargeting every upstream project in one risky change.

## Proposed first vertical slice

**Goal:** Load a user-selected, supported MSTS route package in the browser and parse its route metadata and track database into real C# model objects. The first milestone is data loading and validation, not 3D rendering.

1. Define a browser-independent virtual content interface (logical path lookup and stream opening).
2. Implement a browser adapter that indexes a user-selected package and provides read-only streams.
3. Add path normalization and package-aware include resolution.
4. Adapt the STF reader factory so includes resolve through the virtual content interface rather than physical directories.
5. Adapt binary reader entry points to accept streams.
6. Port the smallest dependency-clean route metadata parser.
7. Add fixtures/tests for STF signatures, nested includes, path normalization, binary reads, missing-file diagnostics, and route metadata.
8. Only then connect track database parsing, after resolving the required vector/math types.

## Important design constraints

- Never assume the browser can read arbitrary local paths. The user must explicitly select/import content.
- Do not upload route packages to a server; keep content local to the browser unless the user explicitly chooses otherwise.
- Preserve MSTS/OR package semantics, path references, and override order.
- Avoid copying entire desktop projects into the WebAssembly host.
- Keep parser/core assemblies free of Blazor component references and browser JavaScript calls.
- Treat route files and third-party content as untrusted input: validate archive paths, cap resource use, and report malformed files cleanly.
- Preserve upstream copyright and GPL-3.0-or-later notices on all copied or modified source.

## Next implementation checkpoint

Prototype a small browser-safe virtual filesystem and test it with the existing stream-based STF parser seam. Before importing the parser, finish mapping every `Microsoft.Xna.Framework` usage in the parser project and identify the minimum portable math types needed. This prevents a seemingly simple parser import from dragging the WindowsDX graphics stack into WebAssembly.

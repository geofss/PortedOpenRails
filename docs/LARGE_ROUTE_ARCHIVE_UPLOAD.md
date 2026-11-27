# Large route archive upload

## Current implementation

Desktop browser uploads larger than the in-memory ZIP parser limit are streamed from the Blazor
`IBrowserFile` source through `DotNetStreamReference` into the browser Origin Private File System
(OPFS). The upload path does not copy the complete compressed archive into a .NET `MemoryStream`
or a JavaScript `ArrayBuffer`.

- Hard archive ceiling: 30,000,000,000 bytes (30 GB decimal).
- Files at or below the existing 300 MiB in-memory parser threshold continue through the current
  ZIP route-import path.
- Larger files are stored as opaque ZIP archives under the OPFS directory
  `portedopenrails-packages`.
- Writes are chunked, progress is reported, and incomplete uploads are removed.
- The browser storage quota is checked before writing, with a safety margin.
- Persistent-storage permission is requested where supported and its result is reported.
- Completed staged archives are listed again after reload and can be deleted from the UI.
- A browser may still deny storage, evict non-persistent data, or have insufficient quota.
  The 30 GB value is a maximum accepted archive size, not a guarantee that every device has
  30 GB available.

## Current staged-route integration

For archives above 300 MiB, the app now:

1. Streams the compressed ZIP into OPFS without buffering the whole archive.
2. Parses the ZIP central directory, including ZIP64 metadata, and persists an entry index.
3. Opens individual entries from OPFS as streams, verifies declared size and CRC-32, and parses
   each route `.trk` metadata file with the upstream STF reader.
4. Offers a **Load route into simulator** action. The current synchronous route pipeline is fed by
   a bounded compatibility store populated entry-by-entry from OPFS.

The compatibility store currently has a **2 GiB aggregate expanded-content ceiling** and a
**512 MiB per-entry ceiling**. It materializes expanded files in WebAssembly memory before invoking
the existing synchronous route readers. This is an integration bridge, not a genuinely lazy,
disk-backed runtime content store. Large archives whose expanded size exceeds the ceiling can be
uploaded and indexed, but cannot yet be loaded through the route pipeline.

## Explicit remaining work

- Replace the bounded in-memory compatibility bridge with async/lazy content access across route,
  track, terrain, WORLD scenery, shape, texture, activity, consist, and rolling-stock readers.
- Validate ZIP64, decompression, CRC checks, quota behavior, cancellation, and recovery in desktop
  browsers with multi-GB archives.
- Run the authorized Demo Model 1 archive through the browser file picker and record the actual
  route-load validation report. Automated fixtures and CI do not substitute for this test.

## Validation

The .NET tests assert the 30 GB configured ceiling, preserve the separate 300 MiB in-memory ZIP
threshold, and exercise route parser/asset-pipeline fixtures. CI validates compilation, JavaScript
syntax, and route regression tests. It does not prove that Demo Model 1 or a multi-GB archive has
been loaded successfully in a real browser.

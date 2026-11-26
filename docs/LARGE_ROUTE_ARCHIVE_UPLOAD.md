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
- A browser may still deny storage, evict non-persistent data, or have insufficient quota.
  The 30 GB value is a maximum accepted archive size, not a guarantee that every device has
  30 GB available.

## Explicit remaining work

Large archives are **staged only** at this point. The current synchronous `IVirtualContentStore`
and `ZipArchive` parser still expect a seekable in-process stream and are not connected to OPFS.
The next required step is an asynchronous, disk-backed ZIP index and lazy entry reader, followed by
async route metadata, terrain, scenery, and rolling-stock asset access. A successfully staged large
ZIP is not yet a loaded or playable route.

## Validation

The .NET tests assert the 30 GB configured ceiling and keep the existing in-memory parser limit
separate. CI validates compilation and route regression tests; real-browser uploads at multi-GB
sizes still require desktop browser integration testing with sufficient OPFS quota.

# Milestone 2C — Browser ZIP package import

**Status:** Browser file-picker integration committed; CI validation pending.

## Goal

Allow a user to select a ZIP package from the browser and index its contents in the existing virtual content store without uploading it to a server or relying on a desktop installation path.

## Implemented

- Added `BrowserZipPackageLoader`, which accepts a Blazor `IBrowserFile`, enforces a compressed archive size limit, and copies the selected file into a bounded in-memory stream.
- The loader hands the stream to `ZipContentStore`, retaining the existing ZIP entry count, path, duplicate, per-entry, and total uncompressed-size validation.
- The home page now provides a ZIP file picker, displays import errors, and lists up to 30 indexed logical paths.
- The current package is disposed when replaced or when the page is disposed.
- The page explicitly tells users that the package remains in browser memory and is not uploaded to a server.

## Limits and caveats

- Maximum compressed upload size is 256 MiB by default. This is a memory-backed prototype and can use substantial device memory; it is not yet suitable for very large route libraries or low-memory devices.
- This step indexes package entries only. It does not yet resolve multiple packages, parse route files, or persist imported content after the page is closed.
- ZIP files are treated as untrusted input and validated by the content store before paths are exposed.
- Browser file access is limited to files explicitly selected by the user.

## Next steps

1. Add a package collection with explicit package roles and deterministic overlay precedence.
2. Add package-aware include lookup for STF files.
3. Adapt the upstream STFReader stream/factory seam and test nested includes.
4. Adapt SBR binary parsing to consume virtual streams.
5. Load and parse a small real route metadata fixture.

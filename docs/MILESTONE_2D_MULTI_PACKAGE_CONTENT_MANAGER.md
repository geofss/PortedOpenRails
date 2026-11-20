# Milestone 2D — Multi-package content manager

**Status:** Initial composite-store and browser package-stack implementation committed; CI validation pending.

## Goal

Expose multiple ZIP-backed MSTS/Open Rails content packages through one virtual namespace while preserving deterministic file precedence.

## Implemented

- `CompositeContentStore` combines package layers into one case-insensitive path namespace.
- Layers are ordered highest to lowest precedence; the first package containing a path wins.
- Unique files fall through to lower-priority packages.
- `GetSourcePackage` identifies which layer supplies a resolved file.
- Duplicate package layer names are rejected to avoid ambiguous diagnostics.
- The composite owns its package stores and disposes them together.
- The browser page accepts multiple ZIP files, places the first selected package at highest priority, displays package order, and shows the winning source for each visible path.

## Current precedence behavior

The importer uses selection order as precedence: the first file selected has highest priority. Additional imports are placed above existing layers. This is an explicit prototype rule, not yet a complete implementation of every MSTS/Open Rails content search rule. The next parser integration must verify actual route/global/train lookup behavior against upstream.

## Safety and limits

Each ZIP still passes the existing path traversal, duplicate-entry, entry-count, per-entry, and total-uncompressed-size checks. Browser file access is explicit and local to the page. The current importer accepts at most 20 files in one selection and each archive has a 256 MiB compressed-size limit.

## Next steps

1. Add user controls to reorder or remove package layers.
2. Confirm package precedence against Open Rails' real content loading behavior.
3. Adapt STFReader's `IStreamReaderFactory` to resolve includes through `CompositeContentStore`.
4. Test nested includes that cross package boundaries.
5. Feed a real route metadata file into the upstream parser.

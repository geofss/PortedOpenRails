# Milestone 2B — Browser virtual content prototype

**Status:** First implementation slice committed; CI validation pending.

## Goal

Create a browser-independent, read-only content namespace that can expose Open Rails/MSTS files from user-provided packages without depending on Windows drive letters or physical installation paths.

## Implemented

- `IVirtualContentStore` defines logical path listing, existence checks, and read-only stream access.
- `VirtualPath.Normalize` converts Windows separators to portable separators and rejects rooted, drive-qualified, and traversal paths.
- `VirtualPath.ResolveRelative` resolves STF-style relative includes while preventing escape above the virtual content root.
- `ZipContentStore` indexes ZIP entries case-insensitively and opens entry streams on demand.
- ZIP validation rejects unsafe paths, duplicate logical paths, oversized individual entries, oversized total uncompressed content, and excessive entry counts.
- Tests cover path normalization, include resolution, traversal rejection, case-insensitive lookup, stream reads, missing files, and package size limits.

## Deliberate boundaries

This is not yet connected to a browser file picker, persistent storage, a package overlay system, or the upstream STF/SBR readers. The current ZIP store owns the supplied stream and must be disposed when the package is no longer needed. It is a first package adapter, not the final multi-package content manager.

The ZIP safety limits are conservative defaults and should be configurable by the eventual import UI. The implementation indexes metadata and opens entry streams lazily; it does not eagerly extract the whole archive into memory.

## Next steps

1. Add a Blazor browser-file adapter that copies a user-selected package into a bounded stream.
2. Define package precedence and overlay semantics for route-local, global, and train content.
3. Adapt the existing `STFReader` stream constructor and `IStreamReaderFactory` to use virtual paths for includes.
4. Adapt binary `SBR` parsing to accept streams without physical filename assumptions.
5. Add real route metadata fixtures and compare parser output with desktop Open Rails.

## Upstream compatibility notes

The upstream Open Rails project is GPL-3.0-or-later. When upstream source files are copied or modified in later steps, preserve their copyright and license notices and keep the repository's licensing obligations clear. This prototype is newly authored infrastructure and does not copy upstream parser implementation.

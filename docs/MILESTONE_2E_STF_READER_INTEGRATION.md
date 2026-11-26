# Milestone 2E — STFReader integration

**Goal:** Integrate the upstream Open Rails Structured Text Format (STF) reader with the browser virtual content namespace without replacing its parsing behavior.

## Upstream source inspected

- Repository: https://github.com/openrails/openrails
- Source file: `Source/Orts.Parsers.Msts/STFReader.cs`
- Inspected upstream revision: `4b5b34e9a53bf03350c857ccfbe69f390ccdbcf2`
- License: GPL-3.0-or-later; retain the complete upstream copyright and license header in any copied or modified source.

## Findings

1. The newer reader implementation exposes `Orts.Parsers.Msts.IStreamReaderFactory`, whose `GetStreamReader(string name, out string simisSignature)` method is an appropriate stream-opening seam.
2. The factory returns a `StreamReader`; the parser expects the SIMIS signature to be read by the factory and the reader to be positioned after that first line when a signature exists.
3. The upstream include preprocessor currently builds include names by concatenating a `directoryName` prefix and the include token. It only initializes that prefix when `Path.IsPathRooted(fileName)` is true. A logical path such as `ROUTES/Example/route.tdb` is not a rooted host path, so the stock behavior would resolve nested includes incorrectly (typically from the virtual root).
4. The parser source imports `Microsoft.Xna.Framework` and uses XNA/MonoGame value types, including `Vector2`, `Vector3`, and `Color`. The parser project also references `MonoGame.Framework.WindowsDX`, which must not be carried into the browser target unchanged.
5. The legacy and newer reader implementations are separated by the `NEW_READER` conditional compilation symbol. Integration must deliberately select and test the intended implementation; do not accidentally compile both.

## Integration design

- Keep `IVirtualContentStore` as the browser-independent content boundary.
- Add a virtual-path-aware stream reader factory that opens a file from `IVirtualContentStore`, reads the optional SIMIS signature, and returns a BOM-aware `StreamReader`.
- Modify/adapt the upstream include-resolution seam so every include is resolved relative to the *including virtual file* using `VirtualPath.ResolveRelative`. Do not use `Path.IsPathRooted`, `Path.GetDirectoryName`, or host path concatenation for virtual names.
- Preserve source names as normalized virtual paths for parser diagnostics.
- Isolate XNA math/color types behind a small compatibility layer or a browser-compatible dependency. Do not add the WindowsDX package to the WebAssembly project.
- Preserve upstream GPL notices and record any local modifications.

## Required tests before declaring parser integration complete

- UTF-8/ASCII STF input with and without a SIMIS signature.
- UTF-16 BOM input.
- Missing virtual file produces a useful not-found error.
- Relative include resolves against the including file's directory.
- Nested includes resolve relative to each nested file, not the original root file.
- Include traversal outside the virtual root is rejected.
- Include streams are disposed when parsing completes or fails.
- Parser token behavior is compared with upstream fixtures.

## Status

The upstream `NEW_READER` implementation has been extracted into `src/OpenRails.Web/Parsing/STFReader.cs`, retaining its GPL notice. Its desktop vector dependency was replaced with `System.Numerics` vector types, and the include preprocessor now supports `IStfIncludePathResolver` so nested includes resolve against their containing virtual file. `VirtualContentStreamReaderFactory` implements both upstream interfaces.

Regression tests now exercise root parsing, nested ZIP includes, and virtual-root escape rejection. The latest CI run passed its regression-test step. The WebAssembly publish is being verified separately.

**Still outstanding:** parsing a real route's metadata and track database, validating behavior against upstream route fixtures, and adapting the SBR binary readers.

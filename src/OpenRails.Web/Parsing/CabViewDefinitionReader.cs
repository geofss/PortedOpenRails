using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Reads the browser-relevant portion of an MSTS/Open Rails cab-view definition
/// using the same STF token grammar as the desktop simulator.
/// </summary>
public sealed class CabViewDefinitionReader
{
    public CabViewDefinition Read(IVirtualContentStore store, string path)
    {
        ArgumentNullException.ThrowIfNull(store);
        var virtualPath = VirtualPath.Normalize(path);
        lock (StfReaderAccess.SyncRoot)
        {
            STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(store);
            using var reader = new STFReader(virtualPath, useTree: false);
            var locations = new List<CabViewVector>();
            var directions = new List<CabViewVector>();
            var views = new List<CabViewImage>();
            var controlBlocks = 0;

            reader.ParseFile(new[]
            {
                new STFReader.TokenProcessor("tr_cabviewfile", () =>
                {
                    reader.MustMatch("(");
                    reader.ParseBlock(new[]
                    {
                        new STFReader.TokenProcessor("position", () =>
                            locations.Add(ReadVector(reader))),
                        new STFReader.TokenProcessor("direction", () =>
                            directions.Add(ReadVector(reader))),
                        new STFReader.TokenProcessor("cabviewfile", () =>
                        {
                            var relative = reader.ReadStringBlock(null);
                            if (string.IsNullOrWhiteSpace(relative))
                                throw new InvalidDataException($"Cab view '{virtualPath}' contains an empty CabViewFile entry.");
                            var normal = relative.Replace('\\', '/');
                            var resolved = VirtualPath.ResolveRelative(virtualPath, normal);
                            var filename = resolved[(resolved.LastIndexOf('/') + 1)..];
                            var slash = resolved.LastIndexOf('/');
                            var directory = slash < 0 ? "" : resolved[..(slash + 1)];
                            var stem = Path.GetFileNameWithoutExtension(filename);
                            var extension = Path.GetExtension(filename);
                            var highResolution = directory + stem + "1024" + extension;
                            var selected = store.Contains(highResolution) ? highResolution : resolved;
                            views.Add(new CabViewImage(selected,
                                directory + "NIGHT/" + Path.GetFileName(selected),
                                directory + "CABLIGHT/" + Path.GetFileName(selected)));
                        }),
                        new STFReader.TokenProcessor("cabviewcontrols", () =>
                        {
                            controlBlocks++;
                            reader.SkipBlock();
                        }),
                        new STFReader.TokenProcessor("ortscabviewcontrols", () =>
                        {
                            controlBlocks++;
                            reader.SkipBlock();
                        })
                    });
                })
            });

            if (locations.Count == 0 && directions.Count == 0 && views.Count == 0 && controlBlocks == 0)
                throw new InvalidDataException($"Cab-view file '{virtualPath}' does not contain a Tr_CabViewFile definition.");
            if (locations.Count != directions.Count)
                throw new InvalidDataException($"Cab-view file '{virtualPath}' has {locations.Count} position entries but {directions.Count} direction entries.");

            return new CabViewDefinition(virtualPath, locations, directions, views, controlBlocks);
        }
    }

    private static CabViewVector ReadVector(STFReader reader)
    {
        reader.MustMatch("(");
        var x = reader.ReadFloat(STFReader.UNITS.None, 0f);
        var y = reader.ReadFloat(STFReader.UNITS.None, 0f);
        var z = reader.ReadFloat(STFReader.UNITS.None, 0f);
        reader.MustMatch(")");
        return new CabViewVector(x, y, z);
    }
}

public sealed record CabViewVector(float X, float Y, float Z);
public sealed record CabViewImage(string DayImagePath, string NightImagePath, string CabLightImagePath);
public sealed record CabViewDefinition(string SourcePath, IReadOnlyList<CabViewVector> Locations,
    IReadOnlyList<CabViewVector> Directions, IReadOnlyList<CabViewImage> Views, int ControlBlockCount);

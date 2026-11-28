using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

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
            var controls = new List<CabViewControlDefinition>();
            var controlBlocks = 0;

            reader.ParseFile(new[]
            {
                new STFReader.TokenProcessor("tr_cabviewfile", () =>
                {
                    reader.MustMatch("(");
                    reader.ParseBlock(new[]
                    {
                        new STFReader.TokenProcessor("position", () => locations.Add(ReadVector(reader))),
                        new STFReader.TokenProcessor("direction", () => directions.Add(ReadVector(reader))),
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
                            controls.AddRange(ReadControls(reader, virtualPath));
                        }),
                        new STFReader.TokenProcessor("ortscabviewcontrols", () =>
                        {
                            controlBlocks++;
                            controls.AddRange(ReadControls(reader, virtualPath));
                        })
                    });
                })
            });

            if (locations.Count == 0 && directions.Count == 0 && views.Count == 0 && controlBlocks == 0)
                throw new InvalidDataException($"Cab-view file '{virtualPath}' does not contain a Tr_CabViewFile definition.");
            if (locations.Count != directions.Count)
                throw new InvalidDataException($"Cab-view file '{virtualPath}' has {locations.Count} position entries but {directions.Count} direction entries.");

            return new CabViewDefinition(virtualPath,
                Array.AsReadOnly(locations.ToArray()),
                Array.AsReadOnly(directions.ToArray()),
                Array.AsReadOnly(views.ToArray()),
                controlBlocks,
                Array.AsReadOnly(controls.ToArray()));
        }
    }

    private static IReadOnlyList<CabViewControlDefinition> ReadControls(STFReader reader, string sourcePath)
    {
        var controls = new List<CabViewControlDefinition>();
        reader.MustMatch("(");
        if (reader.EndOfBlock())
            return controls;
        var declaredCount = reader.ReadInt(null);
        if (declaredCount < 0 || declaredCount > 100_000)
            throw new InvalidDataException($"Cab-view '{sourcePath}' declares invalid control count {declaredCount}.");

        STFReader.TokenProcessor[] processors =
        {
            new("dial", () => controls.Add(ReadControl(reader, sourcePath, "dial"))),
            new("gauge", () => controls.Add(ReadControl(reader, sourcePath, "gauge"))),
            new("digital", () => controls.Add(ReadControl(reader, sourcePath, "digital"))),
            new("lever", () => controls.Add(ReadControl(reader, sourcePath, "lever"))),
            new("twostate", () => controls.Add(ReadControl(reader, sourcePath, "twostate"))),
            new("tristate", () => controls.Add(ReadControl(reader, sourcePath, "tristate"))),
            new("multistate", () => controls.Add(ReadControl(reader, sourcePath, "multistate"))),
            new("multistatedisplay", () => controls.Add(ReadControl(reader, sourcePath, "multistatedisplay"))),
            new("cabsignaldisplay", () => controls.Add(ReadControl(reader, sourcePath, "cabsignaldisplay"))),
            new("combinedcontrol", () => controls.Add(ReadControl(reader, sourcePath, "combinedcontrol"))),
            new("firebox", () => controls.Add(ReadControl(reader, sourcePath, "firebox"))),
            new("digitalclock", () => controls.Add(ReadControl(reader, sourcePath, "digitalclock"))),
            new("dialclock", () => controls.AddRange(ReadDialClock(reader, sourcePath))),
            new("screendisplay", () => controls.Add(ReadControl(reader, sourcePath, "screendisplay"))),
            new("ortsanimateddisplay", () => controls.Add(ReadControl(reader, sourcePath, "ortsanimateddisplay")))
        };
        reader.ParseBlock(processors);
        if (controls.Count > declaredCount)
            throw new InvalidDataException($"Cab-view '{sourcePath}' contains {controls.Count} controls but declares {declaredCount}.");
        return controls;
    }

    // Open Rails' dialclock container expands into three ordinary dial controls.
    // Preserve the three controls separately so the browser renderer can bind each
    // dial to the corresponding clock component without inventing a new control type.
    private static IReadOnlyList<CabViewControlDefinition> ReadDialClock(STFReader reader, string sourcePath)
    {
        var controls = new List<CabViewControlDefinition>();
        reader.MustMatch("(");
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("hours", () => controls.Add(ReadControl(reader, sourcePath, "dialclock-hours"))),
            new STFReader.TokenProcessor("minutes", () => controls.Add(ReadControl(reader, sourcePath, "dialclock-minutes"))),
            new STFReader.TokenProcessor("seconds", () => controls.Add(ReadControl(reader, sourcePath, "dialclock-seconds")))
        });
        return controls;
    }

    private static CabViewControlDefinition ReadControl(STFReader reader, string sourcePath, string kind)
    {
        var type = "";
        var label = "";
        var graphic = "";
        var style = "";
        var units = "";
        var screenPage = "";
        var x = 0d; var y = 0d; var width = 0d; var height = 0d;
        var minimum = 0d; var maximum = 1d;
        var direction = 0; var orientation = 0; var display = 0; var controlId = 0;
        var frames = 0; var framesX = 0; var framesY = 0;
        var positions = new List<int>();
        reader.MustMatch("(");
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("type", () =>
            {
                reader.MustMatch("(");
                type = reader.ReadString();
                reader.SkipRestOfBlock();
            }),
            new STFReader.TokenProcessor("position", () =>
            {
                reader.MustMatch("(");
                x = reader.ReadDouble(null); y = reader.ReadDouble(null);
                width = reader.ReadDouble(null); height = reader.ReadDouble(null);
                reader.SkipRestOfBlock();
            }),
            new STFReader.TokenProcessor("scalerange", () =>
            {
                reader.MustMatch("(");
                minimum = reader.ReadDouble(null); maximum = reader.ReadDouble(null);
                reader.SkipRestOfBlock();
            }),
            new STFReader.TokenProcessor("graphic", () =>
            {
                var relative = reader.ReadStringBlock(null);
                if (!string.IsNullOrWhiteSpace(relative))
                    graphic = VirtualPath.ResolveRelative(sourcePath, relative.Replace('\\', '/'));
            }),
            new STFReader.TokenProcessor("style", () => style = ReadSingleStringBlock(reader)),
            new STFReader.TokenProcessor("units", () => units = ReadSingleStringBlock(reader).Replace('/', '_')),
            new STFReader.TokenProcessor("ortslabel", () => label = ReadSingleStringBlock(reader)),
            new STFReader.TokenProcessor("ortsscreenpage", () => screenPage = ReadSingleStringBlock(reader)),
            new STFReader.TokenProcessor("dirincrease", () => direction = reader.ReadIntBlock(0)),
            new STFReader.TokenProcessor("orientation", () => orientation = reader.ReadIntBlock(0)),
            new STFReader.TokenProcessor("ortsdisplay", () => display = reader.ReadIntBlock(0)),
            new STFReader.TokenProcessor("controlid", () => controlId = reader.ReadIntBlock(0)),
            new STFReader.TokenProcessor("numframes", () =>
            {
                reader.MustMatch("(");
                frames = reader.ReadInt(0); framesX = reader.ReadInt(0); framesY = reader.ReadInt(0);
                reader.SkipRestOfBlock();
            }),
            new STFReader.TokenProcessor("numpositions", () =>
            {
                reader.MustMatch("(");
                var count = reader.ReadInt(0);
                for (var i = 0; i < count && !reader.EndOfBlock(); i++)
                    positions.Add(reader.ReadInt(0));
                reader.SkipRestOfBlock();
            })
        });
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(width) ||
            !double.IsFinite(height) || !double.IsFinite(minimum) || !double.IsFinite(maximum) ||
            width < 0 || height < 0 || maximum < minimum)
            throw new InvalidDataException($"Cab-view '{sourcePath}' has invalid geometry or scale range in {kind} control '{type}'.");
        return new CabViewControlDefinition(kind, type, label, graphic, style, units,
            screenPage, x, y, width, height, minimum, maximum, direction, orientation,
            display, controlId, frames, framesX, framesY, Array.AsReadOnly(positions.ToArray()));
    }

    private static string ReadSingleStringBlock(STFReader reader)
    {
        reader.MustMatch("(");
        var value = reader.ReadString();
        reader.SkipRestOfBlock();
        return value;
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
public sealed record CabViewControlDefinition(string Kind, string ControlType, string Label,
    string GraphicPath, string Style, string Units, string ScreenPage,
    double X, double Y, double Width, double Height, double Minimum, double Maximum,
    int Direction, int Orientation, int Display, int ControlId,
    int Frames, int FramesX, int FramesY, IReadOnlyList<int> Positions);
public sealed record CabViewDefinition(string SourcePath, IReadOnlyList<CabViewVector> Locations,
    IReadOnlyList<CabViewVector> Directions, IReadOnlyList<CabViewImage> Views,
    int ControlBlockCount, IReadOnlyList<CabViewControlDefinition> Controls);

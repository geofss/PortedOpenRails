using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Portable subset of TSECTION.DAT geometry. It preserves straight lengths and
/// curve radius/angle values from global and route-specific track sections.
/// Track database node graph parsing and route traversal build on this layer.
/// </summary>
public sealed class TrackSectionGeometryReader
{
    public TrackSectionCatalog ReadGlobal(IVirtualContentStore store, string path)
    {
        ArgumentNullException.ThrowIfNull(store);
        var normalized = VirtualPath.Normalize(path);
        lock (StfReaderAccess.SyncRoot)
        {
            STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(store);
            using var reader = new STFReader(normalized, useTree: false);
            var sections = new Dictionary<uint, TrackSectionGeometry>();
            var shapes = new Dictionary<uint, TrackShapeDefinition>();
            uint maximum = 0;
            uint maximumShapeIndex = 0;
            reader.ParseFile(new[]
            {
                new STFReader.TokenProcessor("tracksections", () =>
                {
                    reader.MustMatch("(");
                    maximum = reader.ReadUInt(null);
                    reader.ParseBlock(new[]
                    {
                        new STFReader.TokenProcessor("tracksection", () =>
                        {
                            var section = ReadSection(reader);
                            sections[section.Index] = section;
                        })
                    });
                }),
                new STFReader.TokenProcessor("trackshapes", () =>
                {
                    reader.MustMatch("(");
                    maximumShapeIndex = reader.ReadUInt(null);
                    reader.ParseBlock(new[]
                    {
                        new STFReader.TokenProcessor("trackshape", () =>
                        {
                            var shape = ReadTrackShape(reader);
                            shapes[shape.ShapeIndex] = shape;
                        })
                    });
                })
            });
            return new TrackSectionCatalog(maximum, sections, new TrackShapeCatalog(maximumShapeIndex, shapes));
        }
    }

    public TrackSectionCatalog ReadRouteSections(IVirtualContentStore store, string path)
    {
        ArgumentNullException.ThrowIfNull(store);
        var normalized = VirtualPath.Normalize(path);
        lock (StfReaderAccess.SyncRoot)
        {
            STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(store);
            using var reader = new STFReader(normalized, useTree: false);
            var sections = new Dictionary<uint, TrackSectionGeometry>();
            uint maximum = 0;
            reader.ParseFile(new[]
            {
                new STFReader.TokenProcessor("tracksections", () =>
                {
                    reader.MustMatch("(");
                    maximum = reader.ReadUInt(null);
                    reader.ParseBlock(new[]
                    {
                        new STFReader.TokenProcessor("tracksection", () =>
                        {
                            var section = ReadRouteSection(reader);
                            sections[section.Index] = section;
                        })
                    });
                })
            });
            return new TrackSectionCatalog(maximum, sections);
        }
    }

    private static TrackSectionGeometry ReadSection(STFReader reader)
    {
        reader.MustMatch("(");
        var index = reader.ReadUInt(null);
        float? width = null, length = null, radius = null, angleDegrees = null;
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("sectionsize", () =>
            {
                reader.MustMatch("(");
                width = reader.ReadFloat(STFReader.UNITS.Distance, null);
                length = reader.ReadFloat(STFReader.UNITS.Distance, null);
                reader.SkipRestOfBlock();
            }),
            new STFReader.TokenProcessor("sectioncurve", () =>
            {
                reader.MustMatch("(");
                radius = reader.ReadFloat(STFReader.UNITS.Distance, null);
                angleDegrees = reader.ReadFloat(STFReader.UNITS.None, null);
                reader.SkipRestOfBlock();
            })
        });
        return new TrackSectionGeometry(index, width, length, radius, angleDegrees, false);
    }

    private static TrackSectionGeometry ReadRouteSection(STFReader reader)
    {
        reader.MustMatch("(");
        reader.MustMatch("SectionCurve");
        reader.SkipBlock();
        var index = reader.ReadUInt(null);
        var first = reader.ReadFloat(STFReader.UNITS.Distance, null);
        var second = reader.ReadFloat(STFReader.UNITS.None, null);
        reader.SkipRestOfBlock();
        return second == 0
            ? new TrackSectionGeometry(index, null, first, null, null, true)
            : new TrackSectionGeometry(index, null, null, second, (float)(first * 180d / Math.PI), true);
    }
}

public sealed record TrackSectionGeometry(
    uint Index,
    float? WidthMeters,
    float? LengthMeters,
    float? RadiusMeters,
    float? AngleDegrees,
    bool IsRouteSpecific)
{
    public bool IsStraight => LengthMeters.HasValue && !RadiusMeters.HasValue;
    public double CalculatedLengthMeters =>
        IsStraight ? LengthMeters!.Value : RadiusMeters!.Value * Math.Abs(AngleDegrees!.Value) * Math.PI / 180d;

    public (double X, double Z) GetEndOffset()
    {
        if (IsStraight)
            return (0, LengthMeters!.Value);
        var angle = AngleDegrees!.Value * Math.PI / 180d;
        var radius = RadiusMeters!.Value;
        return (Math.Sign(angle) * radius * (1 - Math.Cos(Math.Abs(angle))), radius * Math.Sin(Math.Abs(angle)));
    }
}

public sealed class TrackSectionCatalog
{
    private readonly IReadOnlyDictionary<uint, TrackSectionGeometry> sections;
    public uint MaximumSectionIndex { get; }
    public int Count => sections.Count;

    public TrackSectionCatalog(uint maximumSectionIndex, IReadOnlyDictionary<uint, TrackSectionGeometry> sections)
    {
        MaximumSectionIndex = maximumSectionIndex;
        this.sections = new Dictionary<uint, TrackSectionGeometry>(sections);
    }

    public bool TryGet(uint index, out TrackSectionGeometry section) => sections.TryGetValue(index, out section!);
    public TrackSectionGeometry GetRequired(uint index) => sections.TryGetValue(index, out var section)
        ? section
        : throw new InvalidDataException($"Track section {index} was not found in the catalog.");
}

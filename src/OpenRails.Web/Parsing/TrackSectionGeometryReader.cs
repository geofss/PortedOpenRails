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
            var dynamicPaths = new Dictionary<uint, TrackPathDefinition>();
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
                }),
                new STFReader.TokenProcessor("sectionidx", () =>
                {
                    reader.MustMatch("(");
                    var declaredCount = reader.ReadUInt(null);
                    if (declaredCount > 100000)
                        throw new InvalidDataException($"Route TSECTION declares too many dynamic paths: {declaredCount}.");
                    reader.ParseBlock(new[]
                    {
                        new STFReader.TokenProcessor("trackpath", () =>
                        {
                            reader.MustMatch("(");
                            var dynamicIndex = reader.ReadUInt(null);
                            var count = reader.ReadUInt(null);
                            if (count > 4096)
                                throw new InvalidDataException($"Dynamic track path {dynamicIndex} has too many sections: {count}.");
                            var indices = new uint[count];
                            for (var i = 0; i < indices.Length; i++)
                                indices[i] = reader.ReadUInt(null);
                            reader.SkipRestOfBlock();
                            if (!dynamicPaths.TryAdd(dynamicIndex, new TrackPathDefinition(dynamicIndex, indices)))
                                throw new InvalidDataException($"Duplicate dynamic track path index {dynamicIndex}.");
                        })
                    });
                    // Match upstream Open Rails: SectionIdx's declared value is a dictionary
                    // capacity hint, not a required number of TrackPath records. Real routes
                    // (including Demo Model 1) legitimately contain sparse dynamic indices.
                })
            });
            return new TrackSectionCatalog(maximum, sections, dynamicPaths: dynamicPaths);
        }
    }

    private static TrackShapeDefinition ReadTrackShape(STFReader reader)
    {
        reader.MustMatch("(");
        var index = reader.ReadUInt(null);
        string? fileName = null;
        uint numberOfPaths = 0;
        uint mainRoute = 0;
        double clearanceDistance = 0;
        bool tunnel = false, road = false, rack = false;
        var paths = new List<TrackShapePath>();

        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("filename", () => fileName = reader.ReadStringBlock(null)),
            new STFReader.TokenProcessor("numpaths", () => numberOfPaths = reader.ReadUIntBlock(null)),
            new STFReader.TokenProcessor("mainroute", () => mainRoute = reader.ReadUIntBlock(null)),
            new STFReader.TokenProcessor("clearancedist", () => clearanceDistance = reader.ReadFloatBlock(STFReader.UNITS.Distance, null)),
            new STFReader.TokenProcessor("sectionidx", () => paths.Add(ReadTrackShapePath(reader))),
            new STFReader.TokenProcessor("tunnelshape", () => tunnel = reader.ReadBoolBlock(true)),
            new STFReader.TokenProcessor("roadshape", () => road = reader.ReadBoolBlock(true)),
            new STFReader.TokenProcessor("ortsrackshape", () => rack = reader.ReadBoolBlock(true))
        });

        if (numberOfPaths > 4096)
            throw new InvalidDataException($"Track shape {index} has too many paths: {numberOfPaths}.");

        return new TrackShapeDefinition(index, fileName, numberOfPaths, mainRoute,
            clearanceDistance, paths, tunnel, road, rack);
    }

    private static TrackShapePath ReadTrackShapePath(STFReader reader)
    {
        reader.MustMatch("(");
        var count = reader.ReadUInt(null);
        if (count > 4096)
            throw new InvalidDataException($"Track shape path section count {count} is outside the supported range.");

        var x = reader.ReadDouble(null);
        var y = reader.ReadDouble(null);
        var z = reader.ReadDouble(null);
        var angle = reader.ReadDouble(null);
        var sections = new uint[count];
        for (var i = 0; i < sections.Length; i++)
            sections[i] = reader.ReadUInt(null);
        reader.SkipRestOfBlock();
        return new TrackShapePath(x, y, z, angle, sections);
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

    public TrackShapeCatalog Shapes { get; }
    private readonly IReadOnlyDictionary<uint, TrackPathDefinition> dynamicPaths;
    public IReadOnlyDictionary<uint, TrackPathDefinition> DynamicPaths => dynamicPaths;

    public TrackSectionCatalog(uint maximumSectionIndex, IReadOnlyDictionary<uint, TrackSectionGeometry> sections,
        TrackShapeCatalog? shapes = null, IReadOnlyDictionary<uint, TrackPathDefinition>? dynamicPaths = null)
    {
        MaximumSectionIndex = maximumSectionIndex;
        this.sections = new Dictionary<uint, TrackSectionGeometry>(sections);
        Shapes = shapes ?? new TrackShapeCatalog(0, new Dictionary<uint, TrackShapeDefinition>());
        this.dynamicPaths = new Dictionary<uint, TrackPathDefinition>(dynamicPaths ?? new Dictionary<uint, TrackPathDefinition>());
    }

    public bool TryGetDynamicPath(uint index, out TrackPathDefinition path) => dynamicPaths.TryGetValue(index, out path!);

    public bool TryGet(uint index, out TrackSectionGeometry section) => sections.TryGetValue(index, out section!);
    public TrackSectionGeometry GetRequired(uint index) => sections.TryGetValue(index, out var section)
        ? section
        : throw new InvalidDataException($"Track section {index} was not found in the catalog.");
}

/// <summary>Original route TSECTION.DAT TrackPath mapping for a dynamic section index.</summary>
public sealed record TrackPathDefinition(uint DynamicSectionIndex, IReadOnlyList<uint> TrackSectionIndices);

public sealed record TrackShapePath(
    double OffsetX,
    double OffsetY,
    double OffsetZ,
    double AngleRadians,
    IReadOnlyList<uint> TrackSectionIndices);

public sealed record TrackShapeDefinition(
    uint ShapeIndex,
    string? FileName,
    uint NumberOfPaths,
    uint MainRoute,
    double ClearanceDistanceMeters,
    IReadOnlyList<TrackShapePath> Paths,
    bool IsTunnel,
    bool IsRoad,
    bool IsRack);

public sealed class TrackShapeCatalog
{
    private readonly IReadOnlyDictionary<uint, TrackShapeDefinition> shapes;
    public uint MaximumShapeIndex { get; }
    public int Count => shapes.Count;

    public TrackShapeCatalog(uint maximumShapeIndex, IReadOnlyDictionary<uint, TrackShapeDefinition> shapes)
    {
        MaximumShapeIndex = maximumShapeIndex;
        this.shapes = new Dictionary<uint, TrackShapeDefinition>(shapes);
    }

    public bool TryGet(uint index, out TrackShapeDefinition shape) => shapes.TryGetValue(index, out shape!);
    public TrackShapeDefinition GetRequired(uint index) => shapes.TryGetValue(index, out var shape)
        ? shape
        : throw new InvalidDataException($"Track shape {index} was not found in the catalog.");
}

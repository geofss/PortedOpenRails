using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

public sealed class TrackSectionGeometryReader
{
    public TrackSectionCatalog ReadGlobal(IVirtualContentStore store, string path) => Read(store, path, false);
    public TrackSectionCatalog ReadRouteSections(IVirtualContentStore store, string path) => Read(store, path, true);

    private static TrackSectionCatalog Read(IVirtualContentStore store, string path, bool routeSpecific)
    {
        ArgumentNullException.ThrowIfNull(store);
        var normalized = VirtualPath.Normalize(path);
        lock (StfReaderAccess.SyncRoot)
        {
            STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(store);
            using var reader = new STFReader(normalized, useTree: false);
            var sections = new Dictionary<uint, TrackSectionGeometry>();
            uint maximum = 0;
            reader.ParseFile(new[] { new STFReader.TokenProcessor("tracksections", () =>
            {
                reader.MustMatch("(");
                maximum = reader.ReadUInt(null);
                reader.ParseBlock(new[] { new STFReader.TokenProcessor("tracksection", () =>
                {
                    var section = routeSpecific ? ReadRouteSection(reader) : ReadGlobalSection(reader);
                    sections[section.Index] = section;
                }) });
            }) });
            return new TrackSectionCatalog(maximum, sections);
        }
    }

    private static TrackSectionGeometry ReadGlobalSection(STFReader r)
    {
        r.MustMatch("(");
        var index = r.ReadUInt(null);
        float? width = null, length = null, radius = null, angle = null;
        r.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("sectionsize", () => { r.MustMatch("("); width = r.ReadFloat(STFReader.UNITS.Distance, null); length = r.ReadFloat(STFReader.UNITS.Distance, null); r.SkipRestOfBlock(); }),
            new STFReader.TokenProcessor("sectioncurve", () => { r.MustMatch("("); radius = r.ReadFloat(STFReader.UNITS.Distance, null); angle = r.ReadFloat(STFReader.UNITS.None, null); r.SkipRestOfBlock(); })
        });
        return new TrackSectionGeometry(index, width, length, radius, angle, false);
    }

    private static TrackSectionGeometry ReadRouteSection(STFReader r)
    {
        r.MustMatch("("); r.MustMatch("SectionCurve"); r.SkipBlock();
        var index = r.ReadUInt(null);
        var a = r.ReadFloat(STFReader.UNITS.Distance, null);
        var b = r.ReadFloat(STFReader.UNITS.None, null);
        r.SkipRestOfBlock();
        return b == 0
            ? new TrackSectionGeometry(index, null, a, null, null, true)
            : new TrackSectionGeometry(index, null, null, b, (float)(a * 180d / Math.PI), true);
    }
}

public sealed record TrackSectionGeometry(uint Index, float? WidthMeters, float? LengthMeters, float? RadiusMeters, float? AngleDegrees, bool IsRouteSpecific)
{
    public bool IsStraight => LengthMeters.HasValue && !RadiusMeters.HasValue;
    public double CalculatedLengthMeters => IsStraight ? LengthMeters!.Value : RadiusMeters!.Value * Math.Abs(AngleDegrees!.Value) * Math.PI / 180d;
    public (double X, double Z) GetEndOffset()
    {
        if (IsStraight) return (0, LengthMeters!.Value);
        var a = AngleDegrees!.Value * Math.PI / 180d;
        var radius = RadiusMeters!.Value;
        return (radius * (1 - Math.Cos(a)), radius * Math.Sin(a));
    }
}

public sealed class TrackSectionCatalog
{
    private readonly IReadOnlyDictionary<uint, TrackSectionGeometry> sections;
    public uint MaximumSectionIndex { get; }
    public int Count => sections.Count;
    public TrackSectionCatalog(uint max, IReadOnlyDictionary<uint, TrackSectionGeometry> sections) { MaximumSectionIndex = max; this.sections = new Dictionary<uint, TrackSectionGeometry>(sections); }
    public bool TryGet(uint index, out TrackSectionGeometry section) => sections.TryGetValue(index, out section!);
    public TrackSectionGeometry GetRequired(uint index) => sections.TryGetValue(index, out var s) ? s : throw new InvalidDataException($"Track section {index} was not found in the catalog.");
}

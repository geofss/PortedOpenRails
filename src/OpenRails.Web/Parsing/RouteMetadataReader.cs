using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Reads route metadata using the upstream Open Rails STF parser and mirrors
/// the portable route settings consumed from Orts.Formats.Msts.RouteFile.
/// </summary>
public sealed class RouteMetadataReader
{
    private static readonly object FactoryLock = new();

    public RouteMetadata Read(IVirtualContentStore contentStore, string routeFilePath)
    {
        ArgumentNullException.ThrowIfNull(contentStore);
        var virtualPath = VirtualPath.Normalize(routeFilePath);

        // Upstream STFReader exposes a one-shot static factory injection point.
        // Serialize construction/parsing so concurrent route loads cannot exchange factories.
        lock (FactoryLock)
        {
            STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(contentStore);
            using var reader = new STFReader(virtualPath, useTree: false);

            RouteMetadata? metadata = null;
            reader.ParseFile(new[]
            {
                new STFReader.TokenProcessor("tr_routefile", () => metadata = ReadRouteBlock(reader))
            });

            return metadata ?? throw new InvalidDataException(
                $"The route file '{virtualPath}' does not contain a Tr_RouteFile block.");
        }
    }

    private static RouteMetadata ReadRouteBlock(STFReader reader)
    {
        string? routeId = null;
        string? name = null;
        string? description = null;
        string? fileName = null;
        var electrified = true;
        float maxLineVoltage = 0;
        float overheadWireHeight = 6.0f;
        float speedLimit = 500.0f;
        float tempRestrictedSpeed = -1.0f;
        float routeGaugeMeters = 0;
        var milepostUnitsMetric = false;
        var superElevationMode = -1;
        RouteStartCoordinates? routeStart = null;

        reader.MustMatch("(");
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("routeid", () => routeId = reader.ReadStringBlock(null)),
            new STFReader.TokenProcessor("name", () => name = reader.ReadStringBlock(null)),
            new STFReader.TokenProcessor("description", () => description = reader.ReadStringBlock(null)),
            new STFReader.TokenProcessor("filename", () => fileName = reader.ReadStringBlock(null)),
            new STFReader.TokenProcessor("electrified", () => electrified = reader.ReadBoolBlock(false)),
            new STFReader.TokenProcessor("maxlinevoltage", () => maxLineVoltage = reader.ReadFloatBlock(STFReader.UNITS.None, 0)),
            new STFReader.TokenProcessor("overheadwireheight", () => overheadWireHeight = reader.ReadFloatBlock(STFReader.UNITS.Distance, 6.0f)),
            new STFReader.TokenProcessor("speedlimit", () => speedLimit = reader.ReadFloatBlock(STFReader.UNITS.Speed, 500.0f)),
            new STFReader.TokenProcessor("temprestrictedspeed", () => tempRestrictedSpeed = reader.ReadFloatBlock(STFReader.UNITS.Speed, -1.0f)),
            new STFReader.TokenProcessor("trackgauge", () => routeGaugeMeters = reader.ReadFloatBlock(STFReader.UNITS.Distance, 0)),
            new STFReader.TokenProcessor("milepostunitskilometers", () => milepostUnitsMetric = true),
            new STFReader.TokenProcessor("ortsforcesuperelevation", () => superElevationMode = reader.ReadBoolBlock(false) ? 1 : 0),
            new STFReader.TokenProcessor("routestart", () => routeStart = ReadRouteStart(reader))
        });

        if (string.IsNullOrWhiteSpace(routeId))
            throw new InvalidDataException("The route metadata is missing RouteID.");
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidDataException("The route metadata is missing Name.");
        if (string.IsNullOrWhiteSpace(description))
            throw new InvalidDataException("The route metadata is missing Description.");
        if (routeStart is null)
            throw new InvalidDataException("The route metadata is missing RouteStart.");

        return new RouteMetadata(
            routeId, name, description, electrified, maxLineVoltage, routeStart, fileName,
            overheadWireHeight, speedLimit, tempRestrictedSpeed, routeGaugeMeters,
            milepostUnitsMetric, superElevationMode);
    }

    private static RouteStartCoordinates ReadRouteStart(STFReader reader)
    {
        reader.MustMatch("(");
        var tileX = reader.ReadDouble(null);
        var tileZ = reader.ReadDouble(null);
        var x = reader.ReadDouble(null);
        var z = reader.ReadDouble(null);
        reader.SkipRestOfBlock();
        return new RouteStartCoordinates(tileX, tileZ, x, z);
    }
}

public sealed record RouteMetadata(
    string RouteId,
    string Name,
    string Description,
    bool Electrified,
    float MaxLineVoltage,
    RouteStartCoordinates RouteStart,
    string? FileName = null,
    float OverheadWireHeightMeters = 6.0f,
    float SpeedLimitMetersPerSecond = 500.0f,
    float TempRestrictedSpeedMetersPerSecond = -1.0f,
    float TrackGaugeMeters = 0,
    bool MilepostUnitsMetric = false,
    int SuperElevationMode = -1);

public sealed record RouteStartCoordinates(double TileX, double TileZ, double X, double Z);

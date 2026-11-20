using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Reads the portable, identifying metadata from an MSTS/Open Rails .trk route file.
/// The tokenization, include handling, and STF grammar are provided by upstream STFReader.
/// </summary>
public sealed class RouteMetadataReader
{
    public RouteMetadata Read(IVirtualContentStore contentStore, string routeFilePath)
    {
        ArgumentNullException.ThrowIfNull(contentStore);
        var virtualPath = VirtualPath.Normalize(routeFilePath);

        // Upstream STFReader exposes a one-shot static factory injection point.
        // Serialize construction/parsing so concurrent route loads cannot exchange factories.
        lock (StfReaderAccess.SyncRoot)
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
        bool electrified = true;
        float maxLineVoltage = 0;
        RouteStartCoordinates? routeStart = null;

        reader.MustMatch("(");
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("routeid", () => routeId = reader.ReadStringBlock(null)),
            new STFReader.TokenProcessor("name", () => name = reader.ReadStringBlock(null)),
            new STFReader.TokenProcessor("description", () => description = reader.ReadStringBlock(null)),
            new STFReader.TokenProcessor("electrified", () => electrified = reader.ReadBoolBlock(true)),
            new STFReader.TokenProcessor("maxlinevoltage", () => maxLineVoltage = reader.ReadFloatBlock(STFReader.UNITS.None, 0)),
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

        return new RouteMetadata(routeId, name, description, electrified, maxLineVoltage, routeStart);
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
    RouteStartCoordinates RouteStart);

public sealed record RouteStartCoordinates(double TileX, double TileZ, double X, double Z);

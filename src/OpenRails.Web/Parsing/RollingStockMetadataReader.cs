using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

public sealed record RollingStockMetadata(
    string Name,
    string Type,
    float MassKg,
    float WidthM,
    float HeightM,
    float LengthM,
    int? NumberAxles,
    float? NumberWheels,
    string? BrakeSystemType,
    string? BrakeEquipmentType,
    float? MaxBrakeForceN,
    string SourcePath);

/// <summary>Reads the shared Wagon block of upstream .wag/.eng files through virtual content.</summary>
public sealed class RollingStockMetadataReader
{
    private static readonly object FactoryLock = new();

    public RollingStockMetadata Read(IVirtualContentStore store, string path)
    {
        ArgumentNullException.ThrowIfNull(store);
        var virtualPath = VirtualPath.Normalize(path);
        lock (FactoryLock)
        {
            STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(store);
            using var reader = new STFReader(virtualPath, useTree: false);
            var name = Path.GetFileNameWithoutExtension(virtualPath);
            var type = string.Empty;
            var massKg = 0f;
            var widthM = 2.5f;
            var heightM = 4f;
            var lengthM = 40f;
            int? numberAxles = null;
            float? numberWheels = null;
            string? brakeSystemType = null;
            string? brakeEquipmentType = null;
            float? maxBrakeForceN = null;
            var wagonFound = false;
            reader.ParseFile(new[]
            {
                new STFReader.TokenProcessor("wagon", () =>
                {
                    wagonFound = true;
                    type = reader.ReadString();
                    reader.ParseBlock(new[]
                    {
                        new STFReader.TokenProcessor("name", () => name = reader.ReadStringBlock(null)),
                        new STFReader.TokenProcessor("type", () => type = reader.ReadStringBlock(null)),
                        new STFReader.TokenProcessor("mass", () => massKg = reader.ReadFloatBlock(STFReader.UNITS.Mass, null)),
                        new STFReader.TokenProcessor("size", () => ReadSize(reader, value => widthM = value.Width, value => heightM = value.Height, value => lengthM = value.Length)),
                        new STFReader.TokenProcessor("ortsnumberaxles", () => numberAxles = reader.ReadIntBlock(null)),
                        new STFReader.TokenProcessor("numwheels", () => numberWheels = reader.ReadFloatBlock(STFReader.UNITS.None, null)),
                        new STFReader.TokenProcessor("brakesystemtype", () => brakeSystemType = reader.ReadStringBlock(null)),
                        new STFReader.TokenProcessor("brakeequipmenttype", () => brakeEquipmentType = reader.ReadStringBlock(null)),
                        new STFReader.TokenProcessor("maxbrakeforce", () => maxBrakeForceN = reader.ReadFloatBlock(STFReader.UNITS.Force, null))
                    });
                })
            });
            if (!wagonFound)
                throw new InvalidDataException($"Rolling-stock file '{virtualPath}' contains no Wagon block.");
            return new RollingStockMetadata(name, type, massKg, widthM, heightM, lengthM, numberAxles, numberWheels, brakeSystemType, brakeEquipmentType, maxBrakeForceN, virtualPath);
        }
    }

    private static void ReadSize(STFReader reader, Action<SizeValue> setWidth, Action<SizeValue> setHeight, Action<SizeValue> setLength)
    {
        reader.MustMatch("(");
        setWidth(new SizeValue(reader.ReadFloat(STFReader.UNITS.Distance, null)));
        setHeight(new SizeValue(reader.ReadFloat(STFReader.UNITS.Distance, null)));
        setLength(new SizeValue(reader.ReadFloat(STFReader.UNITS.Distance, null)));
        reader.MustMatch(")");
    }

    private sealed record SizeValue(float Width, float Height, float Length);
}

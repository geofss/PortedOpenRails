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
    string SourcePath,
    string? ShapeFileName = null,
    float? MinCouplerStrengthN = null,
    float? DavisAForceN = null,
    float? DavisBResistance = null,
    float? DavisCResistance = null,
    float? CouplerStiffnessNPerM = null,
    float? CouplerDampingNsPerM = null,
    float? CouplerSlackMeters = null,
    float? MaxHandBrakeForceN = null,
    float? MaxBrakeShoeForceN = null,
    int? NumberCarBrakeShoes = null,
    string? BrakeShoeType = null,
    float? WheelRadiusM = null,
    float? DriverWheelRadiusM = null);

/// <summary>Reads the shared Wagon block of upstream .wag/.eng files through virtual content.</summary>
public sealed class RollingStockMetadataReader
{
    public RollingStockMetadata Read(IVirtualContentStore store, string path)
    {
        ArgumentNullException.ThrowIfNull(store);
        var virtualPath = VirtualPath.Normalize(path);
        lock (StfReaderAccess.SyncRoot)
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
            float? wheelRadiusM = null;
            float? driverWheelRadiusM = null;
            string? brakeSystemType = null;
            string? brakeEquipmentType = null;
            float? maxBrakeForceN = null;
            float? maxHandBrakeForceN = null;
            float? maxBrakeShoeForceN = null;
            int? numberCarBrakeShoes = null;
            string? brakeShoeType = null;
            float? minCouplerStrengthN = null;
            float? davisAForceN = null;
            float? davisBResistance = null;
            float? davisCResistance = null;
            var couplerStiffnessValues = new List<float>();
            var couplerDampingValues = new List<float>();
            var couplerSlackValues = new List<float>();
            string? shapeFileName = null;
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
                        new STFReader.TokenProcessor("wagonshape", () => shapeFileName = reader.ReadStringBlock(null)),
                        new STFReader.TokenProcessor("mass", () => massKg = reader.ReadFloatBlock(STFReader.UNITS.Mass, null)),
                        new STFReader.TokenProcessor("size", () => ReadSize(reader, value => widthM = value, value => heightM = value, value => lengthM = value)),
                        new STFReader.TokenProcessor("ortsnumberaxles", () => numberAxles = reader.ReadIntBlock(null)),
                        new STFReader.TokenProcessor("numwheels", () => numberWheels = reader.ReadFloatBlock(STFReader.UNITS.None, null)),
                        new STFReader.TokenProcessor("wheelradius", () => wheelRadiusM = reader.ReadFloatBlock(STFReader.UNITS.Distance, null)),
                        new STFReader.TokenProcessor("engine", () => driverWheelRadiusM = ReadEngineWheelRadius(reader)),
                        new STFReader.TokenProcessor("brakesystemtype", () => brakeSystemType = reader.ReadStringBlock(null)),
                        new STFReader.TokenProcessor("brakeequipmenttype", () => brakeEquipmentType = reader.ReadStringBlock(null)),
                        new STFReader.TokenProcessor("maxbrakeforce", () => maxBrakeForceN = reader.ReadFloatBlock(STFReader.UNITS.Force, null)),
                        new STFReader.TokenProcessor("maxhandbrakeforce", () => maxHandBrakeForceN = reader.ReadFloatBlock(STFReader.UNITS.Force, null)),
                        new STFReader.TokenProcessor("ortsmaxbrakeshoeforce", () => maxBrakeShoeForceN = reader.ReadFloatBlock(STFReader.UNITS.Force, null)),
                        new STFReader.TokenProcessor("ortsnumbercarbrakeshoes", () => numberCarBrakeShoes = reader.ReadIntBlock(null)),
                        new STFReader.TokenProcessor("ortsbrakeshoetype", () => brakeShoeType = ReadBrakeShoeType(reader)),
                        new STFReader.TokenProcessor("coupling", () => minCouplerStrengthN = ReadCoupling(reader, minCouplerStrengthN, couplerStiffnessValues, couplerDampingValues, couplerSlackValues)),
                        new STFReader.TokenProcessor("ortsdavis_a", () => davisAForceN = reader.ReadFloatBlock(STFReader.UNITS.Force, 0f)),
                        new STFReader.TokenProcessor("ortsdavis_b", () => davisBResistance = reader.ReadFloatBlock(STFReader.UNITS.Resistance, 0f)),
                        new STFReader.TokenProcessor("ortsdavis_c", () => davisCResistance = reader.ReadFloatBlock(STFReader.UNITS.ResistanceDavisC, 0f))
                    });
                })
            });
            if (!wagonFound)
                throw new InvalidDataException($"Rolling-stock file '{virtualPath}' contains no Wagon block.");
            return new RollingStockMetadata(name, type, massKg, widthM, heightM, lengthM, numberAxles, numberWheels,
                brakeSystemType, brakeEquipmentType, maxBrakeForceN, virtualPath, shapeFileName,
                minCouplerStrengthN, davisAForceN, davisBResistance, davisCResistance,
                Average(couplerStiffnessValues), Average(couplerDampingValues), Average(couplerSlackValues),
                maxHandBrakeForceN, maxBrakeShoeForceN, numberCarBrakeShoes, brakeShoeType,
                wheelRadiusM, driverWheelRadiusM);
        }
    }

    private static float? ReadEngineWheelRadius(STFReader reader)
    {
        float? radius = null;
        reader.MustMatch("(");
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("wheelradius", () =>
                radius = reader.ReadFloatBlock(STFReader.UNITS.Distance, null))
        });
        return radius;
    }

    private static string ReadBrakeShoeType(STFReader reader)
    {
        reader.MustMatch("(");
        var value = reader.ReadString();
        reader.SkipRestOfBlock();
        return value;
    }

    private static float? ReadCoupling(
        STFReader reader,
        float? currentMinimum,
        List<float> stiffnessValues,
        List<float> dampingValues,
        List<float> slackValues)
    {
        float? legacyBreak = null;
        float? openRailsBreak = null;
        reader.MustMatch("(");
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("spring", () =>
            {
                reader.MustMatch("(");
                reader.ParseBlock(new[]
                {
                    new STFReader.TokenProcessor("stiffness", () => AddPair(reader, STFReader.UNITS.Stiffness, stiffnessValues)),
                    new STFReader.TokenProcessor("damping", () => AddPair(reader, STFReader.UNITS.Resistance, dampingValues)),
                    new STFReader.TokenProcessor("r0", () => AddPair(reader, STFReader.UNITS.Distance, slackValues)),
                    new STFReader.TokenProcessor("break", () => legacyBreak = ReadBreakPair(reader)),
                    new STFReader.TokenProcessor("ortsbreak", () => openRailsBreak = ReadBreakPair(reader))
                });
            })
        });

        var strength = openRailsBreak ?? legacyBreak;
        if (strength is null)
            return currentMinimum;
        return currentMinimum is null ? strength : Math.Min(currentMinimum.Value, strength.Value);
    }

    private static void AddPair(STFReader reader, STFReader.UNITS units, List<float> values)
    {
        reader.MustMatch("(");
        var first = reader.ReadFloat(units, null);
        var second = reader.ReadFloat(units, null);
        reader.SkipRestOfBlock();
        if (float.IsFinite(first) && first >= 0) values.Add(first);
        if (float.IsFinite(second) && second >= 0) values.Add(second);
    }

    private static float? Average(IReadOnlyList<float> values) =>
        values.Count == 0 ? null : values.Average();

    private static float? ReadBreakPair(STFReader reader)
    {
        reader.MustMatch("(");
        float? minimum = null;
        for (var i = 0; i < 2; i++)
        {
            var value = reader.ReadFloat(STFReader.UNITS.Force, null);
            if (value > 9.9f)
                minimum = minimum is null ? value : Math.Min(minimum.Value, value);
        }
        reader.SkipRestOfBlock();
        return minimum;
    }

    private static void ReadSize(STFReader reader, Action<float> setWidth, Action<float> setHeight, Action<float> setLength)
    {
        reader.MustMatch("(");
        setWidth(reader.ReadFloat(STFReader.UNITS.Distance, null));
        setHeight(reader.ReadFloat(STFReader.UNITS.Distance, null));
        setLength(reader.ReadFloat(STFReader.UNITS.Distance, null));
        reader.MustMatch(")");
    }
}

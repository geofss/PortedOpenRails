using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Reads the portable identifying and motion-related fields from MSTS/Open Rails
/// .eng and .wag files using the upstream STFReader grammar.
/// This deliberately does not instantiate the desktop rolling-stock simulation.
/// </summary>
public sealed class RollingStockMetadataReader
{
    public EngineMetadata ReadEngine(IVirtualContentStore contentStore, string filePath)
    {
        ArgumentNullException.ThrowIfNull(contentStore);
        var path = VirtualPath.Normalize(filePath);
        if (!path.EndsWith(".eng", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("An engine metadata path must end in .eng.", nameof(filePath));

        lock (StfReaderAccess.SyncRoot)
        {
            STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(contentStore);
            using var reader = new STFReader(path, useTree: false);
            var metadata = new EngineMetadata(Path.GetFileNameWithoutExtension(path));
            bool found = false;

            reader.ParseFile(new[]
            {
                new STFReader.TokenProcessor("engine", () =>
                {
                    reader.ReadString();
                    reader.ParseBlock(new[]
                    {
                        new STFReader.TokenProcessor("name", () => metadata.Name = reader.ReadStringBlock(metadata.Name)),
                        new STFReader.TokenProcessor("type", () => metadata.EngineType = reader.ReadStringBlock(null)),
                        new STFReader.TokenProcessor("mass", () => metadata.MassKilograms = reader.ReadFloatBlock(STFReader.UNITS.Mass, null)),
                        new STFReader.TokenProcessor("size", () =>
                        {
                            reader.MustMatch("(");
                            metadata.WidthMeters = reader.ReadFloat(STFReader.UNITS.Distance, null);
                            metadata.HeightMeters = reader.ReadFloat(STFReader.UNITS.Distance, null);
                            metadata.LengthMeters = reader.ReadFloat(STFReader.UNITS.Distance, null);
                            reader.MustMatch(")");
                        }),
                        new STFReader.TokenProcessor("maxpower", () => metadata.MaximumPowerWatts = reader.ReadFloatBlock(STFReader.UNITS.Power, null)),
                        new STFReader.TokenProcessor("ortsdieseltransmissionefficiency", () => metadata.TransmissionEfficiency = reader.ReadFloatBlock(STFReader.UNITS.None, 1f)),
                        new STFReader.TokenProcessor("maxforce", () => metadata.MaximumTractiveForceNewtons = reader.ReadFloatBlock(STFReader.UNITS.Force, null)),
                        new STFReader.TokenProcessor("maxcontinuousforce", () => metadata.MaximumContinuousForceNewtons = reader.ReadFloatBlock(STFReader.UNITS.Force, null)),
                        new STFReader.TokenProcessor("dynamicbrakesmaximumforce", () => metadata.MaximumDynamicBrakeForceNewtons = reader.ReadFloatBlock(STFReader.UNITS.Force, null)),
                        new STFReader.TokenProcessor("maxbrakeforce", () => metadata.MaximumBrakeForceNewtons = reader.ReadFloatBlock(STFReader.UNITS.Force, null)),
                        new STFReader.TokenProcessor("maxvelocity", () => metadata.MaximumSpeedMetersPerSecond = reader.ReadFloatBlock(STFReader.UNITS.Speed, null)),
                        new STFReader.TokenProcessor("description", () => metadata.Description = reader.ReadStringBlock(null))
                    });
                    found = true;
                })
            });

            if (!found)
                throw new InvalidDataException($"The engine file '{path}' does not contain an Engine block.");
            metadata.TransmissionEfficiency = Math.Clamp(metadata.TransmissionEfficiency, 0.001f, 1f);
            return metadata;
        }
    }

    public WagonMetadata ReadWagon(IVirtualContentStore contentStore, string filePath)
    {
        ArgumentNullException.ThrowIfNull(contentStore);
        var path = VirtualPath.Normalize(filePath);
        if (!path.EndsWith(".wag", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A wagon metadata path must end in .wag.", nameof(filePath));

        lock (FactoryLock)
        {
            STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(contentStore);
            using var reader = new STFReader(path, useTree: false);
            var metadata = new WagonMetadata(Path.GetFileNameWithoutExtension(path));
            bool found = false;

            reader.ParseFile(new[]
            {
                new STFReader.TokenProcessor("wagon", () =>
                {
                    reader.ReadString();
                    reader.ParseBlock(new[]
                    {
                        new STFReader.TokenProcessor("name", () => metadata.Name = reader.ReadStringBlock(metadata.Name)),
                        new STFReader.TokenProcessor("type", () => metadata.WagonType = reader.ReadStringBlock(null)),
                        new STFReader.TokenProcessor("mass", () => metadata.MassKilograms = reader.ReadFloatBlock(STFReader.UNITS.Mass, null)),
                        new STFReader.TokenProcessor("size", () =>
                        {
                            reader.MustMatch("(");
                            metadata.WidthMeters = reader.ReadFloat(STFReader.UNITS.Distance, null);
                            metadata.HeightMeters = reader.ReadFloat(STFReader.UNITS.Distance, null);
                            metadata.LengthMeters = reader.ReadFloat(STFReader.UNITS.Distance, null);
                            reader.MustMatch(")");
                        }),
                        new STFReader.TokenProcessor("ortsnumberaxles", () => metadata.NumberOfAxles = reader.ReadIntBlock(null)),
                        new STFReader.TokenProcessor("brakesystemtype", () => metadata.BrakeSystemType = reader.ReadStringBlock(null)),
                        new STFReader.TokenProcessor("brakeequipmenttype", () => metadata.BrakeEquipmentType = reader.ReadStringBlock(null)),
                        new STFReader.TokenProcessor("maxbrakeforce", () => metadata.MaximumBrakeForceNewtons = reader.ReadFloatBlock(STFReader.UNITS.Force, null))
                    });
                    found = true;
                })
            });

            if (!found)
                throw new InvalidDataException($"The wagon file '{path}' does not contain a Wagon block.");
            return metadata;
        }
    }
}

public sealed class EngineMetadata(string name)
{
    public string Name { get; set; } = name;
    public string? EngineType { get; set; }
    public float? MassKilograms { get; set; }
    public float? WidthMeters { get; set; }
    public float? HeightMeters { get; set; }
    public float? LengthMeters { get; set; }
    public float? MaximumBrakeForceNewtons { get; set; }
    public float? MaximumPowerWatts { get; set; }
    public float TransmissionEfficiency { get; set; } = 1f;
    public float? MaximumTractiveForceNewtons { get; set; }
    public float? MaximumContinuousForceNewtons { get; set; }
    public float? MaximumDynamicBrakeForceNewtons { get; set; }
    public float? MaximumSpeedMetersPerSecond { get; set; }
    public string? Description { get; set; }
}

public sealed class WagonMetadata(string name)
{
    public string Name { get; set; } = name;
    public string? WagonType { get; set; }
    public float? MassKilograms { get; set; }
    public float? WidthMeters { get; set; }
    public float? HeightMeters { get; set; }
    public float? LengthMeters { get; set; }
    public int? NumberOfAxles { get; set; }
    public string? BrakeSystemType { get; set; }
    public string? BrakeEquipmentType { get; set; }
    public float? MaximumBrakeForceNewtons { get; set; }
}

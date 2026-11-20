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
    private static readonly object FactoryLock = new();

    public EngineMetadata ReadEngine(IVirtualContentStore contentStore, string filePath)
    {
        ArgumentNullException.ThrowIfNull(contentStore);
        var path = VirtualPath.Normalize(filePath);
        if (!path.EndsWith(".eng", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("An engine metadata path must end in .eng.", nameof(filePath));

        lock (FactoryLock)
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
                        new STFReader.TokenProcessor("maxpower", () => metadata.MaximumPowerWatts = reader.ReadFloatBlock(STFReader.UNITS.Power, null)),
                        new STFReader.TokenProcessor("orts dieseltransmissionefficiency", () => metadata.TransmissionEfficiency = reader.ReadFloatBlock(STFReader.UNITS.None, 1f)),
                        new STFReader.TokenProcessor("maxforce", () => metadata.MaximumTractiveForceNewtons = reader.ReadFloatBlock(STFReader.UNITS.Force, null)),
                        new STFReader.TokenProcessor("maxcontinuousforce", () => metadata.MaximumContinuousForceNewtons = reader.ReadFloatBlock(STFReader.UNITS.Force, null)),
                        new STFReader.TokenProcessor("dynamicbrakesmaximumforce", () => metadata.MaximumDynamicBrakeForceNewtons = reader.ReadFloatBlock(STFReader.UNITS.Force, null)),
                        new STFReader.TokenProcessor("maxvelocity", () => metadata.MaximumSpeedMetersPerSecond = reader.ReadFloatBlock(STFReader.UNITS.Speed, null)),
                        new STFReader.TokenProcessor("description", () => metadata.Description = reader.ReadStringBlock(null))
                    });
                    found = true;
                })
            });

            if (!found)
                throw new InvalidDataException($"The engine file '{path}' does not contain an Engine block.");
            return metadata with
            {
                TransmissionEfficiency = Math.Clamp(metadata.TransmissionEfficiency, 0.001f, 1f)
            };
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

public sealed record EngineMetadata(
    string Name,
    string? EngineType = null,
    float? MaximumPowerWatts = null,
    float TransmissionEfficiency = 1f,
    float? MaximumTractiveForceNewtons = null,
    float? MaximumContinuousForceNewtons = null,
    float? MaximumDynamicBrakeForceNewtons = null,
    float? MaximumSpeedMetersPerSecond = null,
    string? Description = null);

public sealed record WagonMetadata(
    string Name,
    string? WagonType = null,
    float? MassKilograms = null,
    float? WidthMeters = null,
    float? HeightMeters = null,
    float? LengthMeters = null,
    int? NumberOfAxles = null,
    string? BrakeSystemType = null,
    string? BrakeEquipmentType = null,
    float? MaximumBrakeForceNewtons = null);

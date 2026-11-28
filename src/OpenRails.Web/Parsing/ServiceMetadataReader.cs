using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

/// <summary>Reads the original MSTS/Open Rails service_definition block.</summary>
public sealed class ServiceMetadataReader
{
    public ServiceMetadata Read(IVirtualContentStore store, string path)
    {
        ArgumentNullException.ThrowIfNull(store);
        var virtualPath = VirtualPath.Normalize(path);
        lock (StfReaderAccess.SyncRoot)
        {
            STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(store);
            using var reader = new STFReader(virtualPath, useTree: false);
            ServiceMetadata? result = null;
            reader.ParseFile(new[]
            {
                new STFReader.TokenProcessor("service_definition", () => result = ReadDefinition(reader, virtualPath))
            });
            return result ?? throw new InvalidDataException(
                $"Service file '{virtualPath}' does not contain a Service_Definition block.");
        }
    }

    private static ServiceMetadata ReadDefinition(STFReader reader, string path)
    {
        var serial = 0;
        string? name = null, trainConfig = null, pathId = null;
        var maxWheelAcceleration = 0f;
        var efficiency = 0.9f;
        var initialSpeed = 0f;
        reader.MustMatch("(");
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("serial", () => serial = reader.ReadIntBlock(serial)),
            new STFReader.TokenProcessor("name", () => name = reader.ReadStringBlock(null)),
            new STFReader.TokenProcessor("train_config", () => trainConfig = reader.ReadStringBlock(null)),
            new STFReader.TokenProcessor("pathid", () => pathId = reader.ReadStringBlock(null)),
            new STFReader.TokenProcessor("maxwheelacceleration", () =>
                maxWheelAcceleration = reader.ReadFloatBlock(STFReader.UNITS.Any, maxWheelAcceleration)),
            new STFReader.TokenProcessor("efficiency", () =>
                efficiency = reader.ReadFloatBlock(STFReader.UNITS.Any, efficiency)),
            new STFReader.TokenProcessor("timetable", () =>
            {
                reader.MustMatch("(");
                reader.ParseBlock(new[]
                {
                    new STFReader.TokenProcessor("startingspeed", () =>
                        initialSpeed = reader.ReadFloatBlock(STFReader.UNITS.Speed, initialSpeed))
                });
            })
        });

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(trainConfig) ||
            string.IsNullOrWhiteSpace(pathId))
            throw new InvalidDataException(
                $"Service file '{path}' must define Name, Train_Config, and PathID.");
        if (!float.IsFinite(maxWheelAcceleration) || maxWheelAcceleration < 0 ||
            !float.IsFinite(efficiency) || efficiency < 0 || efficiency > 1 ||
            !float.IsFinite(initialSpeed) || initialSpeed < 0)
            throw new InvalidDataException($"Service file '{path}' contains invalid operating parameters.");

        return new ServiceMetadata(path, serial, name, trainConfig, pathId,
            maxWheelAcceleration, efficiency, initialSpeed);
    }
}

/// <summary>Service metadata required to bind an AI/player service to its consist and route path.</summary>
public sealed record ServiceMetadata(
    string SourcePath,
    int Serial,
    string Name,
    string TrainConfig,
    string PathId,
    float MaximumWheelAcceleration,
    float Efficiency,
    float InitialSpeedMetersPerSecond);

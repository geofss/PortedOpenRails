// COPYRIGHT 2009-2015 by the Open Rails project.
// Browser-port adaptation of the upstream MSTS ConsistFile/Train_Config parser.
// Licensed under GPL-3.0-or-later.

using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

public sealed class ConsistFileReader
{
    private static readonly object FactoryLock = new();

    public ConsistDefinition Read(IVirtualContentStore contentStore, string consistFilePath, bool checkDuplicates = true)
    {
        ArgumentNullException.ThrowIfNull(contentStore);
        var virtualPath = VirtualPath.Normalize(consistFilePath);
        lock (FactoryLock)
        {
            STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(contentStore);
            using var reader = new STFReader(virtualPath, useTree: false);
            ConsistDefinition? consist = null;
            reader.ParseFile(new[]
            {
                new STFReader.TokenProcessor("train", () => consist = ReadTrainConfig(reader, checkDuplicates))
            });
            return consist ?? throw new InvalidDataException(
                $"The consist file '{virtualPath}' does not contain a Train block.");
        }
    }

    private static ConsistDefinition ReadTrainConfig(STFReader reader, bool checkDuplicates)
    {
        reader.MustMatch("(");
        ConsistDefinition? result = null;
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("traincfg", () => result = ReadTrainCfg(reader, checkDuplicates))
        });
        return result ?? throw new InvalidDataException("The consist file is missing TrainCfg.");
    }

    private static ConsistDefinition ReadTrainCfg(STFReader reader, bool checkDuplicates)
    {
        reader.MustMatch("(");
        var name = reader.ReadString();
        float durability = 1.0f;
        string? tcsParameters = null;
        ConsistMaximumVelocity? maximumVelocity = null;
        var vehicles = new List<ConsistVehicle>();
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("name", () => name = reader.ReadStringBlock(null)),
            new STFReader.TokenProcessor("durability", () => durability = reader.ReadFloatBlock(STFReader.UNITS.None, null)),
            new STFReader.TokenProcessor("maxvelocity", () => maximumVelocity = ReadMaximumVelocity(reader)),
            new STFReader.TokenProcessor("wagon", () => vehicles.Add(ReadVehicle(reader, false, false))),
            new STFReader.TokenProcessor("engine", () => vehicles.Add(ReadVehicle(reader, true, false))),
            new STFReader.TokenProcessor("ortseot", () => vehicles.Add(ReadVehicle(reader, false, true))),
            new STFReader.TokenProcessor("ortstraincontrolsystemparameters", () => tcsParameters = reader.ReadStringBlock(null))
        });

        var diagnostics = new List<string>();
        if (checkDuplicates)
        {
            foreach (var duplicate in vehicles.GroupBy(vehicle => vehicle.UiD).Where(group => group.Count() > 1))
                diagnostics.Add($"Duplicate vehicle UiD {duplicate.Key} occurs {duplicate.Count()} times in consist '{name}'.");
        }
        return new ConsistDefinition(name, durability, maximumVelocity, tcsParameters,
            Array.AsReadOnly(vehicles.ToArray()), Array.AsReadOnly(diagnostics.ToArray()));
    }

    private static ConsistMaximumVelocity ReadMaximumVelocity(STFReader reader)
    {
        reader.MustMatch("(");
        var a = reader.ReadFloat(STFReader.UNITS.Speed, null);
        var b = reader.ReadFloat(STFReader.UNITS.Speed, null);
        reader.MustMatch(")");
        return new ConsistMaximumVelocity(a, b);
    }

    private static ConsistVehicle ReadVehicle(STFReader reader, bool isEngine, bool isEot)
    {
        reader.MustMatch("(");
        var uid = 0;
        var name = string.Empty;
        var folder = string.Empty;
        var flip = false;
        var loads = new List<ConsistLoad>();
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("uid", () => uid = reader.ReadIntBlock(null)),
            new STFReader.TokenProcessor("flip", () => { reader.MustMatch("("); reader.MustMatch(")"); flip = true; }),
            new STFReader.TokenProcessor("enginedata", () => { reader.MustMatch("("); name = reader.ReadString(); folder = reader.ReadString(); reader.MustMatch(")"); }),
            new STFReader.TokenProcessor("wagondata", () => { reader.MustMatch("("); name = reader.ReadString(); folder = reader.ReadString(); reader.MustMatch(")"); }),
            new STFReader.TokenProcessor("eotdata", () => { reader.MustMatch("("); name = reader.ReadString(); folder = reader.ReadString(); reader.MustMatch(")"); isEot = true; }),
            new STFReader.TokenProcessor("loaddata", () => loads.Add(ReadLoad(reader)))
        });
        return new ConsistVehicle(uid, name, folder, isEngine, isEot, flip, Array.AsReadOnly(loads.ToArray()));
    }

    private static ConsistLoad ReadLoad(STFReader reader)
    {
        reader.MustMatch("(");
        var name = reader.ReadString();
        var folder = reader.ReadString();
        var positionToken = reader.ReadString();
        var position = Enum.TryParse<ConsistLoadPosition>(positionToken, true, out var parsedPosition)
            ? parsedPosition : ConsistLoadPosition.Rear;
        var stateToken = reader.ReadString();
        var state = Enum.TryParse<ConsistLoadState>(stateToken, true, out var parsedState)
            ? parsedState : ConsistLoadState.Random;
        reader.MustMatch(")");
        return new ConsistLoad(name, folder, position, state);
    }
}

public sealed record ConsistDefinition(
    string Name,
    float Durability,
    ConsistMaximumVelocity? MaximumVelocity,
    string? TrainControlSystemParametersFile,
    IReadOnlyList<ConsistVehicle> Vehicles,
    IReadOnlyList<string> Diagnostics);

public sealed record ConsistMaximumVelocity(float A, float B);

public sealed record ConsistVehicle(
    int UiD,
    string Name,
    string Folder,
    bool IsEngine,
    bool IsEot,
    bool Flip,
    IReadOnlyList<ConsistLoad> Loads);

public sealed record ConsistLoad(
    string Name,
    string Folder,
    ConsistLoadPosition Position,
    ConsistLoadState State);

public enum ConsistLoadPosition
{
    Rear,
    CenterRear,
    Center,
    CenterFront,
    Front,
    Above
}

public enum ConsistLoadState
{
    Random,
    Empty,
    Loaded
}

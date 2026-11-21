using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

public sealed record ConsistLoadData(string Name, string Folder, string Position, string? State);
public sealed record ConsistCar(string Name, string Folder, int Uid, bool IsEngine, bool IsEndOfTrain, bool Flipped, IReadOnlyList<ConsistLoadData>? Loads = null);
public sealed record ConsistSpeedLimit(float A, float B);
public sealed record ConsistMetadata(string Name, IReadOnlyList<ConsistCar> Cars, string SourcePath, float Durability = 1.0f, ConsistSpeedLimit? MaxVelocity = null, string? TrainControlSystemParametersFile = null, int Serial = 1, int NextWagonUid = 0);

/// <summary>Reads MSTS/Open Rails .con consists using the upstream STF parser and virtual package streams.</summary>
public sealed class ConsistReader
{
    private static readonly object FactoryLock = new();

    public ConsistMetadata Read(IVirtualContentStore store, string path)
    {
        ArgumentNullException.ThrowIfNull(store);
        var virtualPath = VirtualPath.Normalize(path);
        lock (FactoryLock)
        {
            STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(store);
            using var reader = new STFReader(virtualPath, useTree: false);
            string? consistName = null;
            var cars = new List<ConsistCar>();
            var durability = 1.0f;
            ConsistSpeedLimit? maxVelocity = null;
            string? trainControlSystemParametersFile = null;
            var serial = 1;
            var nextWagonUid = 0;
            reader.ParseFile(new[]
            {
                new STFReader.TokenProcessor("train", () =>
                {
                    reader.MustMatch("(");
                    reader.ParseBlock(new[]
                    {
                        new STFReader.TokenProcessor("traincfg", () => ReadTrainConfig(reader, cars, value => consistName = value, value => durability = value, value => maxVelocity = value, value => trainControlSystemParametersFile = value, value => serial = value, value => nextWagonUid = value))
                    });
                })
            });
            if (cars.Count == 0)
                throw new InvalidDataException($"The consist '{virtualPath}' contains no wagon or engine entries.");
            var name = string.IsNullOrWhiteSpace(consistName) ? Path.GetFileNameWithoutExtension(virtualPath) : consistName.Trim();
            return new ConsistMetadata(name, Array.AsReadOnly(cars.ToArray()), virtualPath, durability, maxVelocity, trainControlSystemParametersFile, serial, nextWagonUid);
        }
    }

    private static void ReadTrainConfig(STFReader reader, List<ConsistCar> cars, Action<string> setName, Action<float> setDurability, Action<ConsistSpeedLimit> setMaxVelocity, Action<string> setTcsFile, Action<int> setSerial, Action<int> setNextWagonUid)
    {
        reader.MustMatch("(");
        var name = reader.ReadString();
        if (!string.IsNullOrWhiteSpace(name)) setName(name);
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("name", () => setName(reader.ReadStringBlock(null))),
            new STFReader.TokenProcessor("serial", () => setSerial(reader.ReadIntBlock(null))),
            new STFReader.TokenProcessor("nextwagonuid", () => setNextWagonUid(reader.ReadIntBlock(null))),
            new STFReader.TokenProcessor("durability", () => setDurability(reader.ReadFloatBlock(STFReader.UNITS.None, null))),
            new STFReader.TokenProcessor("maxvelocity", () => setMaxVelocity(ReadMaxVelocity(reader))),
            new STFReader.TokenProcessor("ortstraincontrolsystemparameters", () => setTcsFile(reader.ReadStringBlock(null))),
            new STFReader.TokenProcessor("wagon", () => cars.Add(ReadCar(reader, false, false))),
            new STFReader.TokenProcessor("engine", () => cars.Add(ReadCar(reader, true, false))),
            new STFReader.TokenProcessor("ortseot", () => cars.Add(ReadCar(reader, false, true)))
        });
    }

    private static ConsistSpeedLimit ReadMaxVelocity(STFReader reader)
    {
        reader.MustMatch("(");
        var a = reader.ReadFloat(STFReader.UNITS.Speed, null);
        var b = reader.ReadFloat(STFReader.UNITS.Speed, null);
        reader.MustMatch(")");
        return new ConsistSpeedLimit(a, b);
    }

    private static ConsistCar ReadCar(STFReader reader, bool engine, bool eot)
    {
        reader.MustMatch("(");
        string? name = null;
        string? folder = null;
        var uid = 0;
        var flipped = false;
        var loads = new List<ConsistLoadData>();
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("uid", () => uid = reader.ReadIntBlock(null)),
            new STFReader.TokenProcessor("flip", () => { reader.MustMatch("("); reader.MustMatch(")"); flipped = true; }),
            new STFReader.TokenProcessor("enginedata", () => ReadCarData(reader, value => name = value, value => folder = value)),
            new STFReader.TokenProcessor("wagondata", () => ReadCarData(reader, value => name = value, value => folder = value)),
            new STFReader.TokenProcessor("eotdata", () => ReadCarData(reader, value => name = value, value => folder = value)),
            new STFReader.TokenProcessor("loaddata", () => loads.Add(ReadLoadData(reader)))
        });
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(folder))
            throw new InvalidDataException($"A consist car entry (UID {uid}) is missing its engine/wagon data.");
        return new ConsistCar(name, folder, uid, engine, eot, flipped, loads.Count == 0 ? null : Array.AsReadOnly(loads.ToArray()));
    }

    private static ConsistLoadData ReadLoadData(STFReader reader)
    {
        reader.MustMatch("(");
        var name = reader.ReadString();
        var folder = reader.ReadString();
        var position = reader.ReadString();
        var state = reader.ReadString();
        if (state == ")")
            return new ConsistLoadData(name, folder, position, null);
        reader.MustMatch(")");
        return new ConsistLoadData(name, folder, position, state);
    }

    private static void ReadCarData(STFReader reader, Action<string> setName, Action<string> setFolder)
    {
        reader.MustMatch("(");
        setName(reader.ReadString());
        setFolder(reader.ReadString());
        reader.MustMatch(")");
    }
}

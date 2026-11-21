using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

public sealed record ConsistCar(string Name, string Folder, int Uid, bool IsEngine, bool IsEndOfTrain, bool Flipped);
public sealed record ConsistMetadata(string Name, IReadOnlyList<ConsistCar> Cars, string SourcePath);

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
            reader.ParseFile(new[]
            {
                new STFReader.TokenProcessor("train", () =>
                    reader.ParseBlock(new[]
                    {
                        new STFReader.TokenProcessor("traincfg", () => ReadTrainConfig(reader, cars, value => consistName = value))
                    }))
            });
            if (cars.Count == 0)
                throw new InvalidDataException($"The consist '{virtualPath}' contains no wagon or engine entries.");
            var name = string.IsNullOrWhiteSpace(consistName) ? Path.GetFileNameWithoutExtension(virtualPath) : consistName.Trim();
            return new ConsistMetadata(name, Array.AsReadOnly(cars.ToArray()), virtualPath);
        }
    }

    private static void ReadTrainConfig(STFReader reader, List<ConsistCar> cars, Action<string> setName)
    {
        reader.MustMatch("(");
        var name = reader.ReadString();
        if (!string.IsNullOrWhiteSpace(name)) setName(name);
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("name", () => setName(reader.ReadStringBlock(null))),
            new STFReader.TokenProcessor("wagon", () => cars.Add(ReadCar(reader, false, false))),
            new STFReader.TokenProcessor("engine", () => cars.Add(ReadCar(reader, true, false))),
            new STFReader.TokenProcessor("ortseot", () => cars.Add(ReadCar(reader, false, true)))
        });
    }

    private static ConsistCar ReadCar(STFReader reader, bool engine, bool eot)
    {
        reader.MustMatch("(");
        string? name = null;
        string? folder = null;
        var uid = 0;
        var flipped = false;
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("uid", () => uid = reader.ReadIntBlock(null)),
            new STFReader.TokenProcessor("flip", () => { reader.MustMatch("("); reader.MustMatch(")"); flipped = true; }),
            new STFReader.TokenProcessor("enginedata", () => ReadCarData(reader, value => name = value, value => folder = value)),
            new STFReader.TokenProcessor("wagondata", () => ReadCarData(reader, value => name = value, value => folder = value)),
            new STFReader.TokenProcessor("eotdata", () => ReadCarData(reader, value => name = value, value => folder = value))
        });
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(folder))
            throw new InvalidDataException($"A consist car entry (UID {uid}) is missing its engine/wagon data.");
        return new ConsistCar(name, folder, uid, engine, eot, flipped);
    }

    private static void ReadCarData(STFReader reader, Action<string> setName, Action<string> setFolder)
    {
        reader.MustMatch("(");
        setName(reader.ReadString());
        setFolder(reader.ReadString());
        reader.MustMatch(")");
    }
}

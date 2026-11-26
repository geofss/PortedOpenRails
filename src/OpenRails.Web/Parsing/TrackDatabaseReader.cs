// COPYRIGHT 2009-2014 by the Open Rails project.
// This file is part of Open Rails and is licensed under GPL-3.0-or-later.
// Browser-port adaptation: parses the portable text track-database structures through
// the upstream STFReader and the browser virtual-content pipeline.

using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Reads the text-based MSTS/Open Rails track database (.tdb). The parser preserves
/// the original TrackNode / TrVectorNode / TrJunctionNode topology and section anchors.
/// Binary road databases and track-item payloads are deliberately outside this reader.
/// </summary>
public sealed class TrackDatabaseReader
{
    private const int MaximumTrackNodes = 5_000_000;
    private const int MaximumVectorSectionsPerNode = 1_000_000;

    public TrackDatabase Read(IVirtualContentStore store, string path)
    {
        ArgumentNullException.ThrowIfNull(store);
        var normalized = VirtualPath.Normalize(path);

        lock (StfReaderAccess.SyncRoot)
        {
            STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(store);
            using var reader = new STFReader(normalized, useTree: false);
            TrackDatabase? database = null;
            reader.ParseFile(new[]
            {
                new STFReader.TokenProcessor("trackdb", () => database = ReadTrackDb(reader))
            });

            return database ?? throw new InvalidDataException(
                $"The track database '{normalized}' does not contain a TrackDB block.");
        }
    }

    private static TrackDatabase ReadTrackDb(STFReader reader)
    {
        reader.MustMatch("(");
        TrackNode?[]? nodes = null;
        var trackItemCount = 0;
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("tracknodes", () =>
            {
                reader.MustMatch("(");
                var count = reader.ReadInt(null);
                if (count < 0 || count > MaximumTrackNodes)
                    throw new InvalidDataException($"Track database node count {count} is outside the supported range.");

                nodes = new TrackNode?[count + 1];
                var nextIndex = 1;
                reader.ParseBlock(new[]
                {
                    new STFReader.TokenProcessor("tracknode", () =>
                    {
                        var node = ReadTrackNode(reader);
                        if (nextIndex > count)
                            throw new InvalidDataException("Track database contains more TrackNode blocks than declared.");
                        if (node.Index != (uint)nextIndex)
                            throw new InvalidDataException($"TrackNode index mismatch: expected {nextIndex}, found {node.Index}.");
                        nodes[nextIndex++] = node;
                    })
                });

                if (nextIndex != count + 1)
                    throw new InvalidDataException($"Track database declares {count} nodes but contains {nextIndex - 1}.");
            }),
            new STFReader.TokenProcessor("tritemtable", () =>
            {
                reader.MustMatch("(");
                trackItemCount = reader.ReadInt(null);
                if (trackItemCount < 0 || trackItemCount > 5_000_000)
                    throw new InvalidDataException($"Track item count {trackItemCount} is outside the supported range.");
                // Track-item payloads are ported separately; retain the declared table size
                // so vector-node references can still be validated like upstream TrackDB.
                reader.SkipRestOfBlock();
            })
        });

        if (nodes is null)
            throw new InvalidDataException("Track database is missing TrackNodes.");

        var diagnostics = new List<string>();
        for (var i = 1; i < nodes.Length; i++)
        {
            var node = nodes[i]!;
            foreach (var pin in node.Pins)
            {
                if (pin.Link <= 0 || pin.Link >= nodes.Length || nodes[pin.Link] is null)
                    diagnostics.Add($"TrackNode {node.Index} has a pin to missing node {pin.Link}.");
            }
            foreach (var itemReference in node.TrackItemReferences)
            {
                if (itemReference < 0 || itemReference >= trackItemCount)
                    diagnostics.Add($"TrackNode {node.Index} has a reference to missing track item {itemReference} (table size {trackItemCount}).");
            }
        }

        return new TrackDatabase(nodes, trackItemCount, diagnostics);
    }

    private static TrackNode ReadTrackNode(STFReader reader)
    {
        reader.MustMatch("(");
        var index = reader.ReadUInt(null);
        TrackNodeKind kind = TrackNodeKind.Unknown;
        TrackNodeLocation? location = null;
        TrackJunctionData? junction = null;
        TrackVectorSection[] sections = Array.Empty<TrackVectorSection>();
        int[] itemReferences = Array.Empty<int>();
        var pins = Array.Empty<TrackPin>();

        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("uid", () => location = ReadUid(reader)),
            new STFReader.TokenProcessor("trjunctionnode", () =>
            {
                reader.MustMatch("(");
                var shapeName = reader.ReadString();
                var shapeIndex = reader.ReadUInt(null);
                reader.SkipRestOfBlock();
                junction = new TrackJunctionData(shapeName, shapeIndex);
                kind = TrackNodeKind.Junction;
            }),
            new STFReader.TokenProcessor("trvectornode", () =>
            {
                sections = ReadVectorNode(reader, out itemReferences);
                kind = TrackNodeKind.Vector;
            }),
            new STFReader.TokenProcessor("trendnode", () =>
            {
                reader.SkipBlock();
                kind = TrackNodeKind.End;
            }),
            new STFReader.TokenProcessor("trpins", () => pins = ReadPins(reader))
        });

        if (kind == TrackNodeKind.Unknown)
            throw new InvalidDataException($"TrackNode {index} has no recognized node type.");

        return new TrackNode(index, kind, location, junction, sections, pins, itemReferences);
    }

    private static TrackNodeLocation ReadUid(STFReader reader)
    {
        reader.MustMatch("(");
        var worldTileX = reader.ReadInt(null);
        var worldTileZ = reader.ReadInt(null);
        var worldId = reader.ReadInt(null);
        _ = reader.ReadInt(null);
        var tileX = reader.ReadInt(null);
        var tileZ = reader.ReadInt(null);
        var x = reader.ReadFloat(STFReader.UNITS.None, null);
        var y = reader.ReadFloat(STFReader.UNITS.None, null);
        var z = reader.ReadFloat(STFReader.UNITS.None, null);
        var ax = reader.ReadFloat(STFReader.UNITS.None, null);
        var ay = reader.ReadFloat(STFReader.UNITS.None, null);
        var az = reader.ReadFloat(STFReader.UNITS.None, null);
        reader.SkipRestOfBlock();
        return new TrackNodeLocation(worldTileX, worldTileZ, worldId, tileX, tileZ, x, y, z, ax, ay, az);
    }

    private static TrackPin[] ReadPins(STFReader reader)
    {
        reader.MustMatch("(");
        var incoming = reader.ReadInt(null);
        var outgoing = reader.ReadInt(null);
        if (incoming < 0 || outgoing < 0 || incoming + (long)outgoing > 16)
            throw new InvalidDataException("TrackNode pin counts are outside the supported range.");

        var pins = new TrackPin[incoming + outgoing];
        for (var i = 0; i < pins.Length; i++)
        {
            reader.MustMatch("TrPin");
            reader.MustMatch("(");
            var link = reader.ReadInt(null);
            var direction = reader.ReadInt(null);
            reader.SkipRestOfBlock();
            pins[i] = new TrackPin(link, direction);
        }
        reader.SkipRestOfBlock();
        return pins;
    }

    private static TrackVectorSection[] ReadVectorNode(STFReader reader, out int[] itemReferences)
    {
        var parsedItemReferences = Array.Empty<int>();
        reader.MustMatch("(");
        TrackVectorSection[]? sections = null;
        reader.ParseBlock(new[]
        {
            new STFReader.TokenProcessor("trvectorsections", () =>
            {
                reader.MustMatch("(");
                var count = reader.ReadInt(null);
                if (count < 0 || count > MaximumVectorSectionsPerNode)
                    throw new InvalidDataException($"Vector section count {count} is outside the supported range.");

                sections = new TrackVectorSection[count];
                for (var i = 0; i < count; i++)
                {
                    var sectionIndex = reader.ReadUInt(null);
                    var shapeIndex = reader.ReadUInt(null);
                    var worldFileTileX = reader.ReadInt(null);
                    var worldFileTileZ = reader.ReadInt(null);
                    var worldFileUid = reader.ReadUInt(null);
                    var flag1 = reader.ReadInt(null);
                    var flag2 = reader.ReadInt(null);
                    var legacyFlags = reader.ReadString();
                    var tileX = reader.ReadInt(null);
                    var tileZ = reader.ReadInt(null);
                    var x = reader.ReadFloat(STFReader.UNITS.None, null);
                    var y = reader.ReadFloat(STFReader.UNITS.None, null);
                    var z = reader.ReadFloat(STFReader.UNITS.None, null);
                    var ax = reader.ReadFloat(STFReader.UNITS.None, null);
                    var ay = reader.ReadFloat(STFReader.UNITS.None, null);
                    var az = reader.ReadFloat(STFReader.UNITS.None, null);

                    sections[i] = new TrackVectorSection(
                        sectionIndex, shapeIndex, worldFileTileX, worldFileTileZ, worldFileUid,
                        flag1, flag2, legacyFlags, tileX, tileZ, x, y, z, ax, ay, az);
                }
                reader.SkipRestOfBlock();
            }),
            new STFReader.TokenProcessor("tritemrefs", () =>
            {
                reader.MustMatch("(");
                var count = reader.ReadInt(null);
                if (count < 0 || count > 5_000_000)
                    throw new InvalidDataException($"Track item reference count {count} is outside the supported range.");
                var references = new List<int>(count);
                reader.ParseBlock(new[]
                {
                    new STFReader.TokenProcessor("tritemref", () =>
                    {
                        if (references.Count >= count)
                            throw new InvalidDataException("TrVectorNode contains more TrItemRef blocks than declared.");
                        references.Add(reader.ReadIntBlock(null));
                    })
                });
                if (references.Count != count)
                    throw new InvalidDataException($"TrVectorNode declares {count} track item references but contains {references.Count}.");
                parsedItemReferences = references.ToArray();
            })
        });

        itemReferences = parsedItemReferences;
        return sections ?? throw new InvalidDataException("TrVectorNode is missing TrVectorSections.");
    }
}

public enum TrackNodeKind
{
    Unknown,
    End,
    Junction,
    Vector
}

public sealed record TrackPin(int Link, int Direction);

public sealed record TrackNodeLocation(
    int WorldTileX, int WorldTileZ, int WorldId,
    int TileX, int TileZ,
    float X, float Y, float Z,
    float AX, float AY, float AZ);

public sealed record TrackJunctionData(string ShapeName, uint ShapeIndex);

public sealed record TrackVectorSection(
    uint SectionIndex,
    uint ShapeIndex,
    int WorldFileTileX,
    int WorldFileTileZ,
    uint WorldFileUid,
    int Flag1,
    int Flag2,
    string LegacyFlags,
    int TileX,
    int TileZ,
    float X,
    float Y,
    float Z,
    float AX,
    float AY,
    float AZ);

public sealed class TrackNode
{
    public uint Index { get; }
    public TrackNodeKind Kind { get; }
    public TrackNodeLocation? Location { get; }
    public TrackJunctionData? Junction { get; }
    public IReadOnlyList<TrackVectorSection> Sections { get; }
    public IReadOnlyList<TrackPin> Pins { get; }
    public IReadOnlyList<int> TrackItemReferences { get; }

    public TrackNode(uint index, TrackNodeKind kind, TrackNodeLocation? location,
        TrackJunctionData? junction, IEnumerable<TrackVectorSection> sections, IEnumerable<TrackPin> pins,
        IEnumerable<int>? trackItemReferences = null)
    {
        Index = index;
        Kind = kind;
        Location = location;
        Junction = junction;
        Sections = Array.AsReadOnly(sections.ToArray());
        Pins = Array.AsReadOnly(pins.ToArray());
        TrackItemReferences = Array.AsReadOnly((trackItemReferences ?? Array.Empty<int>()).ToArray());
    }
}

public sealed class TrackDatabase
{
    private readonly TrackNode?[] nodes;
    public int NodeCount => nodes.Length - 1;
    public int TrackItemCount { get; }
    public IReadOnlyList<string> Diagnostics { get; }

    public TrackDatabase(TrackNode?[] nodes, IEnumerable<string> diagnostics)
        : this(nodes, 0, diagnostics)
    {
    }

    public TrackDatabase(TrackNode?[] nodes, int trackItemCount, IEnumerable<string> diagnostics)
    {
        if (nodes.Length == 0 || nodes[0] is not null)
            throw new ArgumentException("Track database node array must reserve index zero.", nameof(nodes));
        this.nodes = (TrackNode?[])nodes.Clone();
        TrackItemCount = trackItemCount;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public bool TryGetNode(int index, out TrackNode? node)
    {
        node = index > 0 && index < nodes.Length ? nodes[index] : null;
        return node is not null;
    }

    public TrackNode GetRequiredNode(int index) =>
        TryGetNode(index, out var node)
            ? node!
            : throw new InvalidDataException($"Track node {index} does not exist.");

    public IEnumerable<TrackNode> EnumerateNodes()
    {
        for (var i = 1; i < nodes.Length; i++)
            if (nodes[i] is { } node)
                yield return node;
    }

    public IEnumerable<(TrackNode From, TrackNode To, TrackPin Pin)> EnumerateConnections()
    {
        foreach (var node in EnumerateNodes())
        foreach (var pin in node.Pins)
        {
            if (pin.Link > 0 && pin.Link < nodes.Length && nodes[pin.Link] is { } target)
                yield return (node, target, pin);
        }
    }
}

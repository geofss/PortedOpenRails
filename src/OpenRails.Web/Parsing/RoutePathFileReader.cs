using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Reads the original MSTS .pat TrackPDP and TrPathNode tables without flattening
/// their linked-node topology. The 0xFFFFFFFF next-node sentinel is preserved.
/// </summary>
public sealed class RoutePathFileReader
{
    public RoutePathDefinition Read(IVirtualContentStore store, string path)
    {
        ArgumentNullException.ThrowIfNull(store);
        var virtualPath = VirtualPath.Normalize(path);
        lock (StfReaderAccess.SyncRoot)
        {
            STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(store);
            using var reader = new STFReader(virtualPath, useTree: false);
            var pdps = new List<RoutePathPoint>();
            var nodes = new List<RoutePathNode>();
            string? pathId = null, name = null, start = null, end = null;
            uint flags = 0;
            int? declaredNodeCount = null;
            reader.ParseFile(new[]
            {
                new STFReader.TokenProcessor("trackpdps", () =>
                {
                    reader.MustMatch("(");
                    reader.ParseBlock(new[]
                    {
                        new STFReader.TokenProcessor("trackpdp", () => pdps.Add(ReadPoint(reader)))
                    });
                }),
                new STFReader.TokenProcessor("trackpath", () =>
                {
                    reader.MustMatch("(");
                    reader.ParseBlock(new[]
                    {
                        new STFReader.TokenProcessor("trpathname", () => pathId = reader.ReadStringBlock(null)),
                        new STFReader.TokenProcessor("name", () => name = reader.ReadStringBlock(null)),
                        new STFReader.TokenProcessor("trpathflags", () => flags = reader.ReadHexBlock(0)),
                        new STFReader.TokenProcessor("trpathstart", () => start = reader.ReadStringBlock(null)),
                        new STFReader.TokenProcessor("trpathend", () => end = reader.ReadStringBlock(null)),
                        new STFReader.TokenProcessor("trpathnodes", () =>
                        {
                            reader.MustMatch("(");
                            var count = reader.ReadInt(null);
                            if (count < 0 || count > 1_000_000)
                                throw new InvalidDataException($"Path '{virtualPath}' declares an invalid TrPathNodes count.");
                            declaredNodeCount = count;
                            reader.ParseBlock(new[]
                            {
                                new STFReader.TokenProcessor("trpathnode", () =>
                                {
                                    if (nodes.Count >= count)
                                    {
                                        reader.SkipRestOfBlock();
                                        return;
                                    }
                                    nodes.Add(ReadNode(reader));
                                })
                            });
                        })
                    });
                })
            });

            if (string.IsNullOrWhiteSpace(pathId) || string.IsNullOrWhiteSpace(name) ||
                nodes.Count == 0 || declaredNodeCount is null)
                throw new InvalidDataException($"Path file '{virtualPath}' is missing TrackPath identity or TrPathNodes.");
            if (declaredNodeCount.Value != nodes.Count)
                throw new InvalidDataException($"Path '{virtualPath}' declares {declaredNodeCount.Value} nodes but contains {nodes.Count}.");

            var diagnostics = new List<string>();
            for (var index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];
                if (node.FromPdpIndex >= pdps.Count)
                    diagnostics.Add($"Node {index} references missing TrackPDP {node.FromPdpIndex}.");
                ValidateNext(node.NextMainNode, "main", index, nodes.Count, diagnostics);
                ValidateNext(node.NextSidingNode, "siding", index, nodes.Count, diagnostics);
            }
            return new RoutePathDefinition(virtualPath, pathId, name, start ?? "", end ?? "",
                flags, pdps, nodes, diagnostics);
        }
    }

    private static RoutePathPoint ReadPoint(STFReader reader)
    {
        reader.MustMatch("(");
        var tileX = reader.ReadInt(null);
        var tileZ = reader.ReadInt(null);
        var x = reader.ReadFloat(STFReader.UNITS.None, null);
        var y = reader.ReadFloat(STFReader.UNITS.None, null);
        var z = reader.ReadFloat(STFReader.UNITS.None, null);
        var junctionFlag = reader.ReadInt(null);
        var invalidFlag = reader.ReadInt(null);
        reader.SkipRestOfBlock();
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
            throw new InvalidDataException("TrackPDP coordinates must be finite.");
        return new RoutePathPoint(tileX, tileZ, x, y, z, junctionFlag, invalidFlag);
    }

    private static RoutePathNode ReadNode(STFReader reader)
    {
        reader.MustMatch("(");
        var flags = reader.ReadHex(0);
        var nextMain = reader.ReadUInt(null);
        var nextSiding = reader.ReadUInt(null);
        var fromPdp = reader.ReadUInt(null);
        reader.SkipRestOfBlock();
        return new RoutePathNode(flags, nextMain, nextSiding, fromPdp);
    }

    private static void ValidateNext(uint next, string branch, int source, int count, List<string> diagnostics)
    {
        if (next != uint.MaxValue && next >= count)
            diagnostics.Add($"Node {source} has an out-of-range {branch} successor {next}.");
    }
}

public sealed record RoutePathPoint(
    int TileX, int TileZ, float X, float Y, float Z, int JunctionFlag, int InvalidFlag)
{
    public bool IsJunction => JunctionFlag == 2;
    public bool IsInvalid => InvalidFlag == 9 || InvalidFlag == 12;
}

public sealed record RoutePathNode(uint Flags, uint NextMainNode, uint NextSidingNode, uint FromPdpIndex)
{
    public bool HasNextMainNode => NextMainNode != uint.MaxValue;
    public bool HasNextSidingNode => NextSidingNode != uint.MaxValue;
    public bool IsReversalPoint => (Flags & 1) != 0;
    public bool IsWaitingPoint => (Flags & 2) != 0;
    public bool IsIntermediatePoint => (Flags & 4) != 0;
}

public sealed record RoutePathDefinition(
    string SourcePath, string PathId, string Name, string Start, string End, uint Flags,
    IReadOnlyList<RoutePathPoint> TrackPoints, IReadOnlyList<RoutePathNode> Nodes,
    IReadOnlyList<string> Diagnostics)
{
    public bool IsPlayerPath => (Flags & 0x20) == 0;

    /// <summary>Follows the native main-node links beginning at TrPathNodes[0].</summary>
    public RoutePathTrace TraceMainRoute() => Trace(useSidingLinks: false);

    /// <summary>Follows siding links where present, falling back to main links.</summary>
    public RoutePathTrace TraceSidingRoute() => Trace(useSidingLinks: true);

    private RoutePathTrace Trace(bool useSidingLinks)
    {
        var ordered = new List<int>();
        var visited = new HashSet<uint>();
        var diagnostics = new List<string>();
        uint current = 0;
        while (current != uint.MaxValue)
        {
            if (current >= Nodes.Count)
            {
                diagnostics.Add($"Path link points to missing node {current}.");
                break;
            }
            if (!visited.Add(current))
            {
                diagnostics.Add($"Path link cycle detected at node {current}.");
                break;
            }

            ordered.Add((int)current);
            var node = Nodes[(int)current];
            if (node.FromPdpIndex >= TrackPoints.Count)
                diagnostics.Add($"Node {current} cannot be positioned because TrackPDP {node.FromPdpIndex} is missing.");
            var next = useSidingLinks && node.HasNextSidingNode
                ? node.NextSidingNode
                : node.NextMainNode;
            current = next;
        }

        return new RoutePathTrace(
            Array.AsReadOnly(ordered.ToArray()),
            Array.AsReadOnly(ordered.Where(index => Nodes[index].FromPdpIndex < TrackPoints.Count)
                .Select(index => TrackPoints[(int)Nodes[index].FromPdpIndex]).ToArray()),
            Array.AsReadOnly(diagnostics.ToArray()));
    }
}

public sealed record RoutePathTrace(
    IReadOnlyList<int> NodeIndices,
    IReadOnlyList<RoutePathPoint> Points,
    IReadOnlyList<string> Diagnostics)
{
    public bool IsComplete => Diagnostics.Count == 0;
}

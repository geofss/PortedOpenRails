// COPYRIGHT 2009-2014 by the Open Rails project.
// This file is part of Open Rails and is licensed under GPL-3.0-or-later.
// Browser-port adaptation builds route topology from the original MSTS track database.

using OpenRails.Web.Parsing;

namespace OpenRails.Web.Simulation;

/// <summary>
/// Builds a browser-independent route track network from the real TDB topology and
/// TSECTION geometry catalogs. Track-node links remain the authoritative connectivity;
/// vector-section records remain the authoritative placement anchors.
/// </summary>
public sealed class TrackNetworkBuilder
{
    public const double TileSizeMeters = 2048d;

    public TrackNetwork Build(
        TrackDatabase database,
        TrackSectionCatalog globalSections,
        TrackSectionCatalog? routeSections = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(globalSections);

        var paths = new List<TrackVectorPath>();
        var diagnostics = new List<string>(database.Diagnostics);

        foreach (var node in database.EnumerateNodes())
        {
            if (node.Kind != TrackNodeKind.Vector)
                continue;

            if (node.Sections.Count == 0)
            {
                diagnostics.Add($"Vector TrackNode {node.Index} contains no vector sections.");
                continue;
            }

            var placements = new List<TrackSectionPlacement>(node.Sections.Count);
            double totalLength = 0;

            foreach (var section in node.Sections)
            {
                var geometry = ResolveGeometry(section.SectionIndex, globalSections, routeSections);
                var length = geometry.CalculatedLengthMeters;
                if (!double.IsFinite(length) || length <= 0)
                    throw new InvalidDataException(
                        $"TrackNode {node.Index} references track section {section.SectionIndex} with invalid length.");

                var position = new TrackWorldPosition(
                    section.TileX * TileSizeMeters + section.X,
                    section.Y,
                    section.TileZ * TileSizeMeters + section.Z);

                placements.Add(new TrackSectionPlacement(section, geometry, position, length));
                totalLength += length;
            }

            var endpoints = node.Pins
                .Select(pin => pin.Link)
                .Where(link => link > 0)
                .Distinct()
                .ToArray();

            if (endpoints.Length != 2)
                diagnostics.Add($"Vector TrackNode {node.Index} has {endpoints.Length} distinct connected nodes; expected 2.");

            paths.Add(new TrackVectorPath(
                node.Index,
                endpoints,
                placements,
                totalLength));
        }

        return new TrackNetwork(database, paths, diagnostics);
    }

    private static TrackSectionGeometry ResolveGeometry(
        uint sectionIndex,
        TrackSectionCatalog globalSections,
        TrackSectionCatalog? routeSections)
    {
        if (routeSections is not null && routeSections.TryGet(sectionIndex, out var routeGeometry))
            return routeGeometry;
        if (globalSections.TryGet(sectionIndex, out var globalGeometry))
            return globalGeometry;

        throw new InvalidDataException(
            $"Track section {sectionIndex} referenced by the TDB is missing from global and route-specific TSECTION catalogs.");
    }
}

public sealed record TrackWorldPosition(double X, double Y, double Z);

public sealed record TrackSectionPlacement(
    TrackVectorSection Source,
    TrackSectionGeometry Geometry,
    TrackWorldPosition Start,
    double LengthMeters);

public sealed class TrackVectorPath
{
    public uint TrackNodeIndex { get; }
    public IReadOnlyList<int> ConnectedNodeIndices { get; }
    public IReadOnlyList<TrackSectionPlacement> Sections { get; }
    public double LengthMeters { get; }

    public TrackVectorPath(uint trackNodeIndex, IEnumerable<int> connectedNodeIndices,
        IEnumerable<TrackSectionPlacement> sections, double lengthMeters)
    {
        TrackNodeIndex = trackNodeIndex;
        ConnectedNodeIndices = Array.AsReadOnly(connectedNodeIndices.ToArray());
        Sections = Array.AsReadOnly(sections.ToArray());
        LengthMeters = lengthMeters;
    }
}

public sealed class TrackNetwork
{
    public TrackDatabase Database { get; }
    public IReadOnlyList<TrackVectorPath> VectorPaths { get; }
    public IReadOnlyList<string> Diagnostics { get; }
    public int VectorPathCount => VectorPaths.Count;
    public double TotalTrackLengthMeters => VectorPaths.Sum(path => path.LengthMeters);

    public TrackNetwork(TrackDatabase database, IEnumerable<TrackVectorPath> vectorPaths,
        IEnumerable<string> diagnostics)
    {
        Database = database;
        VectorPaths = Array.AsReadOnly(vectorPaths.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }
}

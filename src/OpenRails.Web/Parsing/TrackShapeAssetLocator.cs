using OpenRails.Web.Content;
using OpenRails.Web.Simulation;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Resolves the original shape filenames referenced by TSECTION TrackShapes. Route
/// shapes are checked before global shapes, matching the layered content model.
/// </summary>
public sealed class TrackShapeAssetLocator
{
    public TrackShapeAssetDiscovery Locate(IVirtualContentStore store, string routeRoot, TrackNetwork network)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(network);

        var root = VirtualPath.Normalize(routeRoot);
        var available = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in store.Paths)
            available.TryAdd(path, path);

        var found = new List<TrackShapeAssetReference>();
        var missing = new List<TrackShapeAssetIssue>();
        var shapes = network.VectorPaths
            .SelectMany(path => path.Sections)
            .Select(section => section.Shape)
            .Where(shape => shape is not null && !string.IsNullOrWhiteSpace(shape.FileName))
            .Select(shape => shape!)
            .DistinctBy(shape => shape.ShapeIndex)
            .OrderBy(shape => shape.ShapeIndex);

        foreach (var shape in shapes)
        {
            var fileName = shape.FileName!.Replace('\\', '/');
            if (fileName.StartsWith('/') || fileName.Contains(':') ||
                fileName.Split('/').Any(segment => segment is "." or ".."))
            {
                missing.Add(new TrackShapeAssetIssue(shape.ShapeIndex, shape.FileName,
                    "Shape filename is not a safe virtual relative path."));
                continue;
            }

            var candidates = new[]
            {
                fileName.StartsWith("GLOBAL/", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("ROUTES/", StringComparison.OrdinalIgnoreCase)
                    ? fileName
                    : null,
                $"{root}/SHAPES/{fileName}",
                $"GLOBAL/SHAPES/{fileName}"
            };

            var resolved = candidates
                .Where(candidate => candidate is not null)
                .Select(candidate => available.TryGetValue(candidate!, out var actual) ? actual : null)
                .FirstOrDefault(actual => actual is not null);

            if (resolved is null)
                missing.Add(new TrackShapeAssetIssue(shape.ShapeIndex, shape.FileName,
                    "Original track shape file was not found in route or global SHAPES content."));
            else
                found.Add(new TrackShapeAssetReference(shape.ShapeIndex, shape.FileName, resolved));
        }

        return new TrackShapeAssetDiscovery(found, missing);
    }
}

public sealed record TrackShapeAssetReference(uint ShapeIndex, string FileName, string Path);
public sealed record TrackShapeAssetIssue(uint ShapeIndex, string FileName, string Reason);

public sealed class TrackShapeAssetDiscovery
{
    public IReadOnlyList<TrackShapeAssetReference> Found { get; }
    public IReadOnlyList<TrackShapeAssetIssue> Missing { get; }

    public TrackShapeAssetDiscovery(IEnumerable<TrackShapeAssetReference> found,
        IEnumerable<TrackShapeAssetIssue> missing)
    {
        Found = Array.AsReadOnly(found.ToArray());
        Missing = Array.AsReadOnly(missing.ToArray());
    }
}

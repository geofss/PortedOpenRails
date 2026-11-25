using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Locates the real route data files referenced by a selected TRK file within the
/// virtual content namespace. Matching is case-insensitive, as in MSTS content.
/// </summary>
public sealed class RouteAssetLocator
{
    public RouteAssetPaths Locate(IVirtualContentStore store, string routeFilePath, RouteMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(metadata);

        var normalizedRoutePath = VirtualPath.Normalize(routeFilePath);
        if (!store.Contains(normalizedRoutePath))
            throw new FileNotFoundException($"The selected route metadata file was not found: {normalizedRoutePath}", normalizedRoutePath);

        var separator = normalizedRoutePath.LastIndexOf('/');
        if (separator < 0)
            throw new InvalidDataException("The selected TRK file is not inside a route directory.");

        var routeRoot = normalizedRoutePath[..separator];
        var routePrefix = routeRoot + "/";
        var trackDatabaseName = string.IsNullOrWhiteSpace(metadata.FileName)
            ? metadata.RouteId
            : metadata.FileName;

        var routeFiles = store.Paths
            .Where(path => path.StartsWith(routePrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var trackDatabaseCandidates = routeFiles
            .Where(path => path.EndsWith(".tdb", StringComparison.OrdinalIgnoreCase))
            .Where(path => string.Equals(GetFileNameWithoutExtension(path), trackDatabaseName,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (trackDatabaseCandidates.Length == 0)
            throw new FileNotFoundException(
                $"Could not find the route track database '{trackDatabaseName}.tdb' below '{routeRoot}'.");
        if (trackDatabaseCandidates.Length > 1)
            throw new InvalidDataException(
                $"More than one track database matches '{trackDatabaseName}.tdb' below '{routeRoot}'.");

        var globalTrackSections = store.Paths
            .Where(path => path.EndsWith("/GLOBAL/TSECTION.DAT", StringComparison.OrdinalIgnoreCase)
                || path.Equals("GLOBAL/TSECTION.DAT", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (globalTrackSections.Length != 1)
            throw new FileNotFoundException(
                globalTrackSections.Length == 0
                    ? "Could not find GLOBAL/TSECTION.DAT in the imported content."
                    : "More than one GLOBAL/TSECTION.DAT file is visible in the content packages.");

        var routeTrackSectionCandidates = routeFiles
            .Where(path => GetFileName(path).Equals("TSECTION.DAT", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path.Length)
            .ToArray();
        if (routeTrackSectionCandidates.Length > 1)
            throw new InvalidDataException($"More than one route-specific TSECTION.DAT file is visible below '{routeRoot}'.");

        var routeTrackSections = routeTrackSectionCandidates.SingleOrDefault();

        return new RouteAssetPaths(
            routeRoot,
            trackDatabaseCandidates[0],
            globalTrackSections[0],
            routeTrackSections);
    }

    private static string GetFileName(string path)
    {
        var separator = path.LastIndexOf('/');
        return separator < 0 ? path : path[(separator + 1)..];
    }

    private static string GetFileNameWithoutExtension(string path)
    {
        var fileName = GetFileName(path);
        var extension = fileName.LastIndexOf('.');
        return extension < 0 ? fileName : fileName[..extension];
    }
}

public sealed record RouteAssetPaths(
    string RouteRoot,
    string TrackDatabasePath,
    string GlobalTrackSectionsPath,
    string? RouteTrackSectionsPath);

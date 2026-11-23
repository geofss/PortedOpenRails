namespace OpenRails.Web.Parsing;

/// <summary>Locates activity files associated with a selected route folder.</summary>
public static class ActivityFileLocator
{
    public static IReadOnlyList<string> FindForRoute(IEnumerable<string> virtualPaths, string routeMetadataPath)
    {
        ArgumentNullException.ThrowIfNull(virtualPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeMetadataPath);
        var normalizedRoute = VirtualPath.Normalize(routeMetadataPath);
        var routeSegments = normalizedRoute.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var routesIndex = Array.FindIndex(routeSegments, segment =>
            string.Equals(segment, "ROUTES", StringComparison.OrdinalIgnoreCase));
        if (routesIndex < 0 || routesIndex + 1 >= routeSegments.Length)
            return Array.Empty<string>();

        var routeFolder = string.Join('/', routeSegments.Take(routesIndex + 2));
        var activityPrefix = routeFolder + "/ACTIVITIES/";
        return Array.AsReadOnly(virtualPaths
            .Select(VirtualPath.Normalize)
            .Where(path => path.StartsWith(activityPrefix, StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith(".act", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }
}

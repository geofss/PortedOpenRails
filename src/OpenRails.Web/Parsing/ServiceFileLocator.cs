using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>Locates global service definitions and path files for a selected route.</summary>
public static class ServiceFileLocator
{
    public static IReadOnlyList<string> FindServices(IEnumerable<string> virtualPaths)
    {
        ArgumentNullException.ThrowIfNull(virtualPaths);
        return Array.AsReadOnly(virtualPaths.Select(VirtualPath.Normalize)
            .Where(path => path.StartsWith("TRAINS/SERVICES/", StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith(".srv", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public static IReadOnlyList<string> FindPaths(IEnumerable<string> virtualPaths, string routeMetadataPath)
    {
        ArgumentNullException.ThrowIfNull(virtualPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeMetadataPath);
        var segments = VirtualPath.Normalize(routeMetadataPath).Split('/', StringSplitOptions.RemoveEmptyEntries);
        var routesIndex = Array.FindIndex(segments, segment =>
            string.Equals(segment, "ROUTES", StringComparison.OrdinalIgnoreCase));
        if (routesIndex < 0 || routesIndex + 1 >= segments.Length)
            return Array.Empty<string>();
        var prefix = string.Join('/', segments.Take(routesIndex + 2)) + "/PATHS/";
        return Array.AsReadOnly(virtualPaths.Select(VirtualPath.Normalize)
            .Where(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith(".pat", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray());
    }
}

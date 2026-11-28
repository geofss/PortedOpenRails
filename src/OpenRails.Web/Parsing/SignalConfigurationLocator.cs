using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>Finds the SIGCFG file directly inside the selected route directory, case-insensitively.</summary>
public static class SignalConfigurationLocator
{
    public static string? FindForRoute(IEnumerable<string> virtualPaths, string routeRoot)
    {
        ArgumentNullException.ThrowIfNull(virtualPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeRoot);
        var root = VirtualPath.Normalize(routeRoot).TrimEnd('/');
        return virtualPaths.Select(VirtualPath.Normalize)
            .FirstOrDefault(path =>
            {
                var separator = path.LastIndexOf('/');
                var directory = separator < 0 ? "" : path[..separator];
                var filename = separator < 0 ? path : path[(separator + 1)..];
                return string.Equals(directory, root, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(filename, "sigcfg.dat", StringComparison.OrdinalIgnoreCase);
            });
    }
}

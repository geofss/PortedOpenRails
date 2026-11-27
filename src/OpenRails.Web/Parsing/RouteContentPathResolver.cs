using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Resolves route and global assets when an imported simulator installation is
/// wrapped in a parent directory (for example "Demo Model 1/GLOBAL/...").
/// </summary>
internal static class RouteContentPathResolver
{
    public static string? Resolve(
        IVirtualContentStore store,
        string routeRoot,
        string routeFolder,
        string globalFolder,
        string relativeFileName)
    {
        ArgumentNullException.ThrowIfNull(store);
        var root = VirtualPath.Normalize(routeRoot);
        var name = relativeFileName.Replace('\\', '/');
        var packageRoot = GetPackageRoot(root);
        var globalRelative = name.StartsWith(globalFolder + "/", StringComparison.OrdinalIgnoreCase)
            ? name[(globalFolder.Length + 1)..]
            : name;
        var candidates = new List<string>();
        if (name.StartsWith("GLOBAL/", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("ROUTES/", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(name);
            if (packageRoot.Length > 0)
                candidates.Add(packageRoot + "/" + name);
        }
        else
        {
            candidates.Add(root + "/" + routeFolder + "/" + name);
            candidates.Add((packageRoot.Length > 0 ? packageRoot + "/" : string.Empty) +
                globalFolder + "/" + globalRelative);
            candidates.Add(globalFolder + "/" + globalRelative);
        }

        var paths = store.Paths;
        foreach (var candidate in candidates)
        {
            var match = paths.FirstOrDefault(path =>
                StringComparer.OrdinalIgnoreCase.Equals(path, candidate));
            if (match is not null)
                return match;
        }

        var suffix = "/" + globalFolder + "/" + globalRelative;
        return paths.Where(path => path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path.Length)
            .FirstOrDefault();
    }

    private static string GetPackageRoot(string routeRoot)
    {
        var marker = routeRoot.IndexOf("/ROUTES/", StringComparison.OrdinalIgnoreCase);
        return marker > 0 ? routeRoot[..marker] : string.Empty;
    }
}

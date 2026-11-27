namespace OpenRails.Web.Parsing;

/// <summary>Resolves .srv references to both their native .pat route path and .con consist.</summary>
public static class ServiceRouteBindingResolver
{
    public static IReadOnlyList<ServiceRouteBinding> Resolve(
        IEnumerable<ServiceMetadata> services,
        IEnumerable<RoutePathDefinition> paths,
        IEnumerable<ConsistMetadata> consists)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(consists);
        var pathIndex = paths.GroupBy(path => path.PathId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var consistIndex = consists.SelectMany(consist => new[]
            {
                (Key: Path.GetFileNameWithoutExtension(consist.SourcePath), Consist: consist),
                (Key: consist.Name, Consist: consist)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key,
                group => group.Select(item => item.Consist).Distinct().ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return Array.AsReadOnly(services.Select(service =>
        {
            var diagnostics = new List<string>();
            RoutePathDefinition? path = null;
            if (!pathIndex.TryGetValue(service.PathId, out var matchingPaths))
                diagnostics.Add($"Service '{service.Name}' references missing route path '{service.PathId}'.");
            else if (matchingPaths.Length != 1)
                diagnostics.Add($"Service '{service.Name}' references ambiguous route path ID '{service.PathId}' ({matchingPaths.Length} matches).");
            else
                path = matchingPaths[0];

            ConsistMetadata? consist = null;
            if (!consistIndex.TryGetValue(service.TrainConfig, out var matchingConsists))
                diagnostics.Add($"Service '{service.Name}' references missing consist '{service.TrainConfig}'.");
            else if (matchingConsists.Length != 1)
                diagnostics.Add($"Service '{service.Name}' references ambiguous consist '{service.TrainConfig}' ({matchingConsists.Length} matches).");
            else
                consist = matchingConsists[0];
            var trace = path?.TraceMainRoute();
            if (trace is not null) diagnostics.AddRange(trace.Diagnostics);
            return new ServiceRouteBinding(service, path, trace, consist,
                diagnostics.Count == 0 ? null : string.Join(" ", diagnostics));
        }).ToArray());
    }
}

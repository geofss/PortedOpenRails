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
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var consistIndex = consists.SelectMany(consist => new[]
            {
                (Key: Path.GetFileNameWithoutExtension(consist.SourcePath), Consist: consist),
                (Key: consist.Name, Consist: consist)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Consist, StringComparer.OrdinalIgnoreCase);

        return Array.AsReadOnly(services.Select(service =>
        {
            pathIndex.TryGetValue(service.PathId, out var path);
            consistIndex.TryGetValue(service.TrainConfig, out var consist);
            var diagnostics = new List<string>();
            if (path is null)
                diagnostics.Add($"Service '{service.Name}' references missing route path '{service.PathId}'.");
            if (consist is null)
                diagnostics.Add($"Service '{service.Name}' references missing consist '{service.TrainConfig}'.");
            var trace = path?.TraceMainRoute();
            if (trace is not null) diagnostics.AddRange(trace.Diagnostics);
            return new ServiceRouteBinding(service, path, trace, consist,
                diagnostics.Count == 0 ? null : string.Join(" ", diagnostics));
        }).ToArray());
    }
}

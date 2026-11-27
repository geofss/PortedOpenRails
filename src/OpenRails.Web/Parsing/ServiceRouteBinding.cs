namespace OpenRails.Web.Parsing;

/// <summary>Connects an original .srv service to its route's native .pat path graph and .con consist.</summary>
public sealed record ServiceRouteBinding(
    ServiceMetadata Service,
    RoutePathDefinition? Path,
    RoutePathTrace? MainTrace,
    ConsistMetadata? Consist,
    string? Diagnostic)
{
    public bool IsRunnable => Path is not null && MainTrace is { IsComplete: true } &&
        Consist is not null && Diagnostic is null;
}

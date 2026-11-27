namespace OpenRails.Web.Parsing;

/// <summary>Connects an original .srv service to its route's native .pat path graph.</summary>
public sealed record ServiceRouteBinding(
    ServiceMetadata Service,
    RoutePathDefinition? Path,
    RoutePathTrace? MainTrace,
    string? Diagnostic);

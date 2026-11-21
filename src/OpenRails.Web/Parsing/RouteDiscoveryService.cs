namespace OpenRails.Web.Parsing;

using OpenRails.Web.Content;

/// <summary>
/// Discovers route metadata files in a virtual Open Rails content namespace.
/// Invalid candidates are reported without preventing other routes from loading.
/// </summary>
public sealed class RouteDiscoveryService
{
    private readonly RouteMetadataReader metadataReader;

    public RouteDiscoveryService(RouteMetadataReader? metadataReader = null)
    {
        this.metadataReader = metadataReader ?? new RouteMetadataReader();
    }

    public RouteDiscoveryResult Discover(IVirtualContentStore contentStore)
    {
        ArgumentNullException.ThrowIfNull(contentStore);

        var routes = new List<DiscoveredRoute>();
        var failures = new List<RouteDiscoveryFailure>();

        var candidates = contentStore.Paths
            .Where(path => path.EndsWith(".trk", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (var path in candidates)
        {
            try
            {
                routes.Add(new DiscoveredRoute(path, metadataReader.Read(contentStore, path)));
            }
            catch (Exception exception) when (
                exception is InvalidDataException ||
                exception is IOException ||
                exception is ArgumentException ||
                exception is FormatException)
            {
                failures.Add(new RouteDiscoveryFailure(path, exception.Message));
            }
        }

        return new RouteDiscoveryResult(routes.AsReadOnly(), failures.AsReadOnly());
    }
}

public sealed record DiscoveredRoute(string MetadataFile, RouteMetadata Metadata);

public sealed record RouteDiscoveryFailure(string MetadataFile, string Reason);

public sealed record RouteDiscoveryResult(
    IReadOnlyList<DiscoveredRoute> Routes,
    IReadOnlyList<RouteDiscoveryFailure> Failures)
{
    public bool HasRoutes => Routes.Count > 0;
}

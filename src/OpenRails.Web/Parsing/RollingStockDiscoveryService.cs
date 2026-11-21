namespace OpenRails.Web.Parsing;

using OpenRails.Web.Content;

/// <summary>
/// Discovers and reads locomotive and wagon definitions from layered virtual content.
/// Parsing failures are isolated so one broken vehicle file does not hide the others.
/// </summary>
public sealed class RollingStockDiscoveryService
{
    private readonly RollingStockMetadataReader metadataReader;

    public RollingStockDiscoveryService(RollingStockMetadataReader? metadataReader = null)
    {
        this.metadataReader = metadataReader ?? new RollingStockMetadataReader();
    }

    public RollingStockDiscoveryResult Discover(IVirtualContentStore contentStore)
    {
        ArgumentNullException.ThrowIfNull(contentStore);

        var engines = new List<DiscoveredEngine>();
        var wagons = new List<DiscoveredWagon>();
        var failures = new List<RollingStockDiscoveryFailure>();

        foreach (var path in contentStore.Paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (path.EndsWith(".eng", StringComparison.OrdinalIgnoreCase))
                    engines.Add(new DiscoveredEngine(path, metadataReader.ReadEngine(contentStore, path)));
                else if (path.EndsWith(".wag", StringComparison.OrdinalIgnoreCase))
                    wagons.Add(new DiscoveredWagon(path, metadataReader.ReadWagon(contentStore, path)));
            }
            catch (Exception exception) when (
                exception is InvalidDataException ||
                exception is IOException ||
                exception is ArgumentException ||
                exception is FormatException ||
                exception is OverflowException)
            {
                failures.Add(new RollingStockDiscoveryFailure(path, exception.Message));
            }
        }

        return new RollingStockDiscoveryResult(
            engines.AsReadOnly(),
            wagons.AsReadOnly(),
            failures.AsReadOnly());
    }
}

public sealed record DiscoveredEngine(string DefinitionFile, EngineMetadata Metadata);
public sealed record DiscoveredWagon(string DefinitionFile, WagonMetadata Metadata);
public sealed record RollingStockDiscoveryFailure(string DefinitionFile, string Reason);

public sealed record RollingStockDiscoveryResult(
    IReadOnlyList<DiscoveredEngine> Engines,
    IReadOnlyList<DiscoveredWagon> Wagons,
    IReadOnlyList<RollingStockDiscoveryFailure> Failures)
{
    public int VehicleCount => Engines.Count + Wagons.Count;
}

using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

public sealed record ConsistCatalogIssue(string Path, string Message);
public sealed record ConsistCatalogResult(IReadOnlyList<ConsistMetadata> Consists, IReadOnlyList<ConsistCatalogIssue> Issues);

/// <summary>Discover and parse consist files in the browser virtual content namespace.</summary>
public sealed class ConsistCatalogReader
{
    public ConsistCatalogResult Read(IVirtualContentStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var reader = new ConsistReader();
        var consists = new List<ConsistMetadata>();
        var issues = new List<ConsistCatalogIssue>();
        foreach (var path in store.Paths.Where(path => path.EndsWith(".con", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                consists.Add(reader.Read(store, path));
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException)
            {
                issues.Add(new ConsistCatalogIssue(path, exception.Message));
            }
        }
        return new ConsistCatalogResult(Array.AsReadOnly(consists.ToArray()), Array.AsReadOnly(issues.ToArray()));
    }
}

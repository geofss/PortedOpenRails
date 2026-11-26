namespace OpenRails.Web.Content;

/// <summary>
/// Limits for disk-backed browser package ingestion. This is deliberately separate
/// from the smaller in-memory ZIP parser limit.
/// </summary>
public static class BrowserPackageArchiveLimits
{
    public const long MaxArchiveBytes = 30_000_000_000L;
    public const long MaximumQuotaSafetyMarginBytes = 128L * 1024 * 1024;
}

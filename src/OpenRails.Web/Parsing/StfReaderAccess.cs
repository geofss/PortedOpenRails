namespace OpenRails.Web.Parsing;

/// <summary>
/// STFReader exposes its stream-reader factory as a one-shot static property.
/// All browser-side parser entry points must share this lock across factory
/// assignment, reader construction, and parsing to prevent cross-package races.
/// </summary>
internal static class StfReaderAccess
{
    internal static object SyncRoot { get; } = new();
}

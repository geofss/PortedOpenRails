using System.Text;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Content;

/// <summary>
/// Opens STF text sources from the browser virtual content namespace.
/// The method signature intentionally matches the upstream STFReader factory seam.
/// </summary>
public sealed class VirtualContentStreamReaderFactory : IStreamReaderFactory, IStfIncludePathResolver
{
    private readonly IVirtualContentStore contentStore;

    public VirtualContentStreamReaderFactory(IVirtualContentStore contentStore)
    {
        this.contentStore = contentStore ?? throw new ArgumentNullException(nameof(contentStore));
    }

    /// <summary>
    /// Opens a virtual file, reads its optional first-line SIMIS signature, and
    /// returns a reader positioned at the first content line.
    /// </summary>
    public StreamReader GetStreamReader(string name, out string simisSignature)
    {
        var virtualPath = VirtualPath.Normalize(name);
        var stream = contentStore.OpenRead(virtualPath);
        try
        {
            var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024,
                leaveOpen: false);

            simisSignature = reader.ReadLine()!;
            return reader;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Resolves an STF include relative to the file containing the include.
    /// This is the virtual-path equivalent of the upstream parser's filesystem
    /// directory concatenation and must be used for nested includes.
    /// </summary>
    public string ResolveIncludePath(string containingFilePath, string includePath) =>
        VirtualPath.ResolveRelative(containingFilePath, includePath);
}

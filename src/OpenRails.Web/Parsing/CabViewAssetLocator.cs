using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

/// <summary>Finds the original CabView CVF reference inside an ENG Engine block.</summary>
public sealed class CabViewAssetLocator
{
    public string? Locate(IVirtualContentStore store, string engineFilePath)
    {
        ArgumentNullException.ThrowIfNull(store);
        var path = VirtualPath.Normalize(engineFilePath);
        lock (StfReaderAccess.SyncRoot)
        {
            STFReader.NextStreamReaderFactory = new VirtualContentStreamReaderFactory(store);
            using var reader = new STFReader(path, useTree: false);
            string? cabViewPath = null;
            reader.ParseFile(new[]
            {
                new STFReader.TokenProcessor("engine", () =>
                {
                    reader.MustMatch("(");
                    reader.ParseBlock(new[]
                    {
                        new STFReader.TokenProcessor("cabview", () =>
                        {
                            var relative = reader.ReadStringBlock(null);
                            if (string.IsNullOrWhiteSpace(relative))
                                throw new InvalidDataException($"Engine '{path}' declares an empty CabView reference.");
                            cabViewPath = VirtualPath.ResolveRelative(path, relative.Replace('\\', '/'));
                        })
                    });
                })
            });

            if (cabViewPath is null)
                return null;
            if (!cabViewPath.EndsWith(".cvf", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Engine '{path}' references '{cabViewPath}' as a cab view, but the file is not a CVF.");
            return store.Contains(cabViewPath) ? cabViewPath : null;
        }
    }
}

using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackShapeTextureAssetLoaderTests
{
    [Fact]
    public void SeparatesUnsupportedFormatsAndMalformedAceAndKeepsMissingDiagnostics()
    {
        var discovery = new TrackShapeTextureAssetDiscovery(
            new[]
            {
                new TrackShapeTextureAssetReference(7, "texture.dds", null, "ROUTES/Test/TEXTURES/texture.dds"),
                new TrackShapeTextureAssetReference(7, "broken.ace", null, "ROUTES/Test/TEXTURES/broken.ace")
            },
            new[]
            {
                new TrackShapeTextureAssetIssue(7, "missing.ace", "Texture was not found.")
            });
        using var store = CreateStore(
            ("ROUTES/Test/TEXTURES/texture.dds", "dds placeholder"),
            ("ROUTES/Test/TEXTURES/broken.ace", "not an ACE file"));

        var result = new TrackShapeTextureAssetLoader().Load(store, discovery);

        Assert.Empty(result.Loaded);
        Assert.Equal(2, result.Failed.Count);
        Assert.Contains(result.Failed, issue => issue.Path.EndsWith("texture.dds", StringComparison.OrdinalIgnoreCase)
            && issue.Reason.Contains("not yet supported"));
        Assert.Contains(result.Failed, issue => issue.Path.EndsWith("broken.ace", StringComparison.OrdinalIgnoreCase)
            && issue.Reason.Contains("SIMISA signature"));
        Assert.Single(result.Missing);
        Assert.Equal("missing.ace", result.Missing[0].ImageName);
    }

    private static ZipContentStore CreateStore(params (string Path, string Content)[] files)
    {
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Path);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(file.Content);
            }
        }
        memory.Position = 0;
        return new ZipContentStore(memory);
    }
}

using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class AceTextureReaderTests
{
    [Fact]
    public void DecodesStructuredAceChannelsIntoRgbaPixels()
    {
        using var store = CreateStore(("ROUTES/Test/TERRTEX/ground.ace", CreateAceFile(
            CreateStructuredPayload(), compressed: false)));

        var texture = new AceTextureReader().Read(store, "ROUTES/Test/TERRTEX/ground.ace");

        Assert.Equal(2, texture.Width);
        Assert.Equal(1, texture.Height);
        Assert.Equal(AceTexturePixelFormat.Rgba8, texture.PixelFormat);
        Assert.False(texture.IsRawData);
        Assert.Equal(new byte[] { 255, 0, 0, 255, 0, 128, 255, 255 }, Assert.Single(texture.MipLevels));
    }

    [Fact]
    public void ReadsCompressedAceStreamUsingTheOriginalSimisaDeflateHeader()
    {
        using var store = CreateStore(("ROUTES/Test/TERRTEX/compressed.ace", CreateAceFile(
            CreateStructuredPayload(), compressed: true)));

        var texture = new AceTextureReader().Read(store, "ROUTES/Test/TERRTEX/compressed.ace");

        Assert.Equal(AceTexturePixelFormat.Rgba8, texture.PixelFormat);
        Assert.Equal(new byte[] { 255, 0, 0, 255, 0, 128, 255, 255 }, Assert.Single(texture.MipLevels));
    }

    [Fact]
    public void ExpandsOneBitAceMaskChannelIntoPixelAlpha()
    {
        using var store = CreateStore(("ROUTES/Test/TERRTEX/masked.ace", CreateAceFile(
            CreateMaskedStructuredPayload(), compressed: false)));

        var texture = new AceTextureReader().Read(store, "ROUTES/Test/TERRTEX/masked.ace");

        Assert.Equal((byte)1, texture.AlphaBits);
        Assert.Equal(new byte[] { 255, 0, 0, 255, 0, 128, 255, 0 }, Assert.Single(texture.MipLevels));
    }

    [Fact]
    public void LoadsDiscoveredTerrainTextureAssetsThroughAceDecoder()
    {
        const string path = "ROUTES/Test/TERRTEX/ground.ace";
        var reference = new TerrainTextureAssetReference("Grass", 0, "ground.ace", null, path, false);
        var discovery = new TerrainTextureAssetDiscovery(new[] { reference }, Array.Empty<TerrainTextureAssetIssue>());
        using var store = CreateStore((path, CreateAceFile(CreateStructuredPayload(), compressed: false)));

        var result = new TerrainTextureAssetLoader().Load(store, discovery);

        var loaded = Assert.Single(result.Loaded);
        Assert.Equal(path, loaded.Reference.Path);
        Assert.Equal(AceTexturePixelFormat.Rgba8, loaded.Texture.PixelFormat);
        Assert.Empty(result.Missing);
        Assert.Empty(result.Failed);
    }

    [Fact]
    public void TerrainTextureLoaderRetainsOnlyTexturesWithinDecodedByteBudget()
    {
        const string firstPath = "ROUTES/Test/TERRTEX/first.ace";
        const string secondPath = "ROUTES/Test/TERRTEX/second.ace";
        var first = new TerrainTextureAssetReference("First", 0, "first.ace", null, firstPath, false);
        var second = new TerrainTextureAssetReference("Second", 0, "second.ace", null, secondPath, false);
        var discovery = new TerrainTextureAssetDiscovery(
            new[] { first, second }, Array.Empty<TerrainTextureAssetIssue>());
        var ace = CreateAceFile(CreateStructuredPayload(), compressed: false);
        using var store = CreateStore((firstPath, ace), (secondPath, ace));

        var result = new TerrainTextureAssetLoader().Load(store, discovery, maximumDecodedBytes: 8);

        Assert.Equal(firstPath, Assert.Single(result.Loaded).Reference.Path);
        var skipped = Assert.Single(result.Failed);
        Assert.Equal(secondPath, skipped.Path);
        Assert.Contains("remaining viewport texture budget", skipped.Reason);
    }

    [Fact]
    public void TerrainTextureLoaderRejectsNegativeDecodedByteBudget()
    {
        var discovery = new TerrainTextureAssetDiscovery(
            Array.Empty<TerrainTextureAssetReference>(), Array.Empty<TerrainTextureAssetIssue>());
        using var store = CreateStore();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TerrainTextureAssetLoader().Load(store, discovery, maximumDecodedBytes: -1));
    }

    [Fact]
    public void RollingStockTextureLoaderRetainsOnlyTexturesWithinDecodedByteBudget()
    {
        const string firstPath = "TRAINS/TRAINSET/Test/first.ace";
        const string secondPath = "TRAINS/TRAINSET/Test/second.ace";
        var discovery = new TrackShapeTextureAssetDiscovery(
            new[]
            {
                new TrackShapeTextureAssetReference(1, "first.ace", null, firstPath),
                new TrackShapeTextureAssetReference(2, "second.ace", null, secondPath)
            },
            Array.Empty<TrackShapeTextureAssetIssue>());
        var ace = CreateAceFile(CreateStructuredPayload(), compressed: false);
        using var store = CreateStore((firstPath, ace), (secondPath, ace));

        var result = new TrackShapeTextureAssetLoader().Load(store, discovery, maximumDecodedBytes: 8);

        Assert.Equal(firstPath, Assert.Single(result.Loaded).Reference.Path);
        var skipped = Assert.Single(result.Failed);
        Assert.Equal(secondPath, skipped.Path);
        Assert.Contains("remaining rolling-stock texture budget", skipped.Reason);
    }

    [Fact]
    public void RollingStockTextureLoaderRejectsNegativeDecodedByteBudget()
    {
        var discovery = new TrackShapeTextureAssetDiscovery(
            Array.Empty<TrackShapeTextureAssetReference>(), Array.Empty<TrackShapeTextureAssetIssue>());
        using var store = CreateStore();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TrackShapeTextureAssetLoader().Load(store, discovery, maximumDecodedBytes: -1));
    }

    [Fact]
    public void LoadsShapeReferencedAceTexturesAndReportsUnsupportedDds()
    {
        const string acePath = "ROUTES/Test/TEXTURES/rail.ace";
        var aceReference = new TrackShapeTextureAssetReference(5, "rail.ace", null, acePath);
        var ddsReference = new TrackShapeTextureAssetReference(5, "rail.dds", null, "ROUTES/Test/TEXTURES/rail.dds");
        var discovery = new TrackShapeTextureAssetDiscovery(
            new[] { aceReference, ddsReference },
            Array.Empty<TrackShapeTextureAssetIssue>());
        using var store = CreateStore((acePath, CreateAceFile(CreateStructuredPayload(), compressed: false)));

        var result = new TrackShapeTextureAssetLoader().Load(store, discovery);

        Assert.Equal(1, result.Loaded.Count);
        Assert.Equal(AceTexturePixelFormat.Rgba8, result.Loaded[0].Texture.PixelFormat);
        Assert.Contains(result.Failed, issue => issue.Path == ddsReference.Path && issue.Reason.Contains("not yet supported"));
        Assert.Empty(result.Missing);
    }

    [Fact]
    public void PreservesRawDxtMipDataForGpuUpload()
    {
        var raw = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        using var store = CreateStore(("ROUTES/Test/TERRTEX/ground.ace", CreateAceFile(
            CreateRawDxtPayload(raw), compressed: false)));

        var texture = new AceTextureReader().Read(store, "ROUTES/Test/TERRTEX/ground.ace");

        Assert.Equal(AceTexturePixelFormat.Dxt1, texture.PixelFormat);
        Assert.True(texture.IsRawData);
        Assert.Equal(raw, Assert.Single(texture.MipLevels));
    }

    private static byte[] CreateStructuredPayload()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(1u);
        writer.Write(0);
        writer.Write(2);
        writer.Write(1);
        writer.Write(0);
        writer.Write(3);
        writer.Write(new byte[128]);
        WriteChannel(writer, 8, 3);
        WriteChannel(writer, 8, 4);
        WriteChannel(writer, 8, 5);
        writer.Write(0); // one scanline offset
        writer.Write(new byte[] { 255, 0 });
        writer.Write(new byte[] { 0, 128 });
        writer.Write(new byte[] { 0, 255 });
        return stream.ToArray();
    }

    private static byte[] CreateMaskedStructuredPayload()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(1u);
        writer.Write(0);
        writer.Write(2);
        writer.Write(1);
        writer.Write(0);
        writer.Write(4);
        writer.Write(new byte[128]);
        WriteChannel(writer, 1, 2);
        WriteChannel(writer, 8, 3);
        WriteChannel(writer, 8, 4);
        WriteChannel(writer, 8, 5);
        writer.Write(0);
        writer.Write((byte)0b10000000);
        writer.Write(new byte[] { 255, 0 });
        writer.Write(new byte[] { 0, 128 });
        writer.Write(new byte[] { 0, 255 });
        return stream.ToArray();
    }

    private static byte[] CreateRawDxtPayload(byte[] raw)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(1u);
        writer.Write((int)AceTextureOptions.RawData);
        writer.Write(4);
        writer.Write(4);
        writer.Write(0x12);
        writer.Write(0);
        writer.Write(new byte[128]);
        writer.Write(0); // mip offset
        writer.Write(raw.Length);
        writer.Write(raw);
        return stream.ToArray();
    }

    private static void WriteChannel(BinaryWriter writer, ulong size, ulong type)
    {
        writer.Write(size);
        writer.Write(type);
    }

    private static byte[] CreateAceFile(byte[] payload, bool compressed)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        if (!compressed)
        {
            writer.Write(Encoding.ASCII.GetBytes("SIMISA@@"));
            writer.Write(Encoding.ASCII.GetBytes("@@@@@@@@"));
            writer.Write(payload);
        }
        else
        {
            writer.Write(Encoding.ASCII.GetBytes("SIMISA@F"));
            writer.Write((uint)payload.Length);
            writer.Write(Encoding.ASCII.GetBytes("@@@@"));
            writer.Write((ushort)0x9C78);
            using var compressedStream = new MemoryStream();
            using (var deflate = new DeflateStream(compressedStream, CompressionLevel.Optimal, leaveOpen: true))
                deflate.Write(payload);
            writer.Write(compressedStream.ToArray());
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static ZipContentStore CreateStore(params (string Path, byte[] Content)[] files)
    {
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Path);
                using var output = entry.Open();
                output.Write(file.Content);
            }
        }
        memory.Position = 0;
        return new ZipContentStore(memory);
    }
}

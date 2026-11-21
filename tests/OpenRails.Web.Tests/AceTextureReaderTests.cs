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

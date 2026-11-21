using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Orts.Parsers.Msts;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TerrainTileReaderTests
{
    [Fact]
    public void ReadsTerrainSamplesFromAnSbrStreamInTheVirtualContentStore()
    {
        var samplesPayload = Concat(
            FloatBlock(TokenID.terrain_sample_floor, 10),
            FloatBlock(TokenID.terrain_sample_scale, 0.5f),
            FloatBlock(TokenID.terrain_sample_size, 1),
            IntBlock(TokenID.terrain_nsamples, 2),
            StringBlock(TokenID.terrain_sample_ybuffer, new string(new[] { (char)0, (char)2, (char)4, (char)6 })));
        var terrainPayload = Block(TokenID.terrain_samples, samplesPayload);
        var tileBytes = CreateSbrFile(Block(TokenID.terrain, terrainPayload));

        using var store = CreateStore(("ROUTES/Test/TILES/test.t", tileBytes));
        var tile = new TerrainTileReader().Read(store, "ROUTES/Test/TILES/test.t");

        Assert.Equal(2, tile.Samples.SampleCount);
        Assert.Equal(10, tile.Samples.Floor);
        Assert.Equal(0.5f, tile.Samples.Scale);
        Assert.Equal(1, tile.Samples.SampleSize);
        Assert.Equal((ushort)6, tile.Samples.GetRawHeightSample(1, 1));
        Assert.Equal(13, tile.Samples.GetHeightMeters(1, 1));
    }

    private static byte[] CreateSbrFile(byte[] rootBlock)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("SIMISA@@@@@@@@@@"));
        writer.Write(Encoding.ASCII.GetBytes("JINX0T0b"));
        writer.Write(new byte[8]);
        writer.Write(rootBlock);
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] Block(TokenID id, byte[] payload)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((ushort)id);
        writer.Write((ushort)0);
        writer.Write((uint)(payload.Length + 1));
        writer.Write((byte)0);
        writer.Write(payload);
        return stream.ToArray();
    }

    private static byte[] IntBlock(TokenID id, int value)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(value);
        return Block(id, stream.ToArray());
    }

    private static byte[] FloatBlock(TokenID id, float value)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(value);
        return Block(id, stream.ToArray());
    }

    private static byte[] StringBlock(TokenID id, string value)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.Unicode, leaveOpen: true);
        writer.Write((ushort)value.Length);
        writer.Write(Encoding.Unicode.GetBytes(value));
        return Block(id, stream.ToArray());
    }

    private static byte[] Concat(params byte[][] values)
    {
        using var stream = new MemoryStream();
        foreach (var value in values)
            stream.Write(value);
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

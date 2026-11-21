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
        Assert.Equal(4, tile.Samples.SampleYBuffer?.Length);
    }

    [Fact]
    public void ReadsOriginalTerrainShaderTextureSlotsAndUvCalculations()
    {
        var shader = Block(TokenID.terrain_shader, Concat(
            StringPayload("BlendATexDiff"),
            Block(TokenID.terrain_texslots, Concat(
                UIntPayload(1),
                Block(TokenID.terrain_texslot, Concat(StringPayload("grass.ace"), IntPayload(1), IntPayload(2))))),
            Block(TokenID.terrain_uvcalcs, Concat(
                UIntPayload(1),
                Block(TokenID.terrain_uvcalc, Concat(IntPayload(1, 2, 3), FloatPayload(4.5f)))))));
        var tileBytes = CreateSbrFile(Block(TokenID.terrain, Concat(
            Block(TokenID.terrain_samples, IntBlock(TokenID.terrain_nsamples, 1)),
            Block(TokenID.terrain_shaders, Concat(IntPayload(1), shader)))));
        using var store = CreateStore(("ROUTES/Test/TILES/shaders.t", tileBytes));

        var tile = new TerrainTileReader().Read(store, "ROUTES/Test/TILES/shaders.t");

        var parsed = Assert.Single(tile.Shaders);
        Assert.Equal("BlendATexDiff", parsed.Name);
        var texture = Assert.Single(parsed.TextureSlots);
        Assert.Equal("grass.ace", texture.FileName);
        Assert.Equal(1, texture.A);
        Assert.Equal(2, texture.B);
        var uv = Assert.Single(parsed.UvCalculations);
        Assert.Equal(new TerrainUvCalculation(1, 2, 3, 4), uv);
    }

    [Fact]
    public void LoadsTerrainMetadataAltitudeAndOptionalFlagsAsOneTile()
    {
        var samplesPayload = Concat(
            FloatBlock(TokenID.terrain_sample_floor, 10),
            FloatBlock(TokenID.terrain_sample_scale, 0.5f),
            FloatBlock(TokenID.terrain_sample_size, 1),
            IntBlock(TokenID.terrain_nsamples, 2),
            StringBlock(TokenID.terrain_sample_ybuffer, new string(new[] { (char)0, (char)2, (char)4, (char)6 })));
        var tileBytes = CreateSbrFile(Block(TokenID.terrain, Block(TokenID.terrain_samples, samplesPayload)));
        using var store = CreateStore(
            ("ROUTES/Test/TILES/tile.t", tileBytes),
            ("ROUTES/Test/TILES/tile_y.raw", new byte[] { 1, 0, 2, 0, 3, 0, 4, 0 }),
            ("ROUTES/Test/TILES/tile_f.raw", new byte[] { 0, 4, 0, 4 }));

        var reference = new TerrainTileReference(
            0, 0,
            "ROUTES/Test/TILES/tile.t",
            "ROUTES/Test/TILES/tile_y.raw",
            "ROUTES/Test/TILES/tile_f.raw");

        var tile = new TerrainTileContentLoader().Load(store, reference);

        Assert.Equal(12, tile.Altitude.GetElevationMeters(1, 1, tile.Terrain.Samples.Floor, tile.Terrain.Samples.Scale));
        Assert.True(tile.Flags!.IsVertexHidden(1, 0));
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

    private static byte[] StringPayload(string value)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.Unicode, leaveOpen: true);
        writer.Write((ushort)value.Length);
        writer.Write(Encoding.Unicode.GetBytes(value));
        return stream.ToArray();
    }

    private static byte[] IntPayload(params int[] values)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        foreach (var value in values) writer.Write(value);
        return stream.ToArray();
    }

    private static byte[] UIntPayload(params uint[] values)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        foreach (var value in values) writer.Write(value);
        return stream.ToArray();
    }

    private static byte[] FloatPayload(params float[] values)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        foreach (var value in values) writer.Write(value);
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

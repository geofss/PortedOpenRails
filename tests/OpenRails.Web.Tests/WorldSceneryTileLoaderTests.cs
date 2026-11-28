using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Orts.Parsers.Msts;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class WorldSceneryTileLoaderTests
{
    [Fact]
    public void RejectsNegativePerTileTextureBudgetBeforeReadingWorldContent()
    {
        using var store = CreateStore();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorldSceneryTileLoader().Load(
                store,
                "ROUTES/Test",
                new WorldTileReference(0, 0, "ROUTES/Test/WORLD/W+000000+000000.w"),
                maximumTextureBytes: -1));
    }

    [Fact]
    public void LoadsWorldPlacementsAndIsolatesInvalidShapeAssetFailures()
    {
        var staticObject = Block(TokenID.Static, Concat(
            UIntPayload(TokenID.UiD, 42),
            StringBlock(TokenID.FileName, "building.s"),
            FloatBlock(TokenID.Position, 10, 0, 20)));
        using var store = CreateStore(
            ("ROUTES/Test/WORLD/W+000000+000000.w",
                CreateSbrFile(Block(TokenID.Tr_Worldfile, staticObject))),
            ("ROUTES/Test/SHAPES/building.s", Encoding.ASCII.GetBytes("not a SIMISA shape")));

        var loaded = new WorldSceneryTileLoader().Load(
            store,
            "ROUTES/Test",
            new WorldTileReference(0, 0, "ROUTES/Test/WORLD/W+000000+000000.w"));

        Assert.Equal((uint)42, Assert.Single(loaded.Tile.StaticObjects).Uid);
        Assert.Equal(new WorldTilePosition(10, 0, 20), Assert.Single(loaded.Placements).WorldPosition);
        Assert.Equal("ROUTES/Test/SHAPES/building.s", Assert.Single(loaded.ShapeDiscovery.Found).Path);
        Assert.Empty(loaded.ShapeMeshes);
        Assert.Single(loaded.FailedShapes);
        Assert.Empty(loaded.TextureDiscovery.Found);
        Assert.Empty(loaded.Textures.Loaded);
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

    private static byte[] UIntPayload(TokenID id, uint value) => Block(id, UIntPayload(value));

    private static byte[] StringBlock(TokenID id, string value)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.Unicode, leaveOpen: true);
        writer.Write((ushort)value.Length);
        writer.Write(Encoding.Unicode.GetBytes(value));
        return Block(id, stream.ToArray());
    }

    private static byte[] FloatBlock(TokenID id, params float[] values)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        foreach (var value in values) writer.Write(value);
        return Block(id, stream.ToArray());
    }

    private static byte[] UIntPayload(params uint[] values)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        foreach (var value in values) writer.Write(value);
        return stream.ToArray();
    }

    private static byte[] Concat(params byte[][] values)
    {
        using var stream = new MemoryStream();
        foreach (var value in values) stream.Write(value);
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

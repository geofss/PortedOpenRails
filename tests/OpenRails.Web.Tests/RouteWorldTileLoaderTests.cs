using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Orts.Parsers.Msts;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class RouteWorldTileLoaderTests
{
    [Fact]
    public void RouteSceneryLoaderKeepsValidTilesWhenAnotherTileFails()
    {
        var validWorld = CreateSbrFile(Block(TokenID.Tr_Worldfile, Array.Empty<byte>()));
        using var store = CreateStore(
            ("ROUTES/Test/WORLD/W+000000+000000.w", validWorld),
            ("ROUTES/Test/WORLD/W+000001+000000.w", Encoding.ASCII.GetBytes("broken world file")));
        var discovery = new RouteWorldTileDiscovery(
            new[]
            {
                new WorldTileReference(0, 0, "ROUTES/Test/WORLD/W+000000+000000.w"),
                new WorldTileReference(1, 0, "ROUTES/Test/WORLD/W+000001+000000.w")
            },
            new[] { (2, 0) });

        var result = new RouteWorldSceneryLoader().Load(store, "ROUTES/Test", discovery);

        Assert.Single(result.Loaded);
        Assert.Single(result.Failed);
        Assert.Equal("ROUTES/Test/WORLD/W+000001+000000.w", result.Failed[0].Path);
        Assert.Equal((2, 0), Assert.Single(result.Missing));
        Assert.Empty(result.Loaded[0].Placements);
    }

    [Fact]
    public void LoadsWorldObjectsAndResolvesTheirOriginalShapeAssetsTogether()
    {
        var staticObject = Block(TokenID.Static, Concat(
            UIntPayload(TokenID.UiD, 42),
            StringBlock(TokenID.FileName, "building.s"),
            FloatBlock(TokenID.Position, 10, 0, 20)));
        var worldFile = CreateSbrFile(Block(TokenID.Tr_Worldfile, staticObject));
        using var store = CreateStore(
            ("ROUTES/Test/WORLD/W+000000+000000.w", worldFile),
            ("GLOBAL/SHAPES/building.s", Encoding.UTF8.GetBytes("shape placeholder")));

        var loaded = new RouteWorldTileLoader().Load(
            store,
            "ROUTES/Test",
            new WorldTileReference(0, 0, "ROUTES/Test/WORLD/W+000000+000000.w"));

        Assert.Equal((uint)42, Assert.Single(loaded.Tile.StaticObjects).Uid);
        Assert.Equal(new WorldTilePosition(10, 0, 20), Assert.Single(loaded.Placements).WorldPosition);
        var shape = Assert.Single(loaded.ShapeAssets.Found);
        Assert.Equal("GLOBAL/SHAPES/building.s", shape.Path);
        Assert.Empty(loaded.ShapeAssets.Missing);
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

using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Orts.Parsers.Msts;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class WorldTileReaderTests
{
    [Fact]
    public void ReadsStaticAndTrackObjectPlacementsFromOriginalWorldTile()
    {
        var staticObject = Block(TokenID.Static, Concat(
            UIntPayload(TokenID.UiD, 42),
            StringBlock(TokenID.FileName, "building.s"),
            FloatBlock(TokenID.Position, 1, 2, 3),
            FloatBlock(TokenID.QDirection, 0, 0, 0, 1),
            UIntPayload(TokenID.StaticFlags, 0x10),
            UIntPayload(TokenID.VDbId, 5)));

        var trackObject = Block(TokenID.TrackObj, Concat(
            UIntPayload(TokenID.UiD, 43),
            UIntPayload(TokenID.SectionIdx, 7),
            FloatBlock(TokenID.Elevation, 1.5f),
            UIntPayload(TokenID.CollideFlags, 2),
            StringBlock(TokenID.FileName, "trackside.s"),
            Block(TokenID.JNodePosn, Concat(IntPayload(4, -5), FloatPayload(6, 7, 8)))));

        using var store = CreateStore(("ROUTES/Test/WORLD/W+000001-000002.w",
            CreateSbrFile(Block(TokenID.Tr_Worldfile, Concat(staticObject, trackObject)))));

        var tile = new WorldTileReader().Read(store, "ROUTES/Test/WORLD/W+000001-000002.w");

        Assert.Equal(1, tile.TileX);
        Assert.Equal(-2, tile.TileZ);
        var scenery = Assert.Single(tile.StaticObjects);
        Assert.Equal((uint)42, scenery.Uid);
        Assert.Equal("building.s", scenery.FileName);
        Assert.Equal(new WorldTilePosition(1, 2, 3), scenery.Position);
        Assert.Equal(new WorldQuaternion(0, 0, 0, 1), scenery.Direction);
        Assert.Equal((uint)0x10, scenery.StaticFlags);
        Assert.Equal((uint)5, scenery.WorldDatabaseId);

        var track = Assert.Single(tile.TrackObjects);
        Assert.Equal((uint)43, track.Uid);
        Assert.Equal((uint)7, track.SectionIndex);
        Assert.Equal(1.5f, track.Elevation);
        Assert.Equal((uint)2, track.CollideFlags);
        Assert.Equal("trackside.s", track.FileName);
        Assert.Equal(new WorldJunctionPosition(4, -5, 6, 7, 8), track.JunctionPosition);

        var placements = new WorldObjectPlacementBuilder().Build(tile);
        Assert.Equal(new WorldTilePosition(2049, 2, -4093), placements[0].WorldPosition);
        Assert.Equal(new WorldTilePosition(8198, 7, -10232), placements[1].JunctionWorldPosition);
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
    private static byte[] FloatBlock(TokenID id, params float[] values) => Block(id, FloatPayload(values));
    private static byte[] StringBlock(TokenID id, string value)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.Unicode, leaveOpen: true);
        writer.Write((ushort)value.Length);
        writer.Write(Encoding.Unicode.GetBytes(value));
        return Block(id, stream.ToArray());
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

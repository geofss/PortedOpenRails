using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Orts.Parsers.Msts;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackShapeFileReaderTests
{
    [Fact]
    public void ReadsOriginalShapePointsAndUvCoordinatesFromVirtualSbrStream()
    {
        var shapePayload = Concat(
            Block(TokenID.shape_header, UIntPayload(0, 0)),
            EmptyList(TokenID.volumes),
            EmptyList(TokenID.shader_names),
            EmptyList(TokenID.texture_filter_names),
            Block(TokenID.points, Concat(IntPayload(1),
                Block(TokenID.point, FloatPayload(1, 2, 3)))),
            Block(TokenID.uv_points, Concat(IntPayload(1),
                Block(TokenID.uv_point, FloatPayload(0.25f, 0.75f)))),
            EmptyList(TokenID.normals),
            EmptyList(TokenID.sort_vectors),
            EmptyList(TokenID.colours),
            EmptyList(TokenID.matrices),
            EmptyList(TokenID.images),
            EmptyList(TokenID.textures),
            EmptyList(TokenID.light_materials),
            EmptyList(TokenID.light_model_cfgs),
            EmptyList(TokenID.vtx_states),
            EmptyList(TokenID.prim_states),
            EmptyList(TokenID.lod_controls));

        using var store = CreateStore(("GLOBAL/SHAPES/track.s", CreateSbrFile(Block(TokenID.shape, shapePayload))));
        var asset = new TrackShapeAssetReference(4, "track.s", "GLOBAL/SHAPES/track.s");

        var shape = new TrackShapeFileReader().Read(store, asset);

        var point = Assert.Single(shape.points);
        Assert.Equal(1, point.X);
        Assert.Equal(2, point.Y);
        Assert.Equal(3, point.Z);
        var uv = Assert.Single(shape.uv_points);
        Assert.Equal(0.25f, uv.U);
        Assert.Equal(0.75f, uv.V);
        Assert.Empty(shape.lod_controls);
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

    private static byte[] EmptyList(TokenID id) => Block(id, IntPayload(0));

    private static byte[] IntPayload(int value)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(value);
        return stream.ToArray();
    }

    private static byte[] UIntPayload(uint first, uint second)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(first);
        writer.Write(second);
        return stream.ToArray();
    }

    private static byte[] FloatPayload(params float[] values)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        foreach (var value in values)
            writer.Write(value);
        return stream.ToArray();
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

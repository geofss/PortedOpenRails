using System.IO.Compression;
using System.Numerics;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Orts.Parsers.Msts;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackShapeMeshBuilderTests
{
    [Fact]
    public void BuildsPortableTriangleMeshFromOriginalShapeGeometryAndMaterialReferences()
    {
        using var store = CreateStore(("GLOBAL/SHAPES/triangle.s", CreateSbrFile(CreateTriangleShape())));
        var asset = new TrackShapeAssetReference(0, "triangle.s", "GLOBAL/SHAPES/triangle.s");
        var loaded = new TrackShapeAssetLoader().Load(store, asset);
        var mesh = loaded.Mesh;

        Assert.Equal((uint)0, loaded.ShapeIndex);
        Assert.Equal("GLOBAL/SHAPES/triangle.s", loaded.SourcePath);

        var lod = Assert.Single(mesh.Lods);
        var subObject = Assert.Single(lod.SubObjects);
        Assert.Equal(3, subObject.Vertices.Count);
        Assert.Equal(new Vector3(0, 0, 0), subObject.Vertices[0].Position);
        Assert.Equal(new Vector3(0, 0, -1), subObject.Vertices[2].Position);
        Assert.Equal(new Vector2(0.25f, 0.75f), subObject.Vertices[1].TextureCoordinate);

        var primitive = Assert.Single(subObject.Primitives);
        Assert.Equal(new[] { 0, 1, 2 }, primitive.Indices);
        Assert.Equal("Tex", primitive.ShaderName);
        Assert.Equal("track.ace", primitive.ImageName);
        Assert.Equal(0, primitive.MatrixIndex);
        Assert.Equal(-1, primitive.Hierarchy[0]);
    }

    private static byte[] CreateTriangleShape()
    {
        var points = Block(TokenID.points, Concat(IntPayload(3),
            Block(TokenID.point, FloatPayload(0, 0, 0)),
            Block(TokenID.point, FloatPayload(1, 0, 0)),
            Block(TokenID.point, FloatPayload(0, 0, 1))));
        var uvs = Block(TokenID.uv_points, Concat(IntPayload(3),
            Block(TokenID.uv_point, FloatPayload(0, 0)),
            Block(TokenID.uv_point, FloatPayload(0.25f, 0.75f)),
            Block(TokenID.uv_point, FloatPayload(1, 1))));
        var normals = Block(TokenID.normals, Concat(IntPayload(1),
            Block(TokenID.vector, FloatPayload(0, 1, 0))));
        var matrices = Block(TokenID.matrices, Concat(IntPayload(1),
            Block(TokenID.matrix, FloatPayload(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0))));
        var shaderNames = Block(TokenID.shader_names, Concat(IntPayload(1),
            Block(TokenID.named_shader, StringPayload("Tex"))));
        var images = Block(TokenID.images, Concat(IntPayload(1),
            Block(TokenID.image, StringPayload("track.ace"))));
        var textures = Block(TokenID.textures, Concat(IntPayload(1),
            Block(TokenID.texture, Concat(IntPayload(0), IntPayload(0), FloatPayload(-3)))));
        var lightConfigs = Block(TokenID.light_model_cfgs, Concat(IntPayload(1),
            Block(TokenID.light_model_cfg, Concat(UIntPayload(0), EmptyList(TokenID.uv_ops)))));
        var vertexStates = Block(TokenID.vtx_states, Concat(IntPayload(1),
            Block(TokenID.vtx_state, Concat(UIntPayload(0), IntPayload(0), IntPayload(0), IntPayload(0), UIntPayload(0)))));
        var primitiveState = Block(TokenID.prim_state, Concat(
            UIntPayload(0),
            IntPayload(0),
            Block(TokenID.tex_idxs, Concat(IntPayload(1), IntPayload(0))),
            FloatPayload(0),
            IntPayload(0),
            IntPayload(0),
            IntPayload(0),
            IntPayload(1)));
        var primitiveStates = Block(TokenID.prim_states, Concat(IntPayload(1), primitiveState));

        var geometryInfo = Block(TokenID.geometry_info, Concat(
            IntPayload(0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            EmptyList(TokenID.geometry_nodes),
            Block(TokenID.geometry_node_map, Concat(IntPayload(1), IntPayload(0)))));
        var subObjectHeader = Block(TokenID.sub_object_header, Concat(
            UIntPayload(0), IntPayload(-1), IntPayload(-1), UIntPayload(0), UIntPayload(0), geometryInfo));
        var vertices = Block(TokenID.vertices, Concat(IntPayload(3),
            CreateVertex(0, 0),
            CreateVertex(1, 1),
            CreateVertex(2, 2)));
        var subObject = Block(TokenID.sub_object, Concat(
            subObjectHeader,
            vertices,
            EmptyList(TokenID.vertex_sets),
            Block(TokenID.primitives, Concat(
                IntPayload(2),
                Block(TokenID.prim_state_idx, IntPayload(0)),
                Block(TokenID.indexed_trilist, Concat(
                    Block(TokenID.vertex_idxs, Concat(IntPayload(3), IntPayload(0, 1, 2))),
                    Block(TokenID.normal_idxs, Concat(IntPayload(1), IntPayload(0), IntPayload(3))),
                    EmptyList(TokenID.flags)))))));
        var distanceLevelHeader = Block(TokenID.distance_level_header, Concat(
            Block(TokenID.dlevel_selection, FloatPayload(1000)),
            Block(TokenID.hierarchy, Concat(IntPayload(1), IntPayload(-1)))));
        var distanceLevel = Block(TokenID.distance_level, Concat(
            distanceLevelHeader,
            Block(TokenID.sub_objects, Concat(IntPayload(1), subObject))));
        var lodControl = Block(TokenID.lod_control, Concat(
            Block(TokenID.distance_levels_header, IntPayload(0)),
            Block(TokenID.distance_levels, Concat(IntPayload(1), distanceLevel))));
        var lodControls = Block(TokenID.lod_controls, Concat(IntPayload(1), lodControl));

        return Block(TokenID.shape, Concat(
            Block(TokenID.shape_header, UIntPayload(0, 0)),
            EmptyList(TokenID.volumes),
            shaderNames,
            EmptyList(TokenID.texture_filter_names),
            points,
            uvs,
            normals,
            EmptyList(TokenID.sort_vectors),
            EmptyList(TokenID.colours),
            matrices,
            images,
            textures,
            EmptyList(TokenID.light_materials),
            lightConfigs,
            vertexStates,
            primitiveStates,
            lodControls));
    }

    private static byte[] CreateVertex(int pointIndex, int uvIndex) =>
        Block(TokenID.vertex, Concat(
            UIntPayload(0),
            IntPayload(pointIndex),
            IntPayload(0),
            UIntPayload(0xffffffff),
            UIntPayload(0xff000000),
            Block(TokenID.vertex_uvs, Concat(IntPayload(1), IntPayload(uvIndex)))));

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

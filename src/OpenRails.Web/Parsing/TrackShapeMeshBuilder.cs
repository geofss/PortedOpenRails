using System.Numerics;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Converts the upstream ShapeFile object model into immutable, browser-portable
/// mesh data. Matrix hierarchy and material references are preserved for the renderer.
/// </summary>
public sealed class TrackShapeMeshBuilder
{
    private const int MaximumVertices = 2_000_000;
    private const int MaximumIndices = 12_000_000;

    public TrackShapeMesh Build(shape source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.lod_controls.Count == 0)
            throw new InvalidDataException("Track shape contains no LOD controls.");

        var matrices = source.matrices.Select(matrix => new TrackShapeMatrix(
            matrix.Name,
            matrix.AX, matrix.AY, matrix.AZ,
            matrix.BX, matrix.BY, matrix.BZ,
            matrix.CX, matrix.CY, matrix.CZ,
            matrix.DX, matrix.DY, matrix.DZ)).ToArray();

        var lods = new List<TrackShapeLodMesh>();
        var totalVertices = 0L;
        var totalIndices = 0L;

        foreach (var lodControl in source.lod_controls)
        foreach (var distanceLevel in lodControl.distance_levels)
        {
            var subObjects = new List<TrackShapeSubObjectMesh>();
            foreach (var subObject in distanceLevel.sub_objects)
            {
                totalVertices += subObject.vertices.Count;
                if (totalVertices > MaximumVertices)
                    throw new InvalidDataException($"Track shape exceeds the {MaximumVertices}-vertex limit.");

                var vertices = BuildVertices(source, subObject);
                var primitives = new List<TrackShapePrimitiveMesh>();

                foreach (var primitive in subObject.primitives)
                {
                    var built = BuildPrimitive(source, primitive, subObject, distanceLevel.distance_level_header.hierarchy);
                    totalIndices += built.Indices.Count;
                    if (totalIndices > MaximumIndices)
                        throw new InvalidDataException($"Track shape exceeds the {MaximumIndices}-index limit.");
                    primitives.Add(built);
                }

                subObjects.Add(new TrackShapeSubObjectMesh(
                    vertices,
                    subObject.sub_object_header.geometry_info.geometry_node_map.ToArray(),
                    primitives));
            }

            lods.Add(new TrackShapeLodMesh(
                distanceLevel.distance_level_header.dlevel_selection,
                distanceLevel.distance_level_header.hierarchy.ToArray(),
                subObjects));
        }

        return new TrackShapeMesh(source.shape_header.flags1, source.shape_header.flags2, matrices, lods, TrackShapeAnimationBuilder.Build(source));
    }

    private static IReadOnlyList<TrackShapeMeshVertex> BuildVertices(shape source, sub_object subObject)
    {
        var result = new TrackShapeMeshVertex[subObject.vertices.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var vertex = subObject.vertices[i];
            var point = GetRequired(source.points, vertex.ipoint, "point");
            var normal = GetRequired(source.normals, vertex.inormal, "normal");
            var uv = vertex.vertex_uvs.Length == 0
                ? new uv_point(0, 0)
                : GetRequired(source.uv_points, vertex.vertex_uvs[0], "UV point");

            result[i] = new TrackShapeMeshVertex(
                new Vector3(point.X, point.Y, -point.Z),
                new Vector3(normal.X, normal.Y, -normal.Z),
                new Vector2(uv.U, uv.V),
                vertex.flags,
                vertex.Color1,
                vertex.Color2);
        }

        return Array.AsReadOnly(result);
    }

    private static TrackShapePrimitiveMesh BuildPrimitive(
        shape source,
        primitive primitive,
        sub_object subObject,
        int[] hierarchy)
    {
        var primitiveState = GetRequired(source.prim_states, primitive.prim_state_idx, "primitive state");
        var vertexState = GetRequired(source.vtx_states, primitiveState.ivtx_state, "vertex state");
        var lightConfiguration = GetRequired(source.light_model_cfgs, vertexState.LightCfgIdx, "light configuration");
        var shaderName = GetRequired(source.shader_names, primitiveState.ishader, "shader name");

        string? imageName = null;
        int textureFilterMode = 0;
        float mipMapLodBias = 0;
        if (primitiveState.tex_idxs.Length > 0)
        {
            var texture = GetRequired(source.textures, primitiveState.tex_idxs[0], "texture");
            imageName = GetRequired(source.images, texture.iImage, "image name");
            textureFilterMode = texture.FilterMode;
            mipMapLodBias = texture.MipMapLODBias;
        }

        var indices = new List<int>(primitive.indexed_trilist.vertex_idxs.Count * 3);
        foreach (var triangle in primitive.indexed_trilist.vertex_idxs)
        {
            ValidateVertexIndex(triangle.a, subObject.vertices.Count);
            ValidateVertexIndex(triangle.b, subObject.vertices.Count);
            ValidateVertexIndex(triangle.c, subObject.vertices.Count);
            indices.Add(triangle.a);
            indices.Add(triangle.b);
            indices.Add(triangle.c);
        }

        var textureAddressMode = lightConfiguration.uv_ops.Count == 0
            ? 0
            : lightConfiguration.uv_ops[0].TexAddrMode;

        return new TrackShapePrimitiveMesh(
            primitive.prim_state_idx,
            vertexState.imatrix,
            vertexState.LightMatIdx,
            vertexState.LightCfgIdx,
            hierarchy.ToArray(),
            indices,
            shaderName,
            imageName,
            textureFilterMode,
            mipMapLodBias,
            textureAddressMode,
            primitiveState.alphatestmode == 1,
            primitiveState.ZBias);
    }

    private static T GetRequired<T>(IReadOnlyList<T> values, int index, string kind)
    {
        if (index < 0 || index >= values.Count)
            throw new InvalidDataException($"Track shape references {kind} index {index}, but only {values.Count} entries exist.");
        return values[index];
    }

    private static void ValidateVertexIndex(int index, int vertexCount)
    {
        if (index < 0 || index >= vertexCount)
            throw new InvalidDataException($"Track shape primitive references vertex {index}, but only {vertexCount} vertices exist.");
    }
}

public sealed record TrackShapeMatrix(
    string? Name,
    float AX, float AY, float AZ,
    float BX, float BY, float BZ,
    float CX, float CY, float CZ,
    float DX, float DY, float DZ);

public sealed record TrackShapeMeshVertex(
    Vector3 Position,
    Vector3 Normal,
    Vector2 TextureCoordinate,
    uint Flags,
    uint Color1,
    uint Color2);

public sealed record TrackShapePrimitiveMesh(
    int PrimitiveStateIndex,
    int MatrixIndex,
    int LightMaterialIndex,
    int LightConfigurationIndex,
    IReadOnlyList<int> Hierarchy,
    IReadOnlyList<int> Indices,
    string ShaderName,
    string? ImageName,
    int TextureFilterMode,
    float MipMapLodBias,
    int TextureAddressMode,
    bool AlphaTest,
    float ZBias);

public sealed record TrackShapeSubObjectMesh(
    IReadOnlyList<TrackShapeMeshVertex> Vertices,
    IReadOnlyList<int> GeometryNodeMap,
    IReadOnlyList<TrackShapePrimitiveMesh> Primitives);

public sealed record TrackShapeLodMesh(
    float ViewingDistance,
    IReadOnlyList<int> Hierarchy,
    IReadOnlyList<TrackShapeSubObjectMesh> SubObjects);

public sealed record TrackShapeMesh(
    uint Flags1,
    uint Flags2,
    IReadOnlyList<TrackShapeMatrix> Matrices,
    IReadOnlyList<TrackShapeLodMesh> Lods,
    IReadOnlyList<TrackShapeAnimationClip>? Animations = null);

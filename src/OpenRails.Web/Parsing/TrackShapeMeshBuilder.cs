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

        var lightMaterials = source.light_materials.Select(material => new TrackShapeLightMaterial(
            material.flags, material.DiffColIdx, material.AmbColIdx, material.SpecColIdx,
            material.EmissiveColIdx, material.SpecPower)).ToArray();
        var viewSphereRadius = source.volumes.Count > 0 ? source.volumes[0].Radius : 100f;
        return new TrackShapeMesh(source.shape_header.flags1, source.shape_header.flags2,
            matrices, lods, TrackShapeAnimationBuilder.Build(source), Array.AsReadOnly(lightMaterials), viewSphereRadius);
    }

    private static IReadOnlyList<TrackShapeMeshVertex> BuildVertices(shape source, sub_object subObject)
    {
        var result = new TrackShapeMeshVertex[subObject.vertices.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var vertex = subObject.vertices[i];
            var point = GetRequired(source.points, vertex.ipoint, "point");
            var normal = GetRequired(source.normals, vertex.inormal, "normal");
            var uvCoordinates = vertex.vertex_uvs
                .Select(index => GetRequired(source.uv_points, index, "UV point"))
                .Select(uv => new Vector2(uv.U, uv.V))
                .ToArray();
            var primaryUv = uvCoordinates.Length == 0 ? Vector2.Zero : uvCoordinates[0];

            result[i] = new TrackShapeMeshVertex(
                new Vector3(point.X, point.Y, -point.Z),
                new Vector3(normal.X, normal.Y, -normal.Z),
                primaryUv,
                vertex.flags,
                vertex.Color1,
                vertex.Color2,
                Array.AsReadOnly(uvCoordinates.Skip(1).ToArray()));
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

        var textureBindings = primitiveState.tex_idxs.Select(textureIndex =>
        {
            var texture = GetRequired(source.textures, textureIndex, "texture");
            var image = GetRequired(source.images, texture.iImage, "image name");
            return new TrackShapeTextureBinding(image, texture.FilterMode,
                texture.MipMapLODBias, texture.BorderColor);
        }).ToArray();
        var imageName = textureBindings.FirstOrDefault()?.ImageName;
        var textureFilterMode = textureBindings.FirstOrDefault()?.FilterMode ?? 0;
        var mipMapLodBias = textureBindings.FirstOrDefault()?.MipMapLodBias ?? 0;
        var uvOperations = lightConfiguration.uv_ops.Select(BuildUvOperation).ToArray();

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

        var textureAddressMode = uvOperations.FirstOrDefault()?.TextureAddressMode ?? 0;

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
            primitiveState.ZBias,
            Array.AsReadOnly(textureBindings),
            Array.AsReadOnly(uvOperations),
            primitiveState.flags,
            primitiveState.ZBufMode,
            lightConfiguration.flags,
            vertexState.flags,
            vertexState.LightFlags,
            vertexState.Matrix2);
    }

    private static TrackShapeUvOperation BuildUvOperation(uv_op operation) => operation switch
    {
        uv_op_copy copy => new TrackShapeUvOperation(nameof(uv_op_copy),
            copy.TexAddrMode, copy.SrcUVIdx, 0, 0),
        uv_op_uniformscale scale => new TrackShapeUvOperation(nameof(uv_op_uniformscale),
            scale.TexAddrMode, scale.SrcUVIdx, scale.UnknownParameter3, scale.UnknownParameter4),
        uv_op_nonuniformscale scale => new TrackShapeUvOperation(nameof(uv_op_nonuniformscale),
            scale.TexAddrMode, scale.SrcUVIdx, scale.UnknownParameter3, scale.UnknownParameter4),
        uv_op_reflectmapfull reflect => new TrackShapeUvOperation(nameof(uv_op_reflectmapfull),
            reflect.TexAddrMode, -1, 0, 0),
        uv_op_reflectmap reflect => new TrackShapeUvOperation(nameof(uv_op_reflectmap),
            reflect.TexAddrMode, -1, 0, 0),
        _ => new TrackShapeUvOperation(operation.GetType().Name, operation.TexAddrMode, -1, 0, 0)
    };

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
    uint Color2,
    IReadOnlyList<Vector2>? AdditionalTextureCoordinates = null)
{
    public Vector2 GetTextureCoordinate(int channel)
    {
        if (channel < 0) throw new ArgumentOutOfRangeException(nameof(channel));
        if (channel == 0) return TextureCoordinate;
        return AdditionalTextureCoordinates is not null && channel - 1 < AdditionalTextureCoordinates.Count
            ? AdditionalTextureCoordinates[channel - 1]
            : Vector2.Zero;
    }
}

public sealed record TrackShapeTextureBinding(
    string ImageName,
    int FilterMode,
    float MipMapLodBias,
    uint BorderColor);

public sealed record TrackShapeUvOperation(
    string Kind,
    int TextureAddressMode,
    int SourceUvIndex,
    float Parameter3,
    float Parameter4);

public sealed record TrackShapeLightMaterial(
    uint Flags,
    int DiffuseColorIndex,
    int AmbientColorIndex,
    int SpecularColorIndex,
    int EmissiveColorIndex,
    float SpecularPower);

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
    float ZBias,
    IReadOnlyList<TrackShapeTextureBinding>? TextureBindings = null,
    IReadOnlyList<TrackShapeUvOperation>? UvOperations = null,
    uint PrimitiveFlags = 0,
    int ZBufferMode = 1,
    uint LightConfigurationFlags = 0,
    uint VertexStateFlags = 0,
    uint LightFlags = 0,
    int SecondaryMatrixIndex = -1);

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
    IReadOnlyList<TrackShapeAnimationClip>? Animations = null,
    IReadOnlyList<TrackShapeLightMaterial>? LightMaterials = null,
    float ViewSphereRadius = 100f);

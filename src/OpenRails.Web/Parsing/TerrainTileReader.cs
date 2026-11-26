// COPYRIGHT 2009-2013 by the Open Rails project.
// This file is part of Open Rails and is licensed under GPL-3.0-or-later.
// Browser-port adaptation reads terrain tiles from virtual content streams.

using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Reads the portable terrain-sample and patch metadata from an MSTS .t terrain tile.
/// The upstream SBR reader handles compressed SIMISA binary and Unicode text blocks.
/// </summary>
public sealed class TerrainTileReader
{
    private const int MaximumSamplesPerSide = 4096;
    private const int MaximumPatchCountPerSide = 64;

    public TerrainTileData Read(IVirtualContentStore store, string path)
    {
        ArgumentNullException.ThrowIfNull(store);
        var normalized = VirtualPath.Normalize(path);

        using var stream = store.OpenRead(normalized);
        using var reader = SBR.Open(stream, normalized);
        using var terrainBlock = reader.ReadSubBlock();
        terrainBlock.VerifyID(TokenID.terrain);

        var samples = new TerrainSampleData();
        var patchSets = new List<TerrainPatchSet>();
        var shaders = new List<TerrainShaderDefinition>();
        float errorThresholdScale = 1;
        float alwaysSelectMaxDistance = 0;
        TerrainWaterHeightOffset? waterOffset = null;

        while (!terrainBlock.EndOfBlock())
        {
            using var block = terrainBlock.ReadSubBlock();
            switch (block.ID)
            {
                case TokenID.terrain_errthreshold_scale:
                    errorThresholdScale = block.ReadFloat();
                    break;
                case TokenID.terrain_alwaysselect_maxdist:
                    alwaysSelectMaxDistance = block.ReadFloat();
                    break;
                case TokenID.terrain_water_height_offset:
                    waterOffset = ReadWaterOffset(block);
                    break;
                case TokenID.terrain_samples:
                    samples = ReadSamples(block);
                    break;
                case TokenID.terrain_patches:
                    ReadPatchSets(block, patchSets);
                    break;
                case TokenID.terrain_shaders:
                    ReadShaders(block, shaders);
                    break;
                default:
                    block.Skip();
                    break;
            }
        }

        if (samples.SampleCount <= 0)
            throw new InvalidDataException($"Terrain tile '{normalized}' has no valid terrain_samples block.");

        return new TerrainTileData(
            normalized,
            errorThresholdScale,
            alwaysSelectMaxDistance,
            waterOffset,
            samples,
            patchSets,
            shaders);
    }

    private static TerrainWaterHeightOffset ReadWaterOffset(SBR block)
    {
        block.VerifyID(TokenID.terrain_water_height_offset);
        var values = new float[4];
        for (var i = 0; i < values.Length && !block.EndOfBlock(); i++)
            values[i] = block.ReadFloat();
        return new TerrainWaterHeightOffset(values[0], values[1], values[2], values[3]);
    }

    private static TerrainSampleData ReadSamples(SBR block)
    {
        block.VerifyID(TokenID.terrain_samples);
        var result = new TerrainSampleData();

        while (!block.EndOfBlock())
        {
            using var field = block.ReadSubBlock();
            switch (field.ID)
            {
                case TokenID.terrain_nsamples:
                    result.SampleCount = field.ReadInt();
                    if (result.SampleCount <= 0 || result.SampleCount > MaximumSamplesPerSide)
                        throw new InvalidDataException($"Terrain sample count {result.SampleCount} is outside the supported range.");
                    break;
                case TokenID.terrain_sample_rotation:
                    result.Rotation = field.ReadFloat();
                    break;
                case TokenID.terrain_sample_floor:
                    result.Floor = field.ReadFloat();
                    break;
                case TokenID.terrain_sample_scale:
                    result.Scale = field.ReadFloat();
                    break;
                case TokenID.terrain_sample_size:
                    result.SampleSize = field.ReadFloat();
                    break;
                case TokenID.terrain_sample_ybuffer:
                    result.SampleYBuffer = field.ReadString();
                    break;
                case TokenID.terrain_sample_ebuffer:
                    result.SampleEBuffer = field.ReadString();
                    break;
                case TokenID.terrain_sample_nbuffer:
                    result.SampleNBuffer = field.ReadString();
                    break;
                default:
                    field.Skip();
                    break;
            }
        }

        return result;
    }

    private static void ReadShaders(SBR block, List<TerrainShaderDefinition> result)
    {
        block.VerifyID(TokenID.terrain_shaders);
        var count = block.ReadInt();
        if (count < 0 || count > 4096)
            throw new InvalidDataException($"Terrain shader count {count} is outside the supported range.");

        for (var i = 0; i < count; i++)
        {
            using var shaderBlock = block.ReadSubBlock();
            result.Add(ReadShader(shaderBlock));
        }
        if (!block.EndOfBlock())
            block.Skip();
    }

    private static TerrainShaderDefinition ReadShader(SBR block)
    {
        block.VerifyID(TokenID.terrain_shader);
        var name = block.ReadString();
        var textureSlots = new List<TerrainTextureSlot>();
        var uvCalculations = new List<TerrainUvCalculation>();

        while (!block.EndOfBlock())
        {
            using var field = block.ReadSubBlock();
            switch (field.ID)
            {
                case TokenID.terrain_texslots:
                    var textureCount = field.ReadUInt();
                    if (textureCount > 256)
                        throw new InvalidDataException($"Terrain shader '{name}' has too many texture slots.");
                    for (var i = 0; i < textureCount; i++)
                    {
                        using var textureBlock = field.ReadSubBlock();
                        textureSlots.Add(ReadTextureSlot(textureBlock));
                    }
                    break;
                case TokenID.terrain_uvcalcs:
                    var calculationCount = field.ReadUInt();
                    if (calculationCount > 256)
                        throw new InvalidDataException($"Terrain shader '{name}' has too many UV calculations.");
                    for (var i = 0; i < calculationCount; i++)
                    {
                        using var calculationBlock = field.ReadSubBlock();
                        uvCalculations.Add(ReadUvCalculation(calculationBlock));
                    }
                    break;
                default:
                    field.Skip();
                    break;
            }
        }

        return new TerrainShaderDefinition(name, textureSlots, uvCalculations);
    }

    private static TerrainTextureSlot ReadTextureSlot(SBR block)
    {
        block.VerifyID(TokenID.terrain_texslot);
        var slot = new TerrainTextureSlot(block.ReadString(), block.ReadInt(), block.ReadInt());
        if (!block.EndOfBlock())
            block.Skip();
        return slot;
    }

    private static TerrainUvCalculation ReadUvCalculation(SBR block)
    {
        block.VerifyID(TokenID.terrain_uvcalc);
        return new TerrainUvCalculation(block.ReadInt(), block.ReadInt(), block.ReadInt(), (int)block.ReadFloat());
    }
    private static void ReadPatchSets(SBR block, List<TerrainPatchSet> result)
    {
        block.VerifyID(TokenID.terrain_patches);
        using var setsBlock = block.ReadSubBlock();
        if (setsBlock.ID != TokenID.terrain_patchsets)
        {
            setsBlock.Skip();
            return;
        }

        var count = setsBlock.ReadInt();
        if (count < 0 || count > MaximumPatchCountPerSide)
            throw new InvalidDataException($"Terrain patch-set count {count} is outside the supported range.");

        for (var i = 0; i < count; i++)
        {
            using var patchSetBlock = setsBlock.ReadSubBlock();
            result.Add(ReadPatchSet(patchSetBlock));
        }
    }

    private static TerrainPatchSet ReadPatchSet(SBR block)
    {
        block.VerifyID(TokenID.terrain_patchset);
        var distance = 0;
        var patchesPerSide = 0;
        var patches = new List<TerrainPatch>();

        while (!block.EndOfBlock())
        {
            using var field = block.ReadSubBlock();
            switch (field.ID)
            {
                case TokenID.terrain_patchset_distance:
                    distance = field.ReadInt();
                    break;
                case TokenID.terrain_patchset_npatches:
                    patchesPerSide = field.ReadInt();
                    if (patchesPerSide < 0 || patchesPerSide > MaximumPatchCountPerSide)
                        throw new InvalidDataException($"Terrain patch dimension {patchesPerSide} is outside the supported range.");
                    break;
                case TokenID.terrain_patchset_patches:
                    var patchCount = checked(patchesPerSide * patchesPerSide);
                    for (var i = 0; i < patchCount; i++)
                    {
                        using var patchBlock = field.ReadSubBlock();
                        patches.Add(ReadPatch(patchBlock));
                    }
                    break;
                default:
                    field.Skip();
                    break;
            }
        }

        if (patchesPerSide > 0 && patches.Count != patchesPerSide * patchesPerSide)
            throw new InvalidDataException("Terrain patch set contains an unexpected number of patches.");

        return new TerrainPatchSet(distance, patchesPerSide, patches);
    }

    private static TerrainPatch ReadPatch(SBR block)
    {
        block.VerifyID(TokenID.terrain_patchset_patch);
        return new TerrainPatch(
            block.ReadUInt(),
            block.ReadFloat(), // center X
            block.ReadFloat(), // average Y
            block.ReadFloat(), // center Z
            block.ReadFloat(), // height factor
            block.ReadFloat(), // height range
            block.ReadFloat(), // radius
            block.ReadInt(),   // shader index
            block.ReadFloat(), // texture X
            block.ReadFloat(), // texture Y
            block.ReadFloat(), // texture width
            block.ReadFloat(), // texture B
            block.ReadFloat(), // texture C
            block.ReadFloat(), // texture height
            block.ReadFloat()); // error bias
    }
}

public sealed class TerrainTileData
{
    public string SourcePath { get; }
    public int TileSize { get; } = 1;
    public float ErrorThresholdScale { get; }
    public float AlwaysSelectMaxDistance { get; }
    public TerrainWaterHeightOffset? WaterHeightOffset { get; }
    public TerrainSampleData Samples { get; }
    public IReadOnlyList<TerrainPatchSet> PatchSets { get; }
    public IReadOnlyList<TerrainShaderDefinition> Shaders { get; }

    public TerrainTileData(string sourcePath, float errorThresholdScale, float alwaysSelectMaxDistance,
        TerrainWaterHeightOffset? waterHeightOffset, TerrainSampleData samples, IEnumerable<TerrainPatchSet> patchSets,
        IEnumerable<TerrainShaderDefinition>? shaders = null)
    {
        SourcePath = sourcePath;
        ErrorThresholdScale = errorThresholdScale;
        AlwaysSelectMaxDistance = alwaysSelectMaxDistance;
        WaterHeightOffset = waterHeightOffset;
        Samples = samples;
        PatchSets = Array.AsReadOnly(patchSets.ToArray());
        Shaders = Array.AsReadOnly((shaders ?? Array.Empty<TerrainShaderDefinition>()).ToArray());
    }
}

public sealed class TerrainSampleData
{
    public int SampleCount { get; internal set; }
    public float Rotation { get; internal set; }
    public float Floor { get; internal set; }
    public float Scale { get; internal set; }
    public float SampleSize { get; internal set; }
    public string? SampleYBuffer { get; internal set; }
    public string? SampleEBuffer { get; internal set; }
    public string? SampleNBuffer { get; internal set; }
}

public sealed record TerrainWaterHeightOffset(float SouthWest, float SouthEast, float NorthEast, float NorthWest);

public sealed record TerrainPatchSet(int Distance, int PatchesPerSide, IReadOnlyList<TerrainPatch> Patches);

public sealed record TerrainPatch(
    uint Flags,
    float CenterX,
    float AverageY,
    float CenterZ,
    float HeightFactor,
    float HeightRange,
    float RadiusMeters,
    int ShaderIndex,
    float TextureX,
    float TextureY,
    float TextureWidth,
    float TextureB,
    float TextureC,
    float TextureHeight,
    float ErrorBias)
{
    public bool WaterEnabled => (Flags & 0xC0) != 0;
    public bool DrawingEnabled => (Flags & 1) == 0;
}

public sealed record TerrainTextureSlot(string FileName, int A, int B);
public sealed record TerrainUvCalculation(int A, int B, int C, int D);
public sealed record TerrainShaderDefinition(string Name, IReadOnlyList<TerrainTextureSlot> TextureSlots, IReadOnlyList<TerrainUvCalculation> UvCalculations);

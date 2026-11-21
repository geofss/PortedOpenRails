namespace OpenRails.Web.Parsing;

/// <summary>Reproduces the original terrain material/overlay selection for one patch.</summary>
public sealed class TerrainPatchMaterialBuilder
{
    public TerrainPatchMaterial? Build(TerrainTileData terrain, int shaderIndex, bool usesCustomIndexBuffer)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        if (shaderIndex < 0 || shaderIndex >= terrain.Shaders.Count)
            return null;

        var shader = terrain.Shaders[shaderIndex];
        if (shader.TextureSlots.Count == 0)
            return null;

        var primary = shader.TextureSlots[0].FileName;
        var overlay = shader.TextureSlots.Count > 1
            ? shader.TextureSlots[1].FileName
            : "microtex.ace";

        var overlayScale = 32;
        if (shader.TextureSlots.Count > 1 && shader.UvCalculations.Count > 1)
        {
            var scale = shader.UvCalculations[1].D;
            if (scale != 0 && scale != 32)
                overlayScale = scale;
        }

        var materialName = terrain.TileSize > 2
            ? "TerrainSharedDistantMountain"
            : usesCustomIndexBuffer ? "Terrain" : "TerrainShared";

        return new TerrainPatchMaterial(materialName, shader.Name, primary, overlay, overlayScale);
    }
}

public sealed record TerrainPatchMaterial(
    string MaterialName,
    string ShaderName,
    string PrimaryTextureFileName,
    string OverlayTextureFileName,
    int OverlayScale);

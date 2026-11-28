using System.Globalization;
using OpenRails.Web.Content;
using Orts.Parsers.Msts;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Reads the scenery and track-object placement records from one original MSTS
/// world tile (.w) using the browser-safe SBR stream parser.
/// </summary>
public sealed class WorldTileReader
{
    public WorldTileData Read(IVirtualContentStore store, string path)
    {
        ArgumentNullException.ThrowIfNull(store);
        var normalized = VirtualPath.Normalize(path);
        var (tileX, tileZ) = ParseTileCoordinates(normalized);

        using var stream = store.OpenRead(normalized);
        using var reader = SBR.Open(stream, normalized);
        using var worldBlock = reader.ReadSubBlock();
        worldBlock.VerifyID(TokenID.Tr_Worldfile);

        var staticObjects = new List<StaticWorldObject>();
        var trackObjects = new List<TrackWorldObject>();
        var diagnostics = new List<string>();

        while (!worldBlock.EndOfBlock())
        {
            using var objectBlock = worldBlock.ReadSubBlock();
            switch (objectBlock.ID)
            {
                case TokenID.Static:
                    staticObjects.Add(ReadStaticObject(objectBlock, diagnostics, "Static"));
                    break;
                case TokenID.CollideObject:
                    staticObjects.Add(ReadStaticObject(objectBlock, diagnostics, "CollideObject"));
                    break;
                // Open Rails treats these WORLD records as object instances with
                // the common filename/position/orientation fields. Preserve them
                // as scenery placements instead of silently discarding them.
                case TokenID.CarSpawner:
                case (TokenID)357:
                case TokenID.Siding:
                case (TokenID)361:
                case TokenID.Platform:
                case TokenID.Forest:
                case TokenID.LevelCr:
                case TokenID.Dyntrack:
                case (TokenID)306:
                case TokenID.Transfer:
                case (TokenID)363:
                case TokenID.Gantry:
                case (TokenID)356:
                case TokenID.Pickup:
                case (TokenID)359:
                case TokenID.Hazard:
                case TokenID.Signal:
                case TokenID.Speedpost:
                    staticObjects.Add(ReadStaticObject(objectBlock, diagnostics, objectBlock.ID.ToString()));
                    break;
                case TokenID.TrackObj:
                    trackObjects.Add(ReadTrackObject(objectBlock, diagnostics));
                    break;
                default:
                    AddDiagnostic(diagnostics, $"Skipped unsupported WORLD block token 0x{(ushort)objectBlock.ID:X4}.");
                    objectBlock.Skip();
                    break;
            }
        }

        return new WorldTileData(normalized, tileX, tileZ, staticObjects, trackObjects, diagnostics);
    }

    private static StaticWorldObject ReadStaticObject(SBR block, List<string> diagnostics, string objectKind)
    {
        var uid = 0u;
        string? fileName = null;
        WorldTilePosition? position = null;
        WorldQuaternion? direction = null;
        WorldMatrix3x3? matrix = null;
        uint staticFlags = 0;
        uint worldDatabaseId = 0;
        string? treeTexture = null;
        WorldFloatRange? forestScaleRange = null;
        WorldFloatPair? forestArea = null;
        int? forestPopulation = null;
        WorldFloatPair? forestTreeSize = null;

        while (!block.EndOfBlock())
        {
            using var field = block.ReadSubBlock();
            switch (field.ID)
            {
                case TokenID.UiD: uid = field.ReadUInt(); break;
                case TokenID.FileName: fileName = field.ReadString(); break;
                case TokenID.Position: position = ReadPosition(field); break;
                case TokenID.QDirection: direction = ReadDirection(field); break;
                case TokenID.Matrix3x3: matrix = ReadMatrix(field); break;
                case TokenID.StaticFlags: staticFlags = field.ReadFlags(); break;
                case TokenID.VDbId: worldDatabaseId = field.ReadUInt(); break;
                case TokenID.TreeTexture: treeTexture = field.ReadString(); break;
                case TokenID.ScaleRange: forestScaleRange = ReadFloatRange(field); break;
                case TokenID.Area: forestArea = ReadFloatPair(field); break;
                case TokenID.Population: forestPopulation = field.ReadInt(); break;
                case TokenID.TreeSize: forestTreeSize = ReadFloatPair(field); break;
                default: AddDiagnostic(diagnostics, $"Skipped unsupported STATIC field token 0x{(ushort)field.ID:X4}."); field.Skip(); break;
            }
        }

        return new StaticWorldObject(uid, fileName, position, direction, matrix, staticFlags,
            worldDatabaseId, objectKind, treeTexture, forestScaleRange, forestArea, forestPopulation, forestTreeSize);
    }

    private static WorldFloatRange ReadFloatRange(SBR block)
    {
        block.VerifyID(TokenID.ScaleRange);
        var value = new WorldFloatRange(block.ReadFloat(), block.ReadFloat());
        block.VerifyEndOfBlock();
        return value;
    }

    private static WorldFloatPair ReadFloatPair(SBR block)
    {
        var first = block.ReadFloat();
        var second = block.ReadFloat();
        block.VerifyEndOfBlock();
        return new WorldFloatPair(first, second);
    }

    private static TrackWorldObject ReadTrackObject(SBR block, List<string> diagnostics)
    {
        var uid = 0u;
        uint sectionIndex = 0;
        float elevation = 0;
        uint collideFlags = 0;
        string? fileName = null;
        WorldTilePosition? position = null;
        WorldQuaternion? direction = null;
        uint staticFlags = 0;
        uint worldDatabaseId = 0;
        WorldJunctionPosition? junctionPosition = null;

        while (!block.EndOfBlock())
        {
            using var field = block.ReadSubBlock();
            switch (field.ID)
            {
                case TokenID.UiD: uid = field.ReadUInt(); break;
                case TokenID.SectionIdx: sectionIndex = field.ReadUInt(); break;
                case TokenID.Elevation: elevation = field.ReadFloat(); break;
                case TokenID.CollideFlags: collideFlags = field.ReadUInt(); break;
                case TokenID.FileName: fileName = field.ReadString(); break;
                case TokenID.Position: position = ReadPosition(field); break;
                case TokenID.QDirection: direction = ReadDirection(field); break;
                case TokenID.StaticFlags: staticFlags = field.ReadFlags(); break;
                case TokenID.VDbId: worldDatabaseId = field.ReadUInt(); break;
                case TokenID.JNodePosn: junctionPosition = ReadJunctionPosition(field); break;
                default: AddDiagnostic(diagnostics, $"Skipped unsupported TrackObj field token 0x{(ushort)field.ID:X4}."); field.Skip(); break;
            }
        }

        return new TrackWorldObject(uid, sectionIndex, elevation, collideFlags, fileName,
            position, direction, staticFlags, worldDatabaseId, junctionPosition);
    }

    private static void AddDiagnostic(List<string> diagnostics, string message)
    {
        if (!diagnostics.Contains(message, StringComparer.Ordinal))
            diagnostics.Add(message);
    }

    private static WorldTilePosition ReadPosition(SBR block)
    {
        block.VerifyID(TokenID.Position);
        var value = new WorldTilePosition(block.ReadFloat(), block.ReadFloat(), block.ReadFloat());
        block.VerifyEndOfBlock();
        return value;
    }

    private static WorldQuaternion ReadDirection(SBR block)
    {
        block.VerifyID(TokenID.QDirection);
        var value = new WorldQuaternion(block.ReadFloat(), block.ReadFloat(), block.ReadFloat(), block.ReadFloat());
        block.VerifyEndOfBlock();
        return value;
    }

    private static WorldMatrix3x3 ReadMatrix(SBR block)
    {
        block.VerifyID(TokenID.Matrix3x3);
        var value = new WorldMatrix3x3(
            block.ReadFloat(), block.ReadFloat(), block.ReadFloat(),
            block.ReadFloat(), block.ReadFloat(), block.ReadFloat(),
            block.ReadFloat(), block.ReadFloat(), block.ReadFloat());
        block.VerifyEndOfBlock();
        return value;
    }

    private static WorldJunctionPosition ReadJunctionPosition(SBR block)
    {
        block.VerifyID(TokenID.JNodePosn);
        var value = new WorldJunctionPosition(
            block.ReadInt(), block.ReadInt(),
            block.ReadFloat(), block.ReadFloat(), block.ReadFloat());
        block.VerifyEndOfBlock();
        return value;
    }

    public static bool TryParseTileCoordinates(string path, out int tileX, out int tileZ)
    {
        var fileName = path[(path.LastIndexOf('/') + 1)..];
        tileX = 0;
        tileZ = 0;
        return fileName.Length >= 15 && char.ToUpperInvariant(fileName[0]) == 'W' &&
            int.TryParse(fileName.AsSpan(1, 7), NumberStyles.Integer, CultureInfo.InvariantCulture, out tileX) &&
            int.TryParse(fileName.AsSpan(8, 7), NumberStyles.Integer, CultureInfo.InvariantCulture, out tileZ);
    }

    private static (int TileX, int TileZ) ParseTileCoordinates(string path)
    {
        if (!TryParseTileCoordinates(path, out var tileX, out var tileZ))
        {
            var fileName = path[(path.LastIndexOf('/') + 1)..];
            throw new InvalidDataException($"World tile filename '{fileName}' does not contain the expected signed tile coordinates.");
        }

        return (tileX, tileZ);
    }
}

public sealed class WorldTileData
{
    public string SourcePath { get; }
    public int TileX { get; }
    public int TileZ { get; }
    public IReadOnlyList<StaticWorldObject> StaticObjects { get; }
    public IReadOnlyList<TrackWorldObject> TrackObjects { get; }
    public IReadOnlyList<string> Diagnostics { get; }

    public WorldTileData(string sourcePath, int tileX, int tileZ,
        IEnumerable<StaticWorldObject> staticObjects,
        IEnumerable<TrackWorldObject> trackObjects,
        IEnumerable<string> diagnostics)
    {
        SourcePath = sourcePath;
        TileX = tileX;
        TileZ = tileZ;
        StaticObjects = Array.AsReadOnly(staticObjects.ToArray());
        TrackObjects = Array.AsReadOnly(trackObjects.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }
}

public sealed record WorldTilePosition(float X, float Y, float Z);
public sealed record WorldQuaternion(float A, float B, float C, float D);
public sealed record WorldMatrix3x3(
    float AX, float AY, float AZ,
    float BX, float BY, float BZ,
    float CX, float CY, float CZ);
public sealed record WorldJunctionPosition(int TileX, int TileZ, float X, float Y, float Z);

public sealed record WorldFloatRange(float Minimum, float Maximum);
public sealed record WorldFloatPair(float First, float Second);

public sealed record StaticWorldObject(
    uint Uid,
    string? FileName,
    WorldTilePosition? Position,
    WorldQuaternion? Direction,
    WorldMatrix3x3? Matrix,
    uint StaticFlags,
    uint WorldDatabaseId,
    string ObjectKind = "Static",
    string? TreeTexture = null,
    WorldFloatRange? ForestScaleRange = null,
    WorldFloatPair? ForestArea = null,
    int? ForestPopulation = null,
    WorldFloatPair? ForestTreeSize = null);

public sealed record TrackWorldObject(
    uint Uid,
    uint SectionIndex,
    float Elevation,
    uint CollideFlags,
    string? FileName,
    WorldTilePosition? Position,
    WorldQuaternion? Direction,
    uint StaticFlags,
    uint WorldDatabaseId,
    WorldJunctionPosition? JunctionPosition);

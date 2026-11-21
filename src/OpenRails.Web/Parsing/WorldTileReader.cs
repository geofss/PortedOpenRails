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
                case TokenID.CollideObject:
                    staticObjects.Add(ReadStaticObject(objectBlock));
                    break;
                case TokenID.TrackObj:
                    trackObjects.Add(ReadTrackObject(objectBlock));
                    break;
                default:
                    objectBlock.Skip();
                    break;
            }
        }

        return new WorldTileData(normalized, tileX, tileZ, staticObjects, trackObjects, diagnostics);
    }

    private static StaticWorldObject ReadStaticObject(SBR block)
    {
        var uid = 0u;
        string? fileName = null;
        WorldTilePosition? position = null;
        WorldQuaternion? direction = null;
        WorldMatrix3x3? matrix = null;
        uint staticFlags = 0;
        uint worldDatabaseId = 0;

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
                default: field.Skip(); break;
            }
        }

        return new StaticWorldObject(uid, fileName, position, direction, matrix, staticFlags, worldDatabaseId);
    }

    private static TrackWorldObject ReadTrackObject(SBR block)
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
                default: field.Skip(); break;
            }
        }

        return new TrackWorldObject(uid, sectionIndex, elevation, collideFlags, fileName,
            position, direction, staticFlags, worldDatabaseId, junctionPosition);
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

    private static (int TileX, int TileZ) ParseTileCoordinates(string path)
    {
        var fileName = path[(path.LastIndexOf('/') + 1)..];
        if (fileName.Length < 15 || char.ToUpperInvariant(fileName[0]) != 'W' ||
            !int.TryParse(fileName.AsSpan(1, 7), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tileX) ||
            !int.TryParse(fileName.AsSpan(8, 7), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tileZ))
            throw new InvalidDataException($"World tile filename '{fileName}' does not contain the expected signed tile coordinates.");

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

public sealed record StaticWorldObject(
    uint Uid,
    string? FileName,
    WorldTilePosition? Position,
    WorldQuaternion? Direction,
    WorldMatrix3x3? Matrix,
    uint StaticFlags,
    uint WorldDatabaseId);

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

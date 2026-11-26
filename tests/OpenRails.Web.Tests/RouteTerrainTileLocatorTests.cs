using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class RouteTerrainTileLocatorTests
{
    [Fact]
    public void UsesUpstreamTileNameEncodingForSmallTerrainTiles()
    {
        var name = TerrainTileName.FromTileXZ(0, 0, TerrainTileZoom.Small);

        Assert.StartsWith("-", name);
        Assert.Equal(9, name.Length);
        Assert.Equal("-7FFFFFFC", name);
    }

    [Theory]
    [InlineData(-16384, -16384, TerrainTileZoom.DMLarge)]
    [InlineData(-8192, 8192, TerrainTileZoom.DMSmall)]
    [InlineData(0, 0, TerrainTileZoom.Large)]
    [InlineData(1234, -5678, TerrainTileZoom.Small)]
    public void TileNamesMatchThePinnedUpstreamImplementation(int tileX, int tileZ, TerrainTileZoom zoom)
    {
        var upstream = Orts.Formats.Msts.TileName.FromTileXZ(
            tileX, tileZ, (Orts.Formats.Msts.TileName.Zoom)(int)zoom);

        Assert.Equal(upstream, TerrainTileName.FromTileXZ(tileX, tileZ, zoom));
    }

    [Theory]
    [InlineData(-123, 456, TerrainTileZoom.DMLarge)]
    [InlineData(123, -456, TerrainTileZoom.DMSmall)]
    [InlineData(-123, -456, TerrainTileZoom.Large)]
    [InlineData(123, 456, TerrainTileZoom.Small)]
    public void TileSnappingMatchesThePinnedUpstreamImplementation(int tileX, int tileZ, TerrainTileZoom zoom)
    {
        var expectedX = tileX;
        var expectedZ = tileZ;
        Orts.Formats.Msts.TileName.Snap(
            ref expectedX, ref expectedZ, (Orts.Formats.Msts.TileName.Zoom)(int)zoom);

        TerrainTileName.Snap(ref tileX, ref tileZ, zoom);

        Assert.Equal(expectedX, tileX);
        Assert.Equal(expectedZ, tileZ);
    }

    [Fact]
    public void LocatesTerrainTilesTouchedByTheTrackNetwork()
    {
        var source = new TrackVectorSection(1, 0, 0, 0, 0, 0, 1, "00", 0, 0, 0, 0, 0, 0, 0, 0);
        var geometry = new TrackSectionGeometry(1, null, 10, null, null, false);
        var placement = new TrackSectionPlacement(source, geometry, new TrackWorldPosition(0, 0, 0), 10);
        var path = new TrackVectorPath(1, new[] { 2, 3 }, new[] { placement }, 10);
        var vector = new TrackNode(1, TrackNodeKind.Vector, null, null, new[] { source },
            new[] { new TrackPin(2, 0), new TrackPin(3, 1) });
        var nodeA = new TrackNode(2, TrackNodeKind.End, null, null,
            Array.Empty<TrackVectorSection>(), Array.Empty<TrackPin>());
        var nodeB = new TrackNode(3, TrackNodeKind.End, null, null,
            Array.Empty<TrackVectorSection>(), Array.Empty<TrackPin>());
        var database = new TrackDatabase(new TrackNode?[] { null, vector, nodeA, nodeB }, Array.Empty<string>());
        var network = new TrackNetwork(database, new[] { path }, Array.Empty<string>());

        using var store = CreateStore(
            ("ROUTES/Test/TILES/-7FFFFFFC.t", "terrain fixture"),
            ("ROUTES/Test/TILES/-7FFFFFFC_y.raw", "altitude fixture"));

        var result = new RouteTerrainTileLocator().Locate(store, "ROUTES/Test", network);

        Assert.True(result.Found.Count == 1, string.Join(" | ", result.Missing.Select(item => item.ExpectedPath)));
        var tile = Assert.Single(result.Found);
        Assert.Equal((0, 0), (tile.TileX, tile.TileZ));
        Assert.Equal("ROUTES/Test/TILES/-7FFFFFFC.t", tile.Path);
        Assert.Equal("ROUTES/Test/TILES/-7FFFFFFC_y.raw", tile.AltitudePath);
        Assert.Null(tile.FlagsPath);
        Assert.Empty(result.Missing);
    }

    [Fact]
    public void SnapsTileCoordinatesToTheRequestedZoomGrid()
    {
        var x = 3;
        var z = -3;

        TerrainTileName.Snap(ref x, ref z, TerrainTileZoom.Large);

        Assert.Equal(2, x);
        Assert.Equal(-4, z);
    }

    private static ZipContentStore CreateStore(params (string Path, string Content)[] files)
    {
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Path);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(file.Content);
            }
        }
        memory.Position = 0;
        return new ZipContentStore(memory);
    }
}

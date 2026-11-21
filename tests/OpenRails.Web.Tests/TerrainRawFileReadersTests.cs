using System.IO.Compression;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TerrainRawFileReadersTests
{
    [Fact]
    public void ReadsLittleEndianAltitudeSamplesInOriginalRowMajorOrder()
    {
        using var store = CreateStore(
            ("ROUTES/Test/TILES/tile_y.raw", new byte[]
            {
                1, 0, 2, 0,
                3, 0, 4, 0
            }));

        var altitude = new TerrainAltitudeReader().Read(store, "ROUTES/Test/TILES/tile_y.raw", 2);

        Assert.Equal((ushort)1, altitude.GetRawSample(0, 0));
        Assert.Equal((ushort)2, altitude.GetRawSample(1, 0));
        Assert.Equal((ushort)3, altitude.GetRawSample(0, 1));
        Assert.Equal((ushort)4, altitude.GetRawSample(1, 1));
        Assert.Equal(12, altitude.GetElevationMeters(1, 1, floorMeters: 10, scaleMetersPerUnit: 0.5f));
    }

    [Fact]
    public void ReadsOptionalTerrainVertexHiddenFlags()
    {
        using var store = CreateStore(
            ("ROUTES/Test/TILES/tile_f.raw", new byte[] { 0, 4, 0, 4 }));

        var flags = new TerrainFlagsReader().Read(store, "ROUTES/Test/TILES/tile_f.raw", 2);

        Assert.False(flags.IsVertexHidden(0, 0));
        Assert.True(flags.IsVertexHidden(1, 0));
        Assert.False(flags.IsVertexHidden(0, 1));
        Assert.True(flags.IsVertexHidden(1, 1));
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

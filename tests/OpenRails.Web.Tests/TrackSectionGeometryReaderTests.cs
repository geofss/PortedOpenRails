using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackSectionGeometryReaderTests
{
    [Fact]
    public void ReadsGlobalStraightAndCurveGeometryAndComputesOffsets()
    {
        using var store = CreateStore(("GLOBAL/tsection.dat", """
            SIMISA@@@@@@@@@@JINX0T0t
            TrackSections (
                10
                TrackSection ( 1 SectionSize ( 2m 25m ) )
                TrackSection ( 2 SectionCurve ( 300m 30 ) )
            )
            """));

        var catalog = new TrackSectionGeometryReader().ReadGlobal(store, "GLOBAL/tsection.dat");

        Assert.Equal(2, catalog.Count);
        var straight = catalog.GetRequired(1);
        Assert.True(straight.IsStraight);
        Assert.Equal(25, straight.CalculatedLengthMeters, 3);
        Assert.Equal((0d, 25d), straight.GetEndOffset());

        var curve = catalog.GetRequired(2);
        Assert.False(curve.IsStraight);
        Assert.Equal(300 * Math.PI / 6, curve.CalculatedLengthMeters, 3);
        var offset = curve.GetEndOffset();
        Assert.Equal(40.192, offset.X, 2);
        Assert.Equal(150, offset.Z, 2);
    }

    [Fact]
    public void ReadsGlobalTrackShapeFileNameAndPathDefinitions()
    {
        using var store = CreateStore(("GLOBAL/tsection.dat", """
            SIMISA@@@@@@@@@@JINX0T0t
            TrackSections ( 1 TrackSection ( 1 SectionSize ( 1m 10m ) ) )
            TrackShapes (
                5
                TrackShape ( 2
                    FileName ( "track/trackshape.s" )
                    NumPaths ( 1 )
                    MainRoute ( 0 )
                    ClearanceDist ( 2m )
                    SectionIdx ( 2 1 2 3 0.25 4 5 )
                    TunnelShape ( 1 )
                    RoadShape ( 0 )
                    ORTSRackShape ( 0 )
                )
            )
            """));

        var catalog = new TrackSectionGeometryReader().ReadGlobal(store, "GLOBAL/tsection.dat");

        var shape = catalog.Shapes.GetRequired(2);
        Assert.Equal("track/trackshape.s", shape.FileName);
        Assert.Equal((uint)1, shape.NumberOfPaths);
        Assert.True(shape.IsTunnel);
        Assert.False(shape.IsRoad);
        var path = Assert.Single(shape.Paths);
        Assert.Equal(2, path.TrackSectionIndices.Count);
        Assert.Equal((uint)4, path.TrackSectionIndices[0]);
        Assert.Equal((uint)5, path.TrackSectionIndices[1]);
        Assert.Equal(1, path.OffsetX);
        Assert.Equal(2, path.OffsetY);
        Assert.Equal(3, path.OffsetZ);
        Assert.Equal(0.25, path.AngleRadians);
    }

    [Fact]
    public void MirrorsNegativeCurveEndOffset()
    {
        var curve = new TrackSectionGeometry(3, null, null, 300, -30, false);
        var offset = curve.GetEndOffset();

        Assert.Equal(-40.192, offset.X, 2);
        Assert.Equal(150, offset.Z, 2);
    }

    [Fact]
    public void ReadsRouteSpecificStraightAndCurveDefinitions()
    {
        using var store = CreateStore(("ROUTES/Test/tsection.dat", """
            SIMISA@@@@@@@@@@JINX0T0t
            TrackSections (
                20
                TrackSection ( SectionCurve ( ) 1 12m 0 )
                TrackSection ( SectionCurve ( ) 2 0.5235987756 250 )
            )
            """));

        var catalog = new TrackSectionGeometryReader().ReadRouteSections(store, "ROUTES/Test/tsection.dat");

        Assert.Equal(2, catalog.Count);
        Assert.True(catalog.GetRequired(1).IsStraight);
        Assert.Equal(12, catalog.GetRequired(1).LengthMeters);
        Assert.Equal(30f, catalog.GetRequired(2).AngleDegrees!.Value, 2);
        Assert.True(catalog.GetRequired(2).IsRouteSpecific);
    }

    [Fact]
    public void AcceptsSparseRouteDynamicPathTableLikeUpstreamOpenRails()
    {
        using var store = CreateStore(("ROUTES/SCE/TSECTION.DAT", """
            SIMISA@@@@@@@@@@JINX0T0t
            TrackSections ( 1 TrackSection ( SectionCurve ( ) 10 8m 0 ) )
            SectionIdx (
                241
                TrackPath ( 40000 2 10 11 )
                TrackPath ( 40236 1 12 )
            )
            """));

        var catalog = new TrackSectionGeometryReader().ReadRouteSections(store, "ROUTES/SCE/TSECTION.DAT");

        Assert.Equal(2, catalog.DynamicPaths.Count);
        Assert.True(catalog.TryGetDynamicPath(40000, out var first));
        Assert.Equal(new uint[] { 10, 11 }, first.TrackSectionIndices);
        Assert.True(catalog.TryGetDynamicPath(40236, out var last));
        Assert.Equal(new uint[] { 12 }, last.TrackSectionIndices);
    }

    [Fact]
    public void ReadsRouteSpecificDynamicTrackPathMappings()
    {
        using var store = CreateStore(("ROUTES/Test/tsection.dat", """
            SIMISA@@@@@@@@@@JINX0T0t
            TrackSections ( 1 TrackSection ( SectionCurve ( ) 10 8m 0 ) )
            SectionIdx (
                2
                TrackPath ( 100000 2 10 11 )
                TrackPath ( 100001 3 12 13 14 )
            )
            """));

        var catalog = new TrackSectionGeometryReader().ReadRouteSections(store, "ROUTES/Test/tsection.dat");

        Assert.Equal(2, catalog.DynamicPaths.Count);
        Assert.True(catalog.TryGetDynamicPath(100000, out var first));
        Assert.Equal(new uint[] { 10, 11 }, first.TrackSectionIndices);
        Assert.True(catalog.TryGetDynamicPath(100001, out var second));
        Assert.Equal(new uint[] { 12, 13, 14 }, second.TrackSectionIndices);
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

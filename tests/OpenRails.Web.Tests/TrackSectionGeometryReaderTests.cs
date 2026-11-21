using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackSectionGeometryReaderTests
{
    [Fact]
    public void ReadsGlobalStraightAndCurveGeometry()
    {
        using var store = Store(("GLOBAL/tsection.dat", """
            SIMISA@@@@@@@@@@JINX0T0t
            TrackSections ( 10
                TrackSection ( 1 SectionSize ( 2m 25m ) )
                TrackSection ( 2 SectionCurve ( 300m 30 ) )
            )
            """));
        var catalog = new TrackSectionGeometryReader().ReadGlobal(store, "GLOBAL/tsection.dat");
        Assert.Equal(2, catalog.Count);
        var straight = catalog.GetRequired(1);
        Assert.True(straight.IsStraight);
        Assert.Equal(25, straight.CalculatedLengthMeters, 3);
        var curve = catalog.GetRequired(2);
        Assert.Equal(300 * Math.PI / 6, curve.CalculatedLengthMeters, 3);
        var end = curve.GetEndOffset();
        Assert.Equal(40.192, end.X, 2);
        Assert.Equal(150, end.Z, 2);
    }

    [Fact]
    public void ReadsRouteSpecificStraightAndCurve()
    {
        using var store = Store(("ROUTES/Test/tsection.dat", """
            SIMISA@@@@@@@@@@JINX0T0t
            TrackSections ( 20
                TrackSection ( SectionCurve ( 1 12m 0 ) )
                TrackSection ( SectionCurve ( 2 0.5235987756 250 ) )
            )
            """));
        var catalog = new TrackSectionGeometryReader().ReadRouteSections(store, "ROUTES/Test/tsection.dat");
        Assert.True(catalog.GetRequired(1).IsStraight);
        Assert.Equal(12f, catalog.GetRequired(1).LengthMeters!.Value);
        Assert.Equal(30f, catalog.GetRequired(2).AngleDegrees!.Value, 2);
        Assert.True(catalog.GetRequired(2).IsRouteSpecific);
    }

    private static ZipContentStore Store(params (string Path,string Content)[] files)
    {
        var stream=new MemoryStream();
        using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true))
        foreach(var f in files){var e=zip.CreateEntry(f.Path);using var w=new StreamWriter(e.Open(),Encoding.UTF8);w.Write(f.Content);}
        stream.Position=0; return new ZipContentStore(stream);
    }
}

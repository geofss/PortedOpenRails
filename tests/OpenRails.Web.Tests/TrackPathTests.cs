using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackPathTests
{
    [Fact]
    public void MapsDistanceAlongStraightAndCurvePathToPositionAndHeading()
    {
        var path = new TrackPath(new[]
        {
            new TrackSectionGeometry(1,2,100,null,null,false),
            new TrackSectionGeometry(2,null,null,100,90,false)
        });
        Assert.Equal(257.0796, path.LengthMeters, 3);
        var straightPoint=path.GetPointAtDistance(50);
        Assert.Equal(0,straightPoint.X,4);
        Assert.Equal(50,straightPoint.Z,4);
        var curveMid=path.GetPointAtDistance(100+Math.PI*50/4);
        Assert.Equal(29.289,curveMid.X,2);
        Assert.Equal(170.710,curveMid.Z,2);
        Assert.Equal(Math.PI/4,curveMid.HeadingRadians,3);
    }

    [Fact]
    public void ClampsDistanceToPathBoundaries()
    {
        var path=new TrackPath(new[]{new TrackSectionGeometry(1,null,10,null,null,false)});
        Assert.Equal(0,path.GetPointAtDistance(-5).DistanceMeters);
        Assert.Equal(10,path.GetPointAtDistance(100).DistanceMeters);
    }
}

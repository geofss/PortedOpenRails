using Orts.Parsers.Msts;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class InterpolatorTests
{
    [Fact]
    public void InterpolatesOneDimensionalTablesLinearly()
    {
        var curve = new Interpolator(2);
        curve[0] = 0;
        curve[10] = 100;

        Assert.Equal(50, curve[5], 5);
        Assert.Equal(100, curve[10], 5);
    }

    [Fact]
    public void InterpolatesDieselTractionTablesAcrossThrottleAndSpeed()
    {
        var zeroThrottle = new Interpolator(2);
        zeroThrottle[0] = 0;
        zeroThrottle[10] = 0;
        var fullThrottle = new Interpolator(2);
        fullThrottle[0] = 10_000;
        fullThrottle[10] = 5_000;

        var table = new InterpolatorDiesel2D(2);
        table[0] = zeroThrottle;
        table[1] = fullThrottle;

        Assert.Equal(3_750, table.Get(0.5f, 5), 3);
    }

    [Fact]
    public void PreservesNegativeTractionCurveCapability()
    {
        var negativeCurve = new Interpolator(2);
        negativeCurve[0] = -100;
        negativeCurve[10] = 100;
        var positiveCurve = new Interpolator(2);
        positiveCurve[0] = 0;
        positiveCurve[10] = 200;
        var table = new InterpolatorDiesel2D(2);
        table[0] = negativeCurve;
        table[1] = positiveCurve;

        table.HasNegativeValue();

        Assert.True(table.AcceptsNegativeValues());
    }
}

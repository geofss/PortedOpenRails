using ORTS.Common;
using Xunit;

namespace PortedOpenRails.Tests;

public class SmoothedDataTests
{
    [Fact]
    public void FirstPositiveUpdate_InitializesSmoothedValue()
    {
        var data = new SmoothedData();
        data.Update(1f, 12.5f);
        Assert.Equal(12.5f, data.Value);
        Assert.Equal(12.5f, data.SmoothedValue);
    }

    [Fact]
    public void ZeroDurationUpdate_DoesNotReplaceExistingSmoothedValue()
    {
        var data = new SmoothedData();
        data.Update(1f, 10f);
        data.Update(0f, 20f);
        Assert.Equal(10f, data.SmoothedValue);
        Assert.Equal(10f, data.Value);
    }

    [Fact]
    public void ForceSmoothValue_ReplacesSmoothedValue()
    {
        var data = new SmoothedData();
        data.ForceSmoothValue(42f);
        Assert.Equal(42f, data.SmoothedValue);
    }

    [Fact]
    public void RepeatedUpdates_ConvergeTowardNewValue()
    {
        var data = new SmoothedData();
        data.Update(1f, 0f);
        data.Update(1f, 10f);
        var first = data.SmoothedValue;
        data.Update(1f, 10f);
        Assert.True(data.SmoothedValue > first);
        Assert.True(data.SmoothedValue < 10f);
    }
}

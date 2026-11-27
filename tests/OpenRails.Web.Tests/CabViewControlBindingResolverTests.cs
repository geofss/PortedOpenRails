using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class CabViewControlBindingResolverTests
{
    [Theory]
    [InlineData("throttle", "lever", CabViewControlBinding.ThrottleLever)]
    [InlineData("THROTTLE_DISPLAY", "digital", CabViewControlBinding.ThrottleDisplay)]
    [InlineData("TRAIN_BRAKE", "lever", CabViewControlBinding.TrainBrakeLever)]
    [InlineData("DIRECTION", "lever", CabViewControlBinding.DirectionLever)]
    [InlineData("SPEEDOMETER", "dial", CabViewControlBinding.Speedometer)]
    [InlineData("BRAKE_PIPE", "digital", CabViewControlBinding.BrakePipe)]
    [InlineData("BRAKE_CYL", "digital", CabViewControlBinding.BrakeCylinder)]
    [InlineData("DOORS_DISPLAY", "multistate", CabViewControlBinding.Doors)]
    [InlineData("EMERGENCY_BRAKE", "twostate", CabViewControlBinding.EmergencyBrake)]
    public void ResolvesSupportedNativeCabControlTypes(string controlType, string kind, CabViewControlBinding expected)
    {
        var control = new CabViewControlDefinition(kind, controlType, "", "", "", "", "",
            0, 0, 1, 1, 0, 100, 1, 0, 0, 0, 0, 0, Array.Empty<int>());
        Assert.Equal(expected, CabViewControlBindingResolver.Resolve(control));
    }

    [Fact]
    public void DoesNotMapIndependentEngineBrakeToTrainBrake()
    {
        var control = new CabViewControlDefinition("lever", "ENGINE_BRAKE", "", "", "", "", "",
            0, 0, 1, 1, 0, 100, 1, 0, 0, 0, 0, 0, Array.Empty<int>());
        Assert.Equal(CabViewControlBinding.NotConnected, CabViewControlBindingResolver.Resolve(control));
    }
}

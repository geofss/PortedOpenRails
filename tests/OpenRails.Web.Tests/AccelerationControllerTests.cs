using ORTS.Common;
using Xunit;

namespace PortedOpenRails.Tests;

public class AccelerationControllerTests
{
    [Fact]
    public void ProportionalTerm_RespondsToTargetAcceleration()
    {
        var controller = new AccelerationController(0.01f, 0f);
        controller.Adjust(10f);

        controller.Update(1f, 5f, 0f);

        Assert.Equal(0.5f, controller.PPercent, 4);
        Assert.Equal(0f, controller.IPercent, 4);
        Assert.Equal(0.5f, controller.Percent, 4);
    }

    [Fact]
    public void IntegralTerm_AccumulatesErrorOverElapsedTime()
    {
        var controller = new AccelerationController(0f, 0.01f);
        controller.Adjust(10f);

        controller.Update(2f, 2f, 0f);

        Assert.Equal(0.2f, controller.IPercent, 4);
        Assert.Equal(0.2f, controller.Percent, 4);
    }

    [Fact]
    public void DerivativeTerm_RespondsToChangeInError()
    {
        var controller = new AccelerationController(0f, 0f, 0.01f);
        controller.Adjust(10f);

        controller.Update(1f, 2f, 1f);

        Assert.Equal(0.1f, controller.DPercent, 4);
        Assert.Equal(0.1f, controller.Percent, 4);
    }

    [Fact]
    public void Output_IsClampedToMaximumPercent()
    {
        var controller = new AccelerationController(1f, 0f);
        controller.Adjust(10f);

        controller.Update(1f, 20f, 0f, 0f, 100f);

        Assert.Equal(100f, controller.Percent);
    }

    [Fact]
    public void Output_IsClampedToMinimumPercent()
    {
        var controller = new AccelerationController(1f, 0f);
        controller.Adjust(10f);

        controller.Update(1f, -20f, 0f, 0f, 100f);

        Assert.Equal(0f, controller.Percent);
    }

    [Fact]
    public void DeactivatingController_ResetsItsOutputAndState()
    {
        var controller = new AccelerationController(0.01f, 0f);
        controller.Adjust(10f);
        controller.Update(1f, 5f, 0f);

        controller.Active = false;

        Assert.False(controller.Active);
        Assert.Equal(0f, controller.Percent);
    }
}

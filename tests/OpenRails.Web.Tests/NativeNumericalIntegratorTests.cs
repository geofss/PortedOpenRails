using OpenRails.Web.Simulation.Upstream;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class NativeNumericalIntegratorTests
{
    [Theory]
    [InlineData(NativeIntegratorMethod.EulerBackward)]
    [InlineData(NativeIntegratorMethod.EulerBackModified)]
    [InlineData(NativeIntegratorMethod.EulerForward)]
    [InlineData(NativeIntegratorMethod.RungeKutta2)]
    [InlineData(NativeIntegratorMethod.RungeKutta4)]
    [InlineData(NativeIntegratorMethod.AdamsMoulton)]
    public void IntegratesConstantDerivativeAcrossNativeSolverModes(NativeIntegratorMethod method)
    {
        var integrator = new NativeNumericalIntegrator(2, method);
        var result = integrator.Integrate(0.5f, _ => 4f);
        Assert.Equal(4f, result, 3);
    }

    [Fact]
    public void RungeKutta4TracksExponentialThermalRelaxation()
    {
        var integrator = new NativeNumericalIntegrator(100, NativeIntegratorMethod.RungeKutta4)
        {
            MinStep = 0.0005f
        };
        var result = integrator.Integrate(1, temperature => 0.5f * (20f - temperature));
        Assert.InRange(result, 68.50f, 68.55f);
    }

    [Fact]
    public void LimitedIntegratorClampsAtConfiguredPhysicalBoundary()
    {
        var integrator = new NativeNumericalIntegrator(0, NativeIntegratorMethod.EulerBackward)
        {
            IsLimited = true,
            Minimum = 0,
            Maximum = 10
        };
        Assert.Equal(10, integrator.Integrate(1, _ => 100));
    }

    [Fact]
    public void SnapshotRestoresSolverHistoryAndCurrentValue()
    {
        var original = new NativeNumericalIntegrator(1, NativeIntegratorMethod.RungeKutta4);
        original.Integrate(0.25f, value => -value);
        var snapshot = original.CaptureState();
        var expected = original.Integrate(0.25f, value => -value);

        var restored = new NativeNumericalIntegrator(1, NativeIntegratorMethod.RungeKutta4);
        restored.RestoreState(snapshot);
        Assert.Equal(expected, restored.Integrate(0.25f, value => -value), 6);
    }

    [Fact]
    public void RejectsNonFiniteInputsAndUnsupportedNativeSolver()
    {
        var integrator = new NativeNumericalIntegrator();
        Assert.Throws<ArgumentOutOfRangeException>(() => integrator.Integrate(float.NaN, _ => 1));
        integrator.Method = NativeIntegratorMethod.NewtonRaphson;
        Assert.Throws<NotSupportedException>(() => integrator.Integrate(0.1f, _ => 1));
    }
}

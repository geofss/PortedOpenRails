using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class LocomotiveTractionModelTests
{
    [Fact]
    public void ReadsEngineLimitsAndOriginalTwoDimensionalTractionCurve()
    {
        using var store = CreateStore(("TRAINS/TRAINSET/Test/Engine.eng", """
            SIMISA@@@@@@@@@@JINX0t1t
            Engine (
                MaxPower ( 2000000 )
                MaxForce ( 200000 )
                MaxContinuousForce ( 150000 )
                MaxVelocity ( 120 )
                ORTSUnloadingSpeed ( 40 )
                ORTSMaxTractiveForceCurves (
                    0 ( 0 0 10 0 )
                    1 ( 0 250000 10 200000 )
                )
            )
            """));

        var metadata = new LocomotiveTractionMetadataReader().Read(
            store, "TRAINS/TRAINSET/Test/Engine.eng");

        Assert.Equal(200_000, metadata.MaxForceNewtons);
        Assert.Equal(150_000, metadata.MaxContinuousForceNewtons);
        Assert.Equal(2_000_000, metadata.MaxPowerWatts);
        Assert.Equal(120, metadata.MaxSpeedMetersPerSecond);
        Assert.Equal(40, metadata.UnloadingSpeedMetersPerSecond);
        Assert.NotNull(metadata.TractionForceCurves);
        Assert.True(metadata.TractionForceCurves!.AcceptsNegativeValues() == false);
    }

    [Fact]
    public void EvaluatesOriginalThrottleSpeedCurveAndPowerReduction()
    {
        var metadata = CreateCurveMetadata(maxPower: 2_000_000);
        var model = new LocomotiveTractionModel(metadata);

        Assert.Equal(112_500, model.CalculateTractiveForce(50, 5), 2);
        Assert.Equal(56_250, model.CalculateTractiveForce(50, 5, powerReduction: 0.5f), 2);
    }

    [Fact]
    public void AppliesPowerCapAndContinuousForceThermalLimit()
    {
        var powerLimited = new LocomotiveTractionModel(CreateCurveMetadata(maxPower: 500_000));
        Assert.Equal(50_000, powerLimited.CalculateTractiveForce(100, 10), 2);

        var heatLimited = new LocomotiveTractionModel(CreateCurveMetadata(maxPower: 2_000_000));
        Assert.Equal(150_000,
            heatLimited.CalculateTractiveForce(100, 5, averageForceNewtons: 200_000), 2);
    }

    [Fact]
    public void AppliesQuadraticThrottleResponseAndUnloadingSpeedPowerDecay()
    {
        var metadata = new LocomotiveTractionMetadata(
            "Diesel", 200_000, 150_000, 2_000_000, 120, 10, null, "diesel.eng");
        var model = new LocomotiveTractionModel(metadata);

        var linear = model.CalculateTractiveForce(50, 15);
        var quadratic = model.CalculateTractiveForce(
            50, 15, LocomotiveThrottleResponse.Quadratic);

        Assert.Equal(33_333.33, linear, 2);
        Assert.Equal(16_666.67, quadratic, 2);
    }

    [Fact]
    public void PreservesNegativeValuesWhenOriginalCurveAllowsThem()
    {
        var negative = new Orts.Parsers.Msts.Interpolator(2);
        negative[0] = -100;
        negative[10] = 100;
        var positive = new Orts.Parsers.Msts.Interpolator(2);
        positive[0] = 0;
        positive[10] = 200;
        var curves = new Orts.Parsers.Msts.InterpolatorDiesel2D(2);
        curves[0] = positive;
        curves[1] = negative;
        curves.HasNegativeValue();
        var metadata = new LocomotiveTractionMetadata(
            "Legacy", 200, 150, 0, 100, 0, curves, "legacy.eng");

        Assert.Equal(-100, new LocomotiveTractionModel(metadata).CalculateTractiveForce(100, 0));
    }

    [Fact]
    public void UsesBasicMaximumForceFallbackAndMaximumSpeedCutoff()
    {
        var metadata = new LocomotiveTractionMetadata(
            "Basic", 100_000, 80_000, 1_000_000, 50, 0, null, "basic.eng");
        var model = new LocomotiveTractionModel(metadata);

        Assert.Equal(50_000, model.CalculateTractiveForce(50, 10), 2);
        Assert.Equal(0, model.CalculateTractiveForce(100, 51));
    }

    [Fact]
    public void ReadsNativeTractionForceRampRates()
    {
        using var store = CreateStore(("TRAINS/TRAINSET/Test/Ramp.eng", """
            SIMISA@@@@@@@@@@JINX0t1t
            Engine (
                MaxForce ( 200000 )
                ORTSTractiveForceRampUpRate ( 10000 )
                ORTSTractiveForceRampDownRate ( 20kN/s )
                ORTSTractiveForceRampDownToZeroRate ( 1000lbf/s )
            )
            """));

        var metadata = new LocomotiveTractionMetadataReader().Read(
            store, "TRAINS/TRAINSET/Test/Ramp.eng");

        Assert.Equal(10_000, metadata.TractionForceRampUpNewtonsPerSecond);
        Assert.Equal(20_000, metadata.TractionForceRampDownNewtonsPerSecond);
        Assert.Equal(4_448.2217f, metadata.TractionForceRampDownToZeroNewtonsPerSecond, 2);
    }

    [Fact]
    public void DefaultsRampDownToZeroRateToRampDownRateWhenUnspecified()
    {
        using var store = CreateStore(("TRAINS/TRAINSET/Test/Down.eng", """
            SIMISA@@@@@@@@@@JINX0t1t
            Engine (
                MaxForce ( 200000 )
                ORTSTractiveForceRampDownRate ( 20 kN/s )
            )
            """));

        var metadata = new LocomotiveTractionMetadataReader().Read(
            store, "TRAINS/TRAINSET/Test/Down.eng");

        Assert.Equal(20_000, metadata.TractionForceRampDownNewtonsPerSecond);
        Assert.Equal(20_000, metadata.TractionForceRampDownToZeroNewtonsPerSecond);
    }

    [Fact]
    public void DerivesNativeRampRatesFromRunUpTimeWhenExplicitRatesAreAbsent()
    {
        using var store = CreateStore(("TRAINS/TRAINSET/Test/RunUp.eng", """
            SIMISA@@@@@@@@@@JINX0t1t
            Engine (
                MaxForce ( 200000 )
                RunUpTimeToMaxForce ( 10 )
            )
            """));

        var metadata = new LocomotiveTractionMetadataReader().Read(
            store, "TRAINS/TRAINSET/Test/RunUp.eng");

        Assert.Equal(20_000, metadata.TractionForceRampUpNewtonsPerSecond);
        Assert.Equal(40_000, metadata.TractionForceRampDownNewtonsPerSecond);
        Assert.Equal(200_000, metadata.TractionForceRampDownToZeroNewtonsPerSecond);
    }

    [Fact]
    public void AppliesAsymmetricNativeTractionForceRamping()
    {
        var metadata = new LocomotiveTractionMetadata(
            "Ramp", 200_000, 150_000, 2_000_000, 120, 0, null, "ramp.eng",
            10_000, 20_000, 50_000);

        Assert.Equal(1_000, LocomotiveTractionRamp.Advance(0, 100_000, 0.1, metadata));
        Assert.Equal(95_000, LocomotiveTractionRamp.Advance(100_000, 0, 0.1, metadata));
        Assert.Equal(98_000, LocomotiveTractionRamp.Advance(100_000, 90_000, 0.1, metadata));
    }

    private static LocomotiveTractionMetadata CreateCurveMetadata(float maxPower)
    {
        var zeroThrottle = new Orts.Parsers.Msts.Interpolator(2);
        zeroThrottle[0] = 0;
        zeroThrottle[10] = 0;
        var fullThrottle = new Orts.Parsers.Msts.Interpolator(2);
        fullThrottle[0] = 250_000;
        fullThrottle[10] = 200_000;
        var curves = new Orts.Parsers.Msts.InterpolatorDiesel2D(2);
        curves[0] = zeroThrottle;
        curves[1] = fullThrottle;
        curves.HasNegativeValue();
        return new LocomotiveTractionMetadata(
            "Curve", 200_000, 150_000, maxPower, 120, 0, curves, "curve.eng");
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

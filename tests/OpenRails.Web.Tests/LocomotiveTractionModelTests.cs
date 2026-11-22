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
        curves[0] = negative;
        curves[1] = positive;
        curves.HasNegativeValue();
        var metadata = new LocomotiveTractionMetadata(
            "Legacy", 200, 150, 0, 100, 0, curves, "legacy.eng");

        Assert.Equal(-100, new LocomotiveTractionModel(metadata).CalculateTractiveForce(0, 0));
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

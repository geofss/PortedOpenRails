using OpenRails.Web.Parsing;
using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrainConsistSimulationSetupBuilderTests
{
    [Fact]
    public void BuildsOrderedMotionStateAndBrakeRatingsFromVehicleMetadata()
    {
        var consist = new ConsistMetadata("Freight", new[]
        {
            new ConsistCar("Engine", "Diesel", 1, true, false, false),
            new ConsistCar("Wagon", "Freight", 2, false, false, false),
            new ConsistCar("EOT", "ORTS_EOT", 3, false, true, false)
        }, "freight.con");
        var assets = new[]
        {
            CreateAsset(1, "Engine", 20_000, 15, 50_000, traction: null),
            CreateAsset(2, "Wagon", 30_000, 20, 60_000, traction: null)
        };

        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, assets, routeLengthMeters: 500, startDistanceMeters: 10, initialSpeedMetersPerSecond: 2);

        Assert.Equal(2, setup.InitialState.Cars.Count);
        Assert.Equal(17.5, setup.InitialState.Cars[0].DistanceMeters);
        Assert.Equal(35, setup.InitialState.Cars[1].DistanceMeters);
        Assert.Equal(20_000, setup.InitialState.Cars[0].MassKg);
        Assert.Equal(new[] { 50_000d, 60_000d }, setup.MaximumBrakeForceNewtonsByCar);
        Assert.Equal(2, setup.Session.State.Cars.Count);
        Assert.Equal(2, setup.Session.State.Cars[0].SpeedMetersPerSecond);
    }

    [Fact]
    public void ReportsFallbackMetadataAndRejectsInvalidRouteLength()
    {
        var consist = new ConsistMetadata("Missing", new[]
        {
            new ConsistCar("Wagon", "Freight", 10, false, false, false)
        }, "missing.con");

        var setup = new TrainConsistSimulationSetupBuilder().Build(
            consist, Array.Empty<ConsistCarAssetReference>(), routeLengthMeters: 100);

        Assert.Equal(10_000, setup.InitialState.Cars[0].MassKg);
        Assert.Equal(10_000 * 9.80665 * 0.1, setup.MaximumBrakeForceNewtonsByCar[0], 2);
        Assert.Equal(2, setup.Diagnostics.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TrainConsistSimulationSetupBuilder().Build(consist, Array.Empty<ConsistCarAssetReference>(), 0));
    }

    private static ConsistCarAssetReference CreateAsset(
        int uid, string name, float mass, float length, float brake,
        LocomotiveTractionMetadata? traction) =>
        new("Freight", uid, name, "Freight", uid == 1, false, name + ".eng",
            true, null,
            new RollingStockMetadata(name, "Freight", mass, 3, 4, length, 4, 8,
                null, null, brake, name + ".eng"),
            name + ".s", true, null, traction);
}

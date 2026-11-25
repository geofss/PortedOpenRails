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
    public void UsesTheWeakestAdjacentVehicleCouplerRating()
    {
        var consist = new ConsistMetadata("Freight", new[]
        {
            new ConsistCar("Engine", "Diesel", 1, true, false, false),
            new ConsistCar("Wagon", "Freight", 2, false, false, false)
        }, "coupled.con");
        var assets = new[]
        {
            CreateAsset(1, "Engine", 20_000, 15, 50_000, null, 80_000),
            CreateAsset(2, "Wagon", 30_000, 20, 60_000, null, 55_000)
        };

        var setup = new TrainConsistSimulationSetupBuilder().Build(consist, assets, 500);
        var limit = Assert.Single(setup.CouplerForceLimits);

        Assert.Equal(55_000, limit.MaximumCompressionNewtons);
        Assert.Equal(55_000, limit.MaximumTensionNewtons);
    }

    [Fact]
    public void MapsDavisCoefficientsIntoPerCarResistanceParameters()
    {
        var consist = new ConsistMetadata("Freight", new[]
        {
            new ConsistCar("Wagon", "Freight", 2, false, false, false)
        }, "freight.con");
        var assets = new[]
        {
            CreateAsset(2, "Wagon", 30_000, 20, 60_000, null,
                davisA: 100, davisB: 5, davisC: 0.5f)
        };

        var setup = new TrainConsistSimulationSetupBuilder().Build(consist, assets, 500);
        var resistance = Assert.Single(setup.ResistanceParametersByCar);

        Assert.Equal(100, resistance.DavisA);
        Assert.Equal(5, resistance.DavisB);
        Assert.Equal(0.5, resistance.DavisC);
        Assert.Equal(0.0015, resistance.RollingResistanceCoefficient);
    }

    [Fact]
    public void IgnoresInvalidDavisCoefficientsAndReportsThem()
    {
        var consist = new ConsistMetadata("Freight", new[]
        {
            new ConsistCar("Wagon", "Freight", 2, false, false, false)
        }, "freight.con");
        var assets = new[]
        {
            CreateAsset(2, "Wagon", 30_000, 20, 60_000, null,
                davisA: -1, davisB: float.NaN, davisC: 0.5f)
        };

        var setup = new TrainConsistSimulationSetupBuilder().Build(consist, assets, 500);
        var resistance = Assert.Single(setup.ResistanceParametersByCar);

        Assert.Equal(0, resistance.DavisA);
        Assert.Equal(0, resistance.DavisB);
        Assert.Equal(0.5, resistance.DavisC);
        Assert.Equal(2, setup.Diagnostics.Count);
        Assert.Contains(setup.Diagnostics, message => message.Contains("Davis A"));
        Assert.Contains(setup.Diagnostics, message => message.Contains("Davis B"));
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
        Assert.Equal(4, setup.Diagnostics.Count);
        Assert.Contains(setup.Diagnostics, message => message.Contains("brake-system metadata is missing"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TrainConsistSimulationSetupBuilder().Build(consist, Array.Empty<ConsistCarAssetReference>(), 0));
    }

    [Fact]
    public void CarriesDeclaredAirBrakeMetadataIntoPerCarSetupProfile()
    {
        var consist = new ConsistMetadata("Air", new[]
        {
            new ConsistCar("Engine", "Diesel", 1, true, false, false),
            new ConsistCar("Wagon", "Freight", 2, false, false, false)
        }, "air.con");
        var assets = new[]
        {
            CreateAsset(1, "Engine", 20_000, 15, 50_000, null,
                brakeSystemType: " air_single_pipe ", brakeEquipmentType: "Distributor"),
            CreateAsset(2, "Wagon", 30_000, 20, 60_000, null,
                brakeSystemType: "air_twin_pipe", brakeEquipmentType: "triple_valve")
        };

        var setup = new TrainConsistSimulationSetupBuilder().Build(consist, assets, 500);

        Assert.Equal(2, setup.BrakeProfilesByCar.Count);
        Assert.All(setup.BrakeProfilesByCar,
            profile => Assert.Equal(RollingStockBrakeSupport.PrototypeAirDistributor, profile.Support));
        Assert.Equal(" air_single_pipe ", setup.BrakeProfilesByCar[0].DeclaredSystemType);
    }

    [Theory]
    [InlineData("vacuum_single_pipe", "vacuum_brake")]
    [InlineData("ep_single_pipe", "ep_brake")]
    [InlineData("unknown_system", "unknown_equipment")]
    public void ReportsBrakeTypesThatPrototypeDoesNotModel(string systemType, string equipmentType)
    {
        var consist = new ConsistMetadata("OtherBrake", new[]
        {
            new ConsistCar("Vehicle", "Freight", 1, false, false, false)
        }, "other.con");
        var assets = new[]
        {
            CreateAsset(1, "Vehicle", 20_000, 15, 50_000, null,
                brakeSystemType: systemType, brakeEquipmentType: equipmentType)
        };

        var setup = new TrainConsistSimulationSetupBuilder().Build(consist, assets, 500);

        Assert.Equal(RollingStockBrakeSupport.UnsupportedOrUnverified,
            Assert.Single(setup.BrakeProfilesByCar).Support);
        Assert.Contains(setup.Diagnostics, message => message.Contains("not modelled"));
    }

    [Theory]
    [InlineData(" air_single_pipe ", " distributor ", RollingStockBrakeSupport.PrototypeAirDistributor)]
    [InlineData("air_single_pipe", "(distributor)", RollingStockBrakeSupport.PrototypeAirDistributor)]
    [InlineData("air_single_pipe", " ", RollingStockBrakeSupport.UnsupportedOrUnverified)]
    [InlineData("", "distributor", RollingStockBrakeSupport.UnsupportedOrUnverified)]
    [InlineData("AIR_SINGLE_PIPE", "DISTRIBUTOR", RollingStockBrakeSupport.PrototypeAirDistributor)]
    [InlineData("air_single_pipe", "", RollingStockBrakeSupport.UnsupportedOrUnverified)]
    [InlineData("air_single_pipe", "graduated_release_triple_valve", RollingStockBrakeSupport.UnsupportedOrUnverified)]
    [InlineData("air_single_pipe", "distributing_valve", RollingStockBrakeSupport.UnsupportedOrUnverified)]
    [InlineData("air_twin_pipe", "distributor", RollingStockBrakeSupport.PrototypeAirDistributor)]
    [InlineData("air_single_pipe", "triple_valve", RollingStockBrakeSupport.PrototypeAirDistributor)]
    [InlineData("vacuum_single_pipe", "distributor", RollingStockBrakeSupport.UnsupportedOrUnverified)]
    [InlineData("ep_single_pipe", "distributor", RollingStockBrakeSupport.UnsupportedOrUnverified)]
    [InlineData("manual_braking", "distributor", RollingStockBrakeSupport.UnsupportedOrUnverified)]
    [InlineData("sme", "distributor", RollingStockBrakeSupport.UnsupportedOrUnverified)]
    [InlineData("air_single_pipe", "unknown_valve", RollingStockBrakeSupport.UnsupportedOrUnverified)]
    public void BrakeProfileClassificationRequiresExplicitSupportedEquipment(
        string systemType, string equipmentType, RollingStockBrakeSupport expected)
    {
        var metadata = new RollingStockMetadata("Car", "Freight", 20_000, 3, 4, 15, 4, 8,
            systemType, equipmentType, 50_000, "car.wag");
        Assert.Equal(expected, RollingStockBrakeProfile.FromMetadata(metadata).Support);
    }

    private static ConsistCarAssetReference CreateAsset(
        int uid, string name, float mass, float length, float brake,
        LocomotiveTractionMetadata? traction, float? couplerStrength = null,
        float? davisA = null, float? davisB = null, float? davisC = null,
        string? brakeSystemType = null, string? brakeEquipmentType = null) =>
        new("Freight", uid, name, "Freight", uid == 1, false, name + ".eng",
            true, null,
            new RollingStockMetadata(name, "Freight", mass, 3, 4, length, 4, 8,
                brakeSystemType, brakeEquipmentType, brake, name + ".eng", MinCouplerStrengthN: couplerStrength,
                DavisAForceN: davisA, DavisBResistance: davisB, DavisCResistance: davisC),
            name + ".s", true, null, traction);
}

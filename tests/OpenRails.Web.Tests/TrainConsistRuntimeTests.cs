using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;
using OpenRails.Web.Parsing;
using ORTS.Common;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrainConsistRuntimeTests
{
    [Fact]
    public void ParsedLocomotiveAndCoachesCanMoveAsOneConsist()
    {
        using var store = CreateStore(
            ("TRAINSET/Test/engine.eng", """
                SIMISA@@@@@@@@@@JINX0E0t
                Engine ( TestEngine
                    Name ( "Test Engine" )
                    Mass ( 80000kg )
                    Size ( 3m 4m 18m )
                    MaxPower ( 2000kW )
                    MaxForce ( 200kN )
                    MaxVelocity ( 144km/h )
                )
                """),
            ("TRAINSET/Test/coach-a.wag", """
                SIMISA@@@@@@@@@@JINX0w1t
                Wagon ( CoachA
                    Name ( "Coach A" )
                    Mass ( 40000kg )
                    Size ( 2.8m 3.9m 20m )
                    MaxBrakeForce ( 60kN )
                )
                """),
            ("TRAINSET/Test/coach-b.wag", """
                SIMISA@@@@@@@@@@JINX0w1t
                Wagon ( CoachB
                    Name ( "Coach B" )
                    Mass ( 40000kg )
                    Size ( 2.8m 3.9m 20m )
                    MaxBrakeForce ( 60kN )
                )
                """));

        var reader = new RollingStockMetadataReader();
        var engine = reader.ReadEngine(store, "TRAINSET/Test/engine.eng");
        var wagons = new[]
        {
            reader.ReadWagon(store, "TRAINSET/Test/coach-a.wag"),
            reader.ReadWagon(store, "TRAINSET/Test/coach-b.wag")
        };
        var train = TrainConsistRuntime.Create(engine, wagons, leadPositionMeters: 100);

        var netForce = train.Step(throttleFraction: 1, brakeFraction: 0, direction: 1, elapsedSeconds: 1);
        var positions = train.VehiclePositionsMeters;

        Assert.Equal(200_000, netForce, 0);
        Assert.Equal(1.25, train.SpeedMetersPerSecond, 2);
        Assert.Equal(100.625, positions[0], 3);
        Assert.Equal(81.625, positions[1], 3);
        Assert.Equal(61.625, positions[2], 3);
    }

    [Fact]
    public void CoachBrakesContributeToStoppingTheWholeConsist()
    {
        var engine = new EngineMetadata("Test Engine")
        {
            MassKilograms = 60_000,
            LengthMeters = 18,
            MaximumTractiveForceNewtons = 100_000,
            MaximumSpeedMetersPerSecond = 40,
            MaximumBrakeForceNewtons = 180_000
        };
        var wagons = new[]
        {
            new WagonMetadata("Coach A") { MassKilograms = 30_000, LengthMeters = 20, MaximumBrakeForceNewtons = 110_000 },
            new WagonMetadata("Coach B") { MassKilograms = 30_000, LengthMeters = 20, MaximumBrakeForceNewtons = 110_000 }
        };
        var train = TrainConsistRuntime.Create(engine, wagons, leadPositionMeters: 50, initialSpeedMetersPerSecond: 5);

        train.Step(throttleFraction: 0, brakeFraction: 1, direction: 1, elapsedSeconds: 2);

        Assert.Equal(0, train.SpeedMetersPerSecond, 6);
        var positions = train.VehiclePositionsMeters;
        Assert.Equal(53.75, positions[0], 5);
        Assert.Equal(34.75, positions[1], 5);
        Assert.Equal(14.75, positions[2], 5);
    }

    [Fact]
    public void RejectsVehicleDefinitionsMissingPhysicalDimensions()
    {
        var engine = new EngineMetadata("Incomplete Engine")
        {
            MassKilograms = 80_000,
            MaximumSpeedMetersPerSecond = 30
        };

        Assert.Throws<ArgumentException>(() => TrainConsistRuntime.Create(engine, Array.Empty<WagonMetadata>()));
    }
}

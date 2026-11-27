using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrainConsistMotionIntegratorTests
{
    [Fact]
    public void IntegratesEachCarWithItsOwnMassAndSignedCouplerForce()
    {
        var state = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(10_000, 0, 0),
            new TrainCarMotionState(20_000, 0, 0)
        });
        var forces = new[]
        {
            new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0),
            new TrainCarForceBreakdown(0, 0, 0, 0, 0, 0, 0)
        };

        var result = new TrainConsistMotionIntegrator().Step(
            state, forces, new[] { -5_000d, 5_000d }, 1);

        Assert.Equal(-0.5, result.Cars[0].SpeedMetersPerSecond, 8);
        Assert.Equal(0.25, result.Cars[1].SpeedMetersPerSecond, 8);
    }

    [Fact]
    public void PreservesCarOrderingAndAppliesPerCarResistance()
    {
        var state = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(10_000, 2, 10),
            new TrainCarMotionState(10_000, 2, 20)
        });
        var forces = new[]
        {
            new TrainCarForceBreakdown(0, 0, 1_000, 0, 0, 0, 0),
            new TrainCarForceBreakdown(0, 0, 2_000, 0, 0, 0, 0)
        };

        var result = new TrainConsistMotionIntegrator().Step(
            state, forces, new[] { 0d, 0d }, 1);

        Assert.Equal(1.9, result.Cars[0].SpeedMetersPerSecond, 8);
        Assert.Equal(1.8, result.Cars[1].SpeedMetersPerSecond, 8);
        Assert.Equal(11.95, result.Cars[0].DistanceMeters, 8);
        Assert.Equal(21.9, result.Cars[1].DistanceMeters, 8);
    }


    [Fact]
    public void MotionSnapshotCopiesCallerOwnedReadOnlyList()
    {
        var source = new List<TrainCarMotionState>
        {
            new(10_000, 1, 0),
            new(20_000, 2, 10)
        };
        IReadOnlyList<TrainCarMotionState> readOnlyView = source;

        var snapshot = new TrainConsistMotionState(readOnlyView);
        source[0] = new TrainCarMotionState(99_000, 9, 99);
        source.Add(new TrainCarMotionState(30_000, 3, 20));

        Assert.Equal(2, snapshot.Cars.Count);
        Assert.Equal(10_000, snapshot.Cars[0].MassKilograms);
        Assert.Equal(0, snapshot.Cars[0].DistanceMeters);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<TrainCarMotionState>)snapshot.Cars)[0] = new TrainCarMotionState(1, 0, 0));
    }

    [Fact]
    public void MotionSnapshotRejectsEmptyAndNullCarCollections()
    {
        Assert.Throws<ArgumentException>(() =>
            new TrainConsistMotionState(Array.Empty<TrainCarMotionState>()));
        Assert.Throws<ArgumentException>(() =>
            new TrainConsistMotionState(new TrainCarMotionState[] { null! }));
    }

    [Fact]
    public void RejectsMismatchedPerCarForceArrays()
    {
        var state = new TrainConsistMotionState(new[]
        {
            new TrainCarMotionState(10_000, 0, 0)
        });

        Assert.Throws<ArgumentException>(() =>
            new TrainConsistMotionIntegrator().Step(
                state, Array.Empty<TrainCarForceBreakdown>(), Array.Empty<double>(), 0.1));
    }
}

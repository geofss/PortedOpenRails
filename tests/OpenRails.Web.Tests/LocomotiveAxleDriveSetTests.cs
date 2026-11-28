using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class LocomotiveAxleDriveSetTests
{
    [Fact]
    public void MaterializesAxleConfigurationAndSpeedInputsOnlyOnce()
    {
        var fractions = new SinglePassReadOnlyList<float>(new[] { 0.5f, 0.5f });
        var drive = new LocomotiveAxleDriveSet(fractions);

        drive.SetAxleSpeeds(new SinglePassReadOnlyList<double>(new[] { 10d, 12d }));

        Assert.Equal(2, drive.AxleCount);
    }

    private sealed class SinglePassReadOnlyList<T>(IReadOnlyList<T> values) : IReadOnlyList<T>
    {
        private bool enumerated;
        public int Count => values.Count;
        public T this[int index] => values[index];
        public IEnumerator<T> GetEnumerator()
        {
            if (enumerated) throw new InvalidOperationException("Collection can only be enumerated once.");
            enumerated = true;
            return values.GetEnumerator();
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void DistributesTotalTargetByDrivenAxleFractions()
    {
        var drive = new LocomotiveAxleDriveSet(new[] { 0.25f, 0.25f, 0.25f, 0.25f });
        var total = drive.Advance(1, 100_000, 100_000, forceRampUpNewtonsPerSecond: 40_000);
        Assert.Equal(40_000, total);
        Assert.All(drive.ForceByAxle, force => Assert.Equal(10_000, force));
    }

    [Fact]
    public void TracksPreviousAndCurrentWheelSpeedsForPowerRamp()
    {
        var drive = new LocomotiveAxleDriveSet(new[] { 0.5f, 0.5f });
        drive.SetAxleSpeeds(new[] { 10d, 10d });
        drive.Advance(1, 20_000, 20_000);
        drive.SetAxleSpeeds(new[] { 20d, 20d });
        var result = drive.Advance(1, 20_000, 20_000, powerRampUpWattsPerSecond: 100_000);
        Assert.True(result > 0);
        Assert.True(result < 20_000);
    }

    [Fact]
    public void IndividualSlipControlTargetsOverrideFractionalAllocation()
    {
        var drive = new LocomotiveAxleDriveSet(new[] { 0.5f, 0.5f });
        var result = drive.Advance(1, 100_000, 100_000,
            forceRampUpNewtonsPerSecond: 100_000,
            individualAxleTargetForceNewtons: new[] { 10_000f, 30_000f });
        Assert.Equal(40_000, result);
        Assert.Equal(new[] { 10_000f, 30_000f }, drive.ForceByAxle);
    }

    [Fact]
    public void FailedMultiAxleAdvanceDoesNotPartiallyChangeEarlierAxles()
    {
        var drive = new LocomotiveAxleDriveSet(new[] { 0.5f, 0.5f });
        drive.SetAxleSpeeds(new[] { 0d, double.MaxValue });
        var before = drive.ForceByAxle.ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() => drive.Advance(
            1, 10_000, 20_000, powerRampUpWattsPerSecond: 10_000));

        Assert.Equal(before, drive.ForceByAxle);
    }

    [Fact]
    public void CapturesAndRestoresForceAndWheelSpeedHistory()
    {
        var drive = new LocomotiveAxleDriveSet(new[] { 0.5f, 0.5f });
        drive.SetAxleSpeeds(new[] { 8d, 9d });
        drive.Advance(1, 20_000, 20_000, forceRampUpNewtonsPerSecond: 20_000);
        drive.SetAxleSpeeds(new[] { 10d, 11d });
        var snapshot = drive.CaptureState();

        var restored = new LocomotiveAxleDriveSet(new[] { 0.5f, 0.5f });
        restored.RestoreState(snapshot);

        Assert.Equal(drive.ForceByAxle, restored.ForceByAxle);
        Assert.Equal(drive.TotalDriveForceNewtons, restored.TotalDriveForceNewtons);
        var expected = drive.Advance(1, 20_000, 20_000, powerRampUpWattsPerSecond: 50_000);
        var actual = restored.Advance(1, 20_000, 20_000, powerRampUpWattsPerSecond: 50_000);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CapturedSnapshotCollectionsAreReadOnly()
    {
        var drive = new LocomotiveAxleDriveSet(new[] { 0.5f, 0.5f });
        drive.Advance(1, 10_000, 10_000, forceRampUpNewtonsPerSecond: 10_000);
        var snapshot = drive.CaptureState();

        Assert.IsNotType<float[]>(snapshot.TractiveForceFractions);
        Assert.IsNotType<float[]>(snapshot.ForceByAxle);
        Assert.IsNotType<double[]>(snapshot.PreviousSpeedByAxle);
        Assert.IsNotType<double[]>(snapshot.SpeedByAxle);
        Assert.Throws<NotSupportedException>(() => ((IList<float>)snapshot.ForceByAxle)[0] = 99);
    }

    [Fact]
    public void RejectsSnapshotWithNullCollections()
    {
        var drive = new LocomotiveAxleDriveSet(new[] { 1f });
        var snapshot = new LocomotiveAxleDriveSnapshot(null!, new[] { 0f }, new[] { 0d }, new[] { 0d });
        Assert.Throws<ArgumentException>(() => drive.RestoreState(snapshot));
    }

    [Fact]
    public void RejectsNonFiniteSnapshotValuesWithoutMutatingLiveState()
    {
        var drive = new LocomotiveAxleDriveSet(new[] { 1f });
        drive.Advance(1, 15_000, 15_000, forceRampUpNewtonsPerSecond: 15_000);
        var before = drive.CaptureState();
        var invalid = new LocomotiveAxleDriveSnapshot(new[] { 1f }, new[] { float.NaN }, new[] { 0d }, new[] { 0d });

        Assert.Throws<ArgumentException>(() => drive.RestoreState(invalid));
        Assert.Equal(before.ForceByAxle, drive.ForceByAxle);
    }

    [Fact]
    public void RejectsSnapshotFromDifferentAxleTopology()
    {
        var source = new LocomotiveAxleDriveSet(new[] { 0.5f, 0.5f });
        var target = new LocomotiveAxleDriveSet(new[] { 0.25f, 0.25f, 0.5f });
        Assert.Throws<ArgumentException>(() => target.RestoreState(source.CaptureState()));
    }

    [Fact]
    public void RejectsSnapshotWithDifferentForceAllocation()
    {
        var source = new LocomotiveAxleDriveSet(new[] { 0.25f, 0.75f });
        var target = new LocomotiveAxleDriveSet(new[] { 0.5f, 0.5f });
        Assert.Throws<ArgumentException>(() => target.RestoreState(source.CaptureState()));
    }

    [Fact]
    public void RequiresForceFractionsToSumToOne()
    {
        Assert.Throws<ArgumentException>(() =>
            new LocomotiveAxleDriveSet(new[] { 0.4f, 0.4f }));
    }

    [Fact]
    public void ResetClearsForceAndWheelSpeedHistory()
    {
        var drive = new LocomotiveAxleDriveSet(new[] { 1f });
        drive.SetAxleSpeeds(new[] { 12d });
        drive.Advance(1, 10_000, 10_000, forceRampUpNewtonsPerSecond: 10_000);
        drive.Reset();
        Assert.Equal(0, drive.TotalDriveForceNewtons);
        Assert.Equal(0, drive.ForceByAxle[0]);
    }
}

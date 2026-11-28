using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackShapeLodSelectorTests
{
    private static TrackShapeLodMesh Level(float distance) =>
        new(distance, Array.Empty<int>(), Array.Empty<TrackShapeSubObjectMesh>());

    [Fact]
    public void UsesBoundingSphereAndNativeDistanceThresholds()
    {
        var levels = new[] { Level(1000), Level(100), Level(400) };

        Assert.Equal(100, TrackShapeLodSelector.Select(levels, 150, viewSphereRadius: 100)!.ViewingDistance);
        Assert.Equal(400, TrackShapeLodSelector.Select(levels, 350, viewSphereRadius: 100)!.ViewingDistance);
        Assert.Equal(1000, TrackShapeLodSelector.Select(levels, 800, viewSphereRadius: 100)!.ViewingDistance);
    }

    [Fact]
    public void AppliesLodBiasAndRejectsInvalidInputs()
    {
        var levels = new[] { Level(100), Level(400) };

        Assert.Equal(400, TrackShapeLodSelector.Select(levels, 250, lodBias: 0.5f)!.ViewingDistance);
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackShapeLodSelector.Select(levels, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackShapeLodSelector.Select(levels, 10, viewSphereRadius: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackShapeLodSelector.Select(levels, 10, lodBias: 0));
    }

    [Fact]
    public void SelectsEachNativeLodControlIndependently()
    {
        var levels = new[]
        {
            new TrackShapeLodMesh(100, Array.Empty<int>(), Array.Empty<TrackShapeSubObjectMesh>(), 0),
            new TrackShapeLodMesh(400, Array.Empty<int>(), Array.Empty<TrackShapeSubObjectMesh>(), 0),
            new TrackShapeLodMesh(50, Array.Empty<int>(), Array.Empty<TrackShapeSubObjectMesh>(), 1),
            new TrackShapeLodMesh(200, Array.Empty<int>(), Array.Empty<TrackShapeSubObjectMesh>(), 1)
        };

        var selected = TrackShapeLodSelector.SelectAll(levels, 150);

        Assert.Equal(new[] { 400f, 200f }, selected.Select(level => level.ViewingDistance));
        Assert.Equal(new[] { 0, 1 }, selected.Select(level => level.ControlIndex));
    }

    [Fact]
    public void EmptyLevelsReturnNull()
    {
        Assert.Null(TrackShapeLodSelector.Select(Array.Empty<TrackShapeLodMesh>(), 10));
        Assert.Empty(TrackShapeLodSelector.SelectAll(Array.Empty<TrackShapeLodMesh>(), 10));
    }
}

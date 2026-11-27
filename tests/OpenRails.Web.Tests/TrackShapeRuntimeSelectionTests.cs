using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackShapeRuntimeSelectionTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.5, 15)]
    [InlineData(1, 0)]
    [InlineData(1.5, 15)]
    public void AnimationClockConvertsElapsedTimeAndWraps(double seconds, float expected)
    {
        var clip = new TrackShapeAnimationClip(30, 30, Array.Empty<TrackShapeAnimationNode>());
        Assert.Equal(expected, TrackShapeAnimationClock.GetFrame(clip, seconds), 3);
    }

    [Fact]
    public void AnimationClockClampsWhenNotLooping()
    {
        var clip = new TrackShapeAnimationClip(30, 30, Array.Empty<TrackShapeAnimationNode>());
        Assert.Equal(29, TrackShapeAnimationClock.GetFrame(clip, 10, loop: false));
    }

    [Fact]
    public void LodSelectorChoosesNearestThresholdAndSortsUnorderedLevels()
    {
        var near = new TrackShapeLodMesh(100, Array.Empty<int>(), Array.Empty<TrackShapeSubObjectMesh>());
        var far = new TrackShapeLodMesh(1000, Array.Empty<int>(), Array.Empty<TrackShapeSubObjectMesh>());
        var middle = new TrackShapeLodMesh(400, Array.Empty<int>(), Array.Empty<TrackShapeSubObjectMesh>());
        var levels = new[] { far, near, middle };

        Assert.Same(near, TrackShapeLodSelector.Select(levels, 50));
        Assert.Same(middle, TrackShapeLodSelector.Select(levels, 250));
        Assert.Same(far, TrackShapeLodSelector.Select(levels, 1500));
    }

    [Fact]
    public void LodSelectorRejectsInvalidDistanceAndHandlesNoLevels()
    {
        Assert.Null(TrackShapeLodSelector.Select(Array.Empty<TrackShapeLodMesh>(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrackShapeLodSelector.Select(Array.Empty<TrackShapeLodMesh>(), float.NaN));
    }
}

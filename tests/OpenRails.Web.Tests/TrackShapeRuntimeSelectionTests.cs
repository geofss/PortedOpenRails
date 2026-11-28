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
    public void MeshVertexRetainsEveryUvChannel()
    {
        var vertex = new TrackShapeMeshVertex(
            System.Numerics.Vector3.Zero,
            System.Numerics.Vector3.UnitY,
            new System.Numerics.Vector2(0.1f, 0.2f),
            0, uint.MaxValue, 0,
            new[]
            {
                new System.Numerics.Vector2(0.3f, 0.4f),
                new System.Numerics.Vector2(0.5f, 0.6f)
            });

        Assert.Equal(new System.Numerics.Vector2(0.1f, 0.2f), vertex.GetTextureCoordinate(0));
        Assert.Equal(new System.Numerics.Vector2(0.3f, 0.4f), vertex.GetTextureCoordinate(1));
        Assert.Equal(new System.Numerics.Vector2(0.5f, 0.6f), vertex.GetTextureCoordinate(2));
        Assert.Equal(System.Numerics.Vector2.Zero, vertex.GetTextureCoordinate(3));
    }

    [Fact]
    public void LodSelectorRejectsInvalidDistanceAndHandlesNoLevels()
    {
        Assert.Null(TrackShapeLodSelector.Select(Array.Empty<TrackShapeLodMesh>(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrackShapeLodSelector.Select(Array.Empty<TrackShapeLodMesh>(), float.NaN));
    }
}

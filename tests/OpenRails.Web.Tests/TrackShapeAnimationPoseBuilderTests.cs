using System.Numerics;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackShapeAnimationPoseBuilderTests
{
    [Fact]
    public void MapsNamedAnimationNodeToShapeMatrixAndSamplesItsTransform()
    {
        var matrices = new[]
        {
            new TrackShapeMatrix("wheel", 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0),
            new TrackShapeMatrix("body", 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0)
        };
        var controller = new TrackShapeAnimationController(
            TrackShapeAnimationControllerKind.LinearPosition,
            "linear_pos",
            new[]
            {
                new TrackShapeAnimationKey(0, Vector3.Zero, null, null, null, null, null, null),
                new TrackShapeAnimationKey(10, new Vector3(0, 2, 0), null, null, null, null, null, null)
            });
        var clips = new[]
        {
            new TrackShapeAnimationClip(10, 30, new[]
            {
                new TrackShapeAnimationNode("wheel", new[] { controller }),
                new TrackShapeAnimationNode("unmatched-helper", new[] { controller })
            })
        };

        var pose = TrackShapeAnimationPoseBuilder.Build(matrices, clips, 0, 5);

        Assert.Single(pose);
        Assert.True(pose.ContainsKey(0));
        var translation = Vector3.Transform(Vector3.Zero, pose[0]);
        Assert.Equal(new Vector3(0, 1, 0), translation);
    }

    [Fact]
    public void ReturnsEmptyPoseForMissingClipAndRejectsNonFiniteFrame()
    {
        var matrices = new[] { new TrackShapeMatrix("body", 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0) };
        Assert.Empty(TrackShapeAnimationPoseBuilder.Build(matrices, Array.Empty<TrackShapeAnimationClip>(), 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrackShapeAnimationPoseBuilder.Build(matrices, Array.Empty<TrackShapeAnimationClip>(), 0, float.NaN));
    }
}

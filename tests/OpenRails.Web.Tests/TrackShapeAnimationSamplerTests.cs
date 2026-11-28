using System.Numerics;
using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackShapeAnimationSamplerTests
{
    [Fact]
    public void SamplesLinearPositionBetweenKeyframes()
    {
        var controller = new TrackShapeAnimationController(
            TrackShapeAnimationControllerKind.LinearPosition,
            "linear_pos",
            new[]
            {
                new TrackShapeAnimationKey(0, Vector3.Zero, null, null, null, null, null, null),
                new TrackShapeAnimationKey(10, new Vector3(10, 4, -2), null, null, null, null, null, null)
            });

        Assert.Equal(new Vector3(5, 2, -1), TrackShapeAnimationSampler.SamplePosition(controller, 5));
        Assert.Equal(Vector3.Zero, TrackShapeAnimationSampler.SamplePosition(controller, -1));
        Assert.Equal(new Vector3(10, 4, -2), TrackShapeAnimationSampler.SamplePosition(controller, 11));
    }

    [Fact]
    public void SamplesRotationUsingQuaternionSlerp()
    {
        var start = Quaternion.Identity;
        var finish = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        var controller = new TrackShapeAnimationController(
            TrackShapeAnimationControllerKind.Rotation,
            "tcb_rot",
            new[]
            {
                new TrackShapeAnimationKey(0, null, start, null, null, null, null, null),
                new TrackShapeAnimationKey(10, null, finish, null, null, null, null, null)
            });

        var sampled = TrackShapeAnimationSampler.SampleRotation(controller, 5);
        var expected = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4);
        Assert.True(MathF.Abs(Quaternion.Dot(sampled, expected)) > 0.999f);
    }

    [Fact]
    public void TcbRotationMatchesUpstreamAdjacentKeySlerp()
    {
        var controller = new TrackShapeAnimationController(
            TrackShapeAnimationControllerKind.Rotation,
            "tcb_rot",
            new[]
            {
                new TrackShapeAnimationKey(0, null, Quaternion.Identity, 0, 0, 0, 0, 0),
                new TrackShapeAnimationKey(10, null, Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.4f), 0.7f, 0.3f, -0.2f, 0, 0),
                new TrackShapeAnimationKey(20, null, Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.6f), 0, 0, 0, 0, 0)
            });

        var sampled = TrackShapeAnimationSampler.SampleRotation(controller, 5);
        var linear = Quaternion.Slerp(Quaternion.Identity,
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.4f), 0.5f);

        Assert.True(float.IsFinite(sampled.LengthSquared()));
        Assert.InRange(sampled.LengthSquared(), 0.999f, 1.001f);
        Assert.InRange(MathF.Abs(Quaternion.Dot(sampled, linear)), 0.99999f, 1.00001f);
    }

    [Fact]
    public void RejectsSamplingWithWrongControllerKind()
    {
        var controller = new TrackShapeAnimationController(
            TrackShapeAnimationControllerKind.Rotation, "tcb_rot", Array.Empty<TrackShapeAnimationKey>());
        Assert.Throws<ArgumentException>(() => TrackShapeAnimationSampler.SamplePosition(controller, 0));
    }
}

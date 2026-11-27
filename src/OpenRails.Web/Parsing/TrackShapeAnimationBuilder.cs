using System.Numerics;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Converts native shape animation controllers into portable clip/keyframe data.
/// Sampling is available to the browser runtime; this does not yet apply sampled
/// transforms to rendered matrix hierarchies.
/// </summary>
public static class TrackShapeAnimationBuilder
{
    public static IReadOnlyList<TrackShapeAnimationClip> Build(shape source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.animations is null || source.animations.Count == 0)
            return Array.Empty<TrackShapeAnimationClip>();

        return Array.AsReadOnly(source.animations.Select(animation =>
        {
            var nodes = animation.anim_nodes.Select(node => new TrackShapeAnimationNode(
                node.Name,
                Array.AsReadOnly(node.controllers.Select(BuildController).ToArray()))).ToArray();
            return new TrackShapeAnimationClip(animation.FrameCount, animation.FrameRate,
                Array.AsReadOnly(nodes));
        }).ToArray());
    }

    private static TrackShapeAnimationController BuildController(controller source)
    {
        var kind = source switch
        {
            linear_pos => TrackShapeAnimationControllerKind.LinearPosition,
            tcb_rot => TrackShapeAnimationControllerKind.Rotation,
            _ => TrackShapeAnimationControllerKind.Unsupported
        };

        var keys = source.Select(key => key switch
        {
            linear_key position => new TrackShapeAnimationKey(position.Frame,
                new Vector3(position.X, position.Y, position.Z), null, null, null, null, null),
            slerp_rot rotation => new TrackShapeAnimationKey(rotation.Frame, null,
                Normalize(new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W)), null, null, null, null),
            tcb_key rotation => new TrackShapeAnimationKey(rotation.Frame, null,
                Normalize(new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W)),
                rotation.Tension, rotation.Continuity, rotation.Bias, rotation.In, rotation.Out),
            _ => null
        }).Where(key => key is not null).Cast<TrackShapeAnimationKey>()
          .OrderBy(key => key.Frame).ToArray();

        return new TrackShapeAnimationController(kind, source.GetType().Name,
            Array.AsReadOnly(keys));
    }

    private static Quaternion Normalize(Quaternion value)
    {
        var lengthSquared = value.LengthSquared();
        return lengthSquared > 1e-12f && float.IsFinite(lengthSquared)
            ? Quaternion.Normalize(value)
            : Quaternion.Identity;
    }
}

public enum TrackShapeAnimationControllerKind
{
    Unsupported,
    LinearPosition,
    Rotation
}

public sealed record TrackShapeAnimationClip(
    int FrameCount,
    int FrameRate,
    IReadOnlyList<TrackShapeAnimationNode> Nodes);

public sealed record TrackShapeAnimationNode(
    string Name,
    IReadOnlyList<TrackShapeAnimationController> Controllers);

public sealed record TrackShapeAnimationController(
    TrackShapeAnimationControllerKind Kind,
    string NativeControllerType,
    IReadOnlyList<TrackShapeAnimationKey> Keys);

public sealed record TrackShapeAnimationKey(
    int Frame,
    Vector3? Position,
    Quaternion? Rotation,
    float? Tension,
    float? Continuity,
    float? Bias,
    float? In,
    float? Out);

/// <summary>Samples position/rotation controllers between native keyframes.</summary>
public static class TrackShapeAnimationSampler
{
    public static Vector3 SamplePosition(TrackShapeAnimationController controller, float frame)
    {
        ArgumentNullException.ThrowIfNull(controller);
        if (controller.Kind != TrackShapeAnimationControllerKind.LinearPosition)
            throw new ArgumentException("Controller is not a native linear-position controller.", nameof(controller));
        var keys = controller.Keys.Where(key => key.Position.HasValue).ToArray();
        if (keys.Length == 0) return Vector3.Zero;
        var (left, right, amount) = FindInterval(keys, frame);
        if (left == right) return left.Position!.Value;
        return Vector3.Lerp(left.Position!.Value, right.Position!.Value, amount);
    }

    public static Quaternion SampleRotation(TrackShapeAnimationController controller, float frame)
    {
        ArgumentNullException.ThrowIfNull(controller);
        if (controller.Kind != TrackShapeAnimationControllerKind.Rotation)
            throw new ArgumentException("Controller is not a native rotation controller.", nameof(controller));
        var keys = controller.Keys.Where(key => key.Rotation.HasValue).ToArray();
        if (keys.Length == 0) return Quaternion.Identity;
        var (left, right, amount) = FindInterval(keys, frame);
        if (left == right) return left.Rotation!.Value;
        return Quaternion.Normalize(Quaternion.Slerp(left.Rotation!.Value, right.Rotation!.Value, amount));
    }

    private static (TrackShapeAnimationKey Left, TrackShapeAnimationKey Right, float Amount)
        FindInterval(TrackShapeAnimationKey[] keys, float frame)
    {
        if (keys.Length == 1 || frame <= keys[0].Frame) return (keys[0], keys[0], 0);
        if (frame >= keys[^1].Frame) return (keys[^1], keys[^1], 0);
        for (var i = 1; i < keys.Length; i++)
        {
            if (frame > keys[i].Frame) continue;
            var left = keys[i - 1];
            var right = keys[i];
            var span = right.Frame - left.Frame;
            var amount = span <= 0 ? 0 : Math.Clamp((frame - left.Frame) / span, 0, 1);
            return (left, right, amount);
        }
        return (keys[^1], keys[^1], 0);
    }
}

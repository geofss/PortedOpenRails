using System.Numerics;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Converts native shape animation controllers into portable clip/keyframe data.
/// The sampler supports linear position and TCB quaternion rotation; pose evaluation
/// applies sampled transforms to track-shape and WORLD render hierarchies.
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
                new Vector3(position.X, position.Y, position.Z), null, null, null, null, null, null),
            slerp_rot rotation => new TrackShapeAnimationKey(rotation.Frame, null,
                Normalize(new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W)), null, null, null, null, null),
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

/// <summary>Samples native linear-position and rotation controllers between keyframes, including TCB quaternion interpolation.</summary>
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
        if (keys.Any(key => key.Tension.HasValue || key.Continuity.HasValue || key.Bias.HasValue))
            return SampleTcbRotation(keys, Array.IndexOf(keys, left), amount);
        return Quaternion.Normalize(Quaternion.Slerp(left.Rotation!.Value, right.Rotation!.Value, amount));
    }

    private static Quaternion SampleTcbRotation(TrackShapeAnimationKey[] keys, int leftIndex, float amount)
    {
        var rightIndex = Math.Min(leftIndex + 1, keys.Length - 1);
        var start = NormalizeRotation(keys[leftIndex].Rotation!.Value);
        var end = NormalizeRotation(keys[rightIndex].Rotation!.Value);
        if (Quaternion.Dot(start, end) < 0)
            end = Negate(end);

        var startControl = BuildTcbControl(keys, leftIndex, outgoing: true);
        var endControl = BuildTcbControl(keys, rightIndex, outgoing: false);
        if (Quaternion.Dot(startControl, start) < 0) startControl = Negate(startControl);
        if (Quaternion.Dot(endControl, end) < 0) endControl = Negate(endControl);

        var direct = Quaternion.Normalize(Quaternion.Slerp(start, end, amount));
        var tangent = Quaternion.Normalize(Quaternion.Slerp(startControl, endControl, amount));
        return Quaternion.Normalize(Quaternion.Slerp(direct, tangent, 2 * amount * (1 - amount)));
    }

    private static Quaternion BuildTcbControl(TrackShapeAnimationKey[] keys, int index, bool outgoing)
    {
        var current = NormalizeRotation(keys[index].Rotation!.Value);
        if (index == 0 || index == keys.Length - 1)
            return current;

        var previous = NormalizeRotation(keys[index - 1].Rotation!.Value);
        var next = NormalizeRotation(keys[index + 1].Rotation!.Value);
        if (Quaternion.Dot(current, previous) < 0) previous = Negate(previous);
        if (Quaternion.Dot(current, next) < 0) next = Negate(next);

        var previousLog = QuaternionLog(Quaternion.Normalize(Quaternion.Multiply(Quaternion.Conjugate(current), previous)));
        var nextLog = QuaternionLog(Quaternion.Normalize(Quaternion.Multiply(Quaternion.Conjugate(current), next)));
        var previousSpan = Math.Max(1, keys[index].Frame - keys[index - 1].Frame);
        var nextSpan = Math.Max(1, keys[index + 1].Frame - keys[index].Frame);
        var segmentSpan = outgoing ? nextSpan : previousSpan;

        var tension = keys[index].Tension ?? 0;
        var continuity = keys[index].Continuity ?? 0;
        var bias = keys[index].Bias ?? 0;
        var previousWeight = outgoing
            ? (1 - tension) * (1 + continuity) * (1 + bias)
            : (1 - tension) * (1 - continuity) * (1 + bias);
        var nextWeight = outgoing
            ? (1 - tension) * (1 - continuity) * (1 - bias)
            : (1 - tension) * (1 + continuity) * (1 - bias);

        var tangent = -0.25f * segmentSpan *
            (previousLog * (previousWeight / previousSpan) +
             nextLog * (nextWeight / nextSpan));
        var control = Quaternion.Normalize(Quaternion.Multiply(current, QuaternionExp(tangent)));
        return control;
    }

    private static Vector3 QuaternionLog(Quaternion value)
    {
        value = NormalizeRotation(value);
        var vector = new Vector3(value.X, value.Y, value.Z);
        var length = vector.Length();
        if (length < 1e-7f) return Vector3.Zero;
        var angle = MathF.Atan2(length, Math.Clamp(value.W, -1f, 1f));
        return vector * (angle / length);
    }

    private static Quaternion QuaternionExp(Vector3 value)
    {
        var angle = value.Length();
        if (angle < 1e-7f)
            return Quaternion.Normalize(new Quaternion(value, 1));
        var scale = MathF.Sin(angle) / angle;
        return Quaternion.Normalize(new Quaternion(value * scale, MathF.Cos(angle)));
    }

    private static Quaternion NormalizeRotation(Quaternion value)
    {
        var length = value.LengthSquared();
        return length > 1e-12f && float.IsFinite(length)
            ? Quaternion.Normalize(value)
            : Quaternion.Identity;
    }

    private static Quaternion Negate(Quaternion value) =>
        new(-value.X, -value.Y, -value.Z, -value.W);

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

using System.Numerics;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Evaluates a native shape animation clip into per-matrix local transforms.
/// Unknown animation nodes are ignored because some native clips target helper
/// nodes that are not present in a selected LOD hierarchy.
/// </summary>
public static class TrackShapeAnimationPoseBuilder
{
    public static IReadOnlyDictionary<int, Matrix4x4> Build(
        IReadOnlyList<TrackShapeMatrix> matrices,
        IReadOnlyList<TrackShapeAnimationClip>? clips,
        int clipIndex,
        float frame)
    {
        ArgumentNullException.ThrowIfNull(matrices);
        if (!float.IsFinite(frame))
            throw new ArgumentOutOfRangeException(nameof(frame));
        if (clips is null || clips.Count == 0 || clipIndex < 0 || clipIndex >= clips.Count)
            return new Dictionary<int, Matrix4x4>();

        var matrixIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < matrices.Count; i++)
        {
            var name = matrices[i].Name;
            if (!string.IsNullOrWhiteSpace(name))
                matrixIndexes.TryAdd(name, i);
        }

        var pose = new Dictionary<int, Matrix4x4>();
        foreach (var node in clips[clipIndex].Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Name) ||
                !matrixIndexes.TryGetValue(node.Name, out var matrixIndex))
                continue;

            var position = Vector3.Zero;
            var rotation = Quaternion.Identity;
            var hasPosition = false;
            var hasRotation = false;

            foreach (var controller in node.Controllers)
            {
                switch (controller.Kind)
                {
                    case TrackShapeAnimationControllerKind.LinearPosition:
                        position = TrackShapeAnimationSampler.SamplePosition(controller, frame);
                        hasPosition = true;
                        break;
                    case TrackShapeAnimationControllerKind.Rotation:
                        rotation = TrackShapeAnimationSampler.SampleRotation(controller, frame);
                        hasRotation = true;
                        break;
                }
            }

            if (hasPosition || hasRotation)
            {
                var transform = Matrix4x4.Identity;
                if (hasRotation)
                    transform *= Matrix4x4.CreateFromQuaternion(rotation);
                if (hasPosition)
                    transform *= Matrix4x4.CreateTranslation(position);
                pose[matrixIndex] = transform;
            }
        }

        return new System.Collections.ObjectModel.ReadOnlyDictionary<int, Matrix4x4>(pose);
    }
}

namespace OpenRails.Web.Parsing;

/// <summary>
/// Converts elapsed simulation time to the native frame position of an animation clip.
/// The caller owns the simulation clock; this class keeps animation playback deterministic.
/// </summary>
public static class TrackShapeAnimationClock
{
    public static float GetFrame(TrackShapeAnimationClip clip, double elapsedSeconds, bool loop = true)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (!double.IsFinite(elapsedSeconds))
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (clip.FrameRate <= 0)
            throw new InvalidDataException("Animation frame rate must be positive.");
        if (clip.FrameCount <= 0)
            throw new InvalidDataException("Animation frame count must be positive.");

        var frame = Math.Max(0, elapsedSeconds) * clip.FrameRate;
        if (loop)
            return (float)(frame % clip.FrameCount);
        return (float)Math.Min(frame, clip.FrameCount - 1);
    }
}

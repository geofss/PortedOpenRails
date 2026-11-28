namespace OpenRails.Web.Simulation;

/// <summary>Leading-vehicle state for another AI or player train on the same route chainage.</summary>
public sealed record AiServiceTrainState(
    string TrainId,
    double DistanceMeters,
    double SpeedMetersPerSecond);

/// <summary>
/// Resolves the nearest train ahead in the selected service direction. This
/// converts the shared multi-train route state into the following-distance
/// governor input without relying on train enumeration order.
/// </summary>
public static class AiTrainFollowingResolver
{
    public static AiTrainFollowingConstraint? Resolve(
        string trainId,
        double currentDistanceMeters,
        int direction,
        IEnumerable<AiServiceTrainState> otherTrains)
    {
        if (string.IsNullOrWhiteSpace(trainId))
            throw new ArgumentException("Train identity is required.", nameof(trainId));
        if (!double.IsFinite(currentDistanceMeters))
            throw new ArgumentOutOfRangeException(nameof(currentDistanceMeters));
        if (direction is not (-1 or 1))
            throw new ArgumentOutOfRangeException(nameof(direction));
        ArgumentNullException.ThrowIfNull(otherTrains);

        var candidates = otherTrains.ToArray();
        if (candidates.Any(train => train is null || string.IsNullOrWhiteSpace(train.TrainId) ||
            !double.IsFinite(train.DistanceMeters) ||
            !double.IsFinite(train.DistanceMeters - currentDistanceMeters) ||
            !double.IsFinite(train.SpeedMetersPerSecond) || train.SpeedMetersPerSecond < 0))
            throw new ArgumentException("Other train states must have valid identities, finite positions and non-negative speeds.", nameof(otherTrains));

        return candidates
            .Where(train => !string.Equals(train.TrainId, trainId, StringComparison.Ordinal))
            .Select(train => new
            {
                Train = train,
                DistanceAhead = (train.DistanceMeters - currentDistanceMeters) * direction
            })
            .Where(candidate => candidate.DistanceAhead > 0.5)
            .OrderBy(candidate => candidate.DistanceAhead)
            .Select(candidate => new AiTrainFollowingConstraint(
                candidate.DistanceAhead, candidate.Train.SpeedMetersPerSecond))
            .FirstOrDefault();
    }
}

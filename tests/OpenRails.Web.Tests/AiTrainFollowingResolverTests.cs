using OpenRails.Web.Simulation;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class AiTrainFollowingResolverTests
{
    [Fact]
    public void SelectsNearestTrainAheadRegardlessOfInputOrder()
    {
        var following = AiTrainFollowingResolver.Resolve("AI-1", 100, 1, new[]
        {
            new AiServiceTrainState("AI-3", 180, 4),
            new AiServiceTrainState("PLAYER", 130, 0),
            new AiServiceTrainState("AI-2", 150, 8)
        });

        Assert.NotNull(following);
        Assert.Equal(30, following.DistanceAheadMeters);
        Assert.Equal(0, following.LeadingTrainSpeedMetersPerSecond);
    }

    [Fact]
    public void ResolvesNearestTrainAheadForReverseDirection()
    {
        var following = AiTrainFollowingResolver.Resolve("AI-1", 200, -1, new[]
        {
            new AiServiceTrainState("AI-2", 150, 5),
            new AiServiceTrainState("AI-3", 100, 2)
        });

        Assert.NotNull(following);
        Assert.Equal(50, following.DistanceAheadMeters);
        Assert.Equal(5, following.LeadingTrainSpeedMetersPerSecond);
    }

    [Fact]
    public void IgnoresOwnTrainAndTrainsBehindOrAtSamePosition()
    {
        var following = AiTrainFollowingResolver.Resolve("AI-1", 100, 1, new[]
        {
            new AiServiceTrainState("AI-1", 150, 10),
            new AiServiceTrainState("AI-2", 90, 0),
            new AiServiceTrainState("AI-3", 100, 0)
        });

        Assert.Null(following);
    }

    [Fact]
    public void RejectsInvalidTrainPositionsAndSpeeds()
    {
        Assert.Throws<ArgumentException>(() => AiTrainFollowingResolver.Resolve("AI-1", 0, 1,
            new[] { new AiServiceTrainState("AI-2", double.NaN, 0) }));
        Assert.Throws<ArgumentException>(() => AiTrainFollowingResolver.Resolve("AI-1", 0, 1,
            new[] { new AiServiceTrainState("AI-2", 10, -1) }));
    }
}

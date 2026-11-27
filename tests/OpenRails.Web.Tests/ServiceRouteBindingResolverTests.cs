using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class ServiceRouteBindingResolverTests
{
    [Fact]
    public void ResolvesServicePathAndConsistByOriginalTrainConfigFilename()
    {
        var service = new ServiceMetadata("TRAINS/SERVICES/Express.srv", 1, "Express",
            "ExpressSet", "MainLine", 0.2f, 0.9f, 10);
        var path = new RoutePathDefinition("ROUTES/R/PATHS/MainLine.pat", "MainLine", "Main",
            "A", "B", 0,
            new[] { new RoutePathPoint(0, 0, 0, 0, 0, 1, 1) },
            new[] { new RoutePathNode(0, uint.MaxValue, uint.MaxValue, 0) },
            Array.Empty<string>());
        var consist = new ConsistMetadata("Internal express name", Array.Empty<ConsistCar>(),
            "TRAINS/CONSISTS/ExpressSet.con");

        var binding = Assert.Single(ServiceRouteBindingResolver.Resolve(
            new[] { service }, new[] { path }, new[] { consist }));

        Assert.Same(path, binding.Path);
        Assert.Same(consist, binding.Consist);
        Assert.True(binding.IsRunnable);
        Assert.Null(binding.Diagnostic);
    }

    [Fact]
    public void RejectsAmbiguousPathIdInsteadOfSilentlySelectingFirstMatch()
    {
        var service = new ServiceMetadata("service.srv", 1, "Ambiguous",
            "ExpressSet", "MainLine", 0, 0.9f, 0);
        var first = new RoutePathDefinition("ROUTES/R/PATHS/MainLine.pat", "MainLine", "First",
            "A", "B", 0, Array.Empty<RoutePathPoint>(), Array.Empty<RoutePathNode>(), Array.Empty<string>());
        var second = first with { SourcePath = "ROUTES/Other/PATHS/MainLine.pat", Name = "Second" };
        var consist = new ConsistMetadata("ExpressSet", Array.Empty<ConsistCar>(),
            "TRAINS/CONSISTS/ExpressSet.con");

        var binding = Assert.Single(ServiceRouteBindingResolver.Resolve(
            new[] { service }, new[] { first, second }, new[] { consist }));

        Assert.Null(binding.Path);
        Assert.Same(consist, binding.Consist);
        Assert.False(binding.IsRunnable);
        Assert.Contains("ambiguous route path ID", binding.Diagnostic);
    }

    [Fact]
    public void RejectsAmbiguousConsistReferenceInsteadOfSelectingFirstMatch()
    {
        var service = new ServiceMetadata("service.srv", 1, "Ambiguous",
            "ExpressSet", "MainLine", 0, 0.9f, 0);
        var path = new RoutePathDefinition("ROUTES/R/PATHS/MainLine.pat", "MainLine", "Main",
            "A", "B", 0, Array.Empty<RoutePathPoint>(), Array.Empty<RoutePathNode>(), Array.Empty<string>());
        var first = new ConsistMetadata("First", Array.Empty<ConsistCar>(),
            "TRAINS/CONSISTS/ExpressSet.con");
        var second = new ConsistMetadata("Second", Array.Empty<ConsistCar>(),
            "TRAINS/OTHER/ExpressSet.con");

        var binding = Assert.Single(ServiceRouteBindingResolver.Resolve(
            new[] { service }, new[] { path }, new[] { first, second }));

        Assert.Same(path, binding.Path);
        Assert.Null(binding.Consist);
        Assert.False(binding.IsRunnable);
        Assert.Contains("ambiguous consist", binding.Diagnostic);
    }

    [Fact]
    public void ReportsMissingConsistAndPathReferences()
    {
        var service = new ServiceMetadata("service.srv", 1, "Broken",
            "missing", "missing-path", 0, 0.9f, 0);
        var binding = Assert.Single(ServiceRouteBindingResolver.Resolve(
            new[] { service }, Array.Empty<RoutePathDefinition>(), Array.Empty<ConsistMetadata>()));

        Assert.False(binding.IsRunnable);
        Assert.Contains("missing route path", binding.Diagnostic);
        Assert.Contains("missing consist", binding.Diagnostic);
    }
}

using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class ForestMeshBuilderTests
{
    [Fact]
    public void BuildsDeterministicBillboardsFromOriginalForestParameters()
    {
        var forest = CreateForest();
        var builder = new ForestMeshBuilder();

        var first = builder.Build(forest);
        var second = builder.Build(forest);

        Assert.Equal(2, first.TreeCount);
        Assert.Equal(8, first.Vertices.Length / 5);
        Assert.Equal(12, first.Indices.Length);
        Assert.Equal("trees.ace", first.TextureName);
        Assert.Equal(first.Vertices, second.Vertices);
        Assert.Equal(first.Indices, second.Indices);
        Assert.Equal(0f, first.Vertices[3]);
        Assert.Equal(1f, first.Vertices[4]);
    }

    [Fact]
    public void RejectsUnboundedForestPopulation()
    {
        var forest = CreateForest() with { ForestPopulation = ForestMeshBuilder.MaximumTreesPerForest + 1 };
        Assert.Throws<InvalidDataException>(() => new ForestMeshBuilder().Build(forest));
    }

    [Fact]
    public void IgnoresNonForestPlacements()
    {
        var placement = CreateForest() with { ObjectKind = "Static" };
        Assert.Throws<ArgumentException>(() => new ForestMeshBuilder().Build(placement));
    }

    private static WorldObjectPlacement CreateForest() => new(
        7, "Forest", null, new WorldTilePosition(100, 12, 200), null, null,
        0, 0, null, 0, 0, null,
        "trees.ace", new WorldFloatRange(0.5f, 1.5f),
        new WorldFloatPair(40, 60), 2, new WorldFloatPair(8, 20));
}

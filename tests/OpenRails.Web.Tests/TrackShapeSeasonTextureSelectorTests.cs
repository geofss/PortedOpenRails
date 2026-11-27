using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class TrackShapeSeasonTextureSelectorTests
{
    private static LoadedTrackShapeTexture Create(string season, byte marker) =>
        new(new TrackShapeTextureAssetReference(12, "rail.ace", season, $"textures/{season ?? "base"}/rail.ace"),
            new AceTextureData(1, 1, AceTexturePixelFormat.Rgba8, false, false, 8,
                new[] { new byte[] { marker, marker, marker, 255 } }, Array.Empty<AceTextureChannel>()));

    [Fact]
    public void SelectsRequestedSeasonAndFallsBackToBaseForMissingVariant()
    {
        var assets = new[]
        {
            Create(null!, 10),
            Create("Winter", 20),
            Create("Summer", 30)
        };

        var selected = TrackShapeSeasonTextureSelector.Select(assets, "Winter");
        var item = Assert.Single(selected);
        Assert.Equal("Winter", item.Reference.SeasonFolder);
        Assert.Equal((byte)20, item.Texture.MipLevels[0][0];
    }

    [Fact]
    public void UsesBaseTextureWhenRequestedSeasonIsUnavailable()
    {
        var assets = new[] { Create(null!, 10), Create("Winter", 20) };

        var selected = TrackShapeSeasonTextureSelector.Select(assets, "Autumn");
        var item = Assert.Single(selected);
        Assert.Null(item.Reference.SeasonFolder);
        Assert.Equal((byte)10, item.Texture.MipLevels[0][0]);
    }

    [Fact]
    public void FallsBackToAvailableSeasonWhenBaseAndRequestedSeasonAreAbsent()
    {
        var assets = new[] { Create("Winter", 20), Create("Spring", 30) };

        var selected = TrackShapeSeasonTextureSelector.Select(assets, "Autumn");
        Assert.Equal("Winter", Assert.Single(selected).Reference.SeasonFolder);
    }
}

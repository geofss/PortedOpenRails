using OpenRails.Web.Parsing;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class AceTexturePngEncoderTests
{
    [Fact]
    public void EncodesStructuredRgbaTextureAsPng()
    {
        var texture = new AceTextureData(1, 1, AceTexturePixelFormat.Rgba8,
            false, false, 8, new[] { new byte[] { 255, 0, 0, 255 } },
            Array.Empty<AceTextureChannel>());

        var png = AceTexturePngEncoder.Encode(texture);

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4));
        Assert.Equal("IDAT", System.Text.Encoding.ASCII.GetString(png, 37, 4));
        Assert.StartsWith("data:image/png;base64,", AceTexturePngEncoder.EncodeDataUri(
            new AceTextureData(1, 1, AceTexturePixelFormat.Rgba8, false, false, 8,
                new[] { new byte[] { 0, 0, 0, 255 } }, Array.Empty<AceTextureChannel>()))
            .Split("base64,")[0] + "base64,");
    }

    [Fact]
    public void DecodesPackedBgr565PixelsBeforePngEncoding()
    {
        var texture = new AceTextureData(1, 1, AceTexturePixelFormat.Bgr565,
            false, true, 0, new[] { new byte[] { 0x00, 0xF8 } },
            Array.Empty<AceTextureChannel>());

        var png = AceTexturePngEncoder.Encode(texture);

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
    }

    [Fact]
    public void RejectsDxtFormatsThatNeedGpuDecoding()
    {
        var texture = new AceTextureData(4, 4, AceTexturePixelFormat.Dxt1,
            false, true, 0, new[] { new byte[8] }, Array.Empty<AceTextureChannel>());
        Assert.Throws<NotSupportedException>(() => AceTexturePngEncoder.Encode(texture));
    }
}

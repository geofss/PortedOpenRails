using ORTS.IO;
using Xunit;

namespace OpenRails.Web.Tests;

public sealed class UpstreamIoCompatibilityTests
{
    [Fact]
    public void ByteEncoding_RoundTripsAllByteValues()
    {
        var source = new[] { '\0', '\u007F', '\u0080', '\u00FF' };
        var encoding = new ByteEncoding();

        var bytes = encoding.GetBytes(source, 0, source.Length);
        var decoded = encoding.GetChars(bytes, 0, bytes.Length);

        Assert.Equal(new byte[] { 0x00, 0x7F, 0x80, 0xFF }, bytes);
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void BufferedInMemoryStream_AllowsSeekingWithinBufferedData()
    {
        using var source = new MemoryStream(new byte[] { 10, 20, 30, 40 });
        using var buffered = new BufferedInMemoryStream(source);
        var firstRead = new byte[3];

        Assert.Equal(3, buffered.Read(firstRead, 0, firstRead.Length));
        Assert.Equal(new byte[] { 10, 20, 30 }, firstRead);

        buffered.Seek(0, SeekOrigin.Begin);
        var repeatedRead = new byte[3];

        Assert.Equal(3, buffered.Read(repeatedRead, 0, repeatedRead.Length));
        Assert.Equal(firstRead, repeatedRead);
    }
}

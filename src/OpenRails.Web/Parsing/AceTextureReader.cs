using System.IO.Compression;
using System.Text;
using OpenRails.Web.Content;

namespace OpenRails.Web.Parsing;

/// <summary>
/// Reads original MSTS ACE textures into browser-neutral pixel or compressed mip data.
/// Supports SIMISA compressed/uncompressed headers, structured channels, and raw formats.
/// </summary>
public sealed class AceTextureReader
{
    private const int MaximumDimension = 16384;
    private const int MaximumChannels = 8;
    private const int MaximumTextureBytes = 128 * 1024 * 1024;

    public AceTextureData Read(IVirtualContentStore store, string path)
    {
        ArgumentNullException.ThrowIfNull(store);
        var normalized = VirtualPath.Normalize(path);
        using var stream = store.OpenRead(normalized);
        return Read(stream, normalized);
    }

    public AceTextureData Read(Stream input, string sourcePath = "ACE stream")
    {
        ArgumentNullException.ThrowIfNull(input);
        using var headerReader = new BinaryReader(input, Encoding.ASCII, leaveOpen: true);
        var signature = ReadAscii(headerReader, 8);
        Stream content = input;
        DeflateStream? decompressor = null;

        if (signature == "SIMISA@F")
        {
            var uncompressedSize = headerReader.ReadUInt32();
            if (uncompressedSize > MaximumTextureBytes)
                throw new InvalidDataException("ACE texture declares an uncompressed size above the supported limit.");
            if (ReadAscii(headerReader, 4) != "@@@@")
                throw new InvalidDataException("ACE compressed header has an invalid SIMISA marker.");
            var zlibHeader = headerReader.ReadUInt16();
            if ((zlibHeader & 0x20FF) != 0x0078)
                throw new InvalidDataException($"ACE texture has an unsupported zlib header 0x{zlibHeader:X4}.");

            decompressor = new DeflateStream(input, CompressionMode.Decompress, leaveOpen: true);
            content = decompressor;
        }
        else if (signature == "SIMISA@@")
        {
            if (ReadAscii(headerReader, 8) != "@@@@@@@@")
                throw new InvalidDataException("ACE uncompressed header has an invalid padding marker.");
        }
        else
        {
            throw new InvalidDataException($"ACE texture '{sourcePath}' has an unknown SIMISA signature '{signature}'.");
        }

        try
        {
            using var reader = new BinaryReader(content, Encoding.ASCII, leaveOpen: true);
            return ReadTexture(reader, sourcePath);
        }
        finally
        {
            decompressor?.Dispose();
        }
    }

    private static AceTextureData ReadTexture(BinaryReader reader, string sourcePath)
    {
        if (reader.ReadUInt32() != 1)
            throw new InvalidDataException($"ACE texture '{sourcePath}' has an unsupported internal signature.");

        var options = (AceTextureOptions)reader.ReadInt32();
        var width = reader.ReadInt32();
        var height = reader.ReadInt32();
        var surfaceFormat = reader.ReadInt32();
        var channelCount = reader.ReadInt32();
        if (width <= 0 || height <= 0 || width > MaximumDimension || height > MaximumDimension)
            throw new InvalidDataException($"ACE texture dimensions {width}x{height} are outside the supported range.");
        if (channelCount < 0 || channelCount > MaximumChannels)
            throw new InvalidDataException($"ACE texture channel count {channelCount} is outside the supported range.");

        var mipMapped = (options & AceTextureOptions.MipMaps) != 0;
        var rawData = (options & AceTextureOptions.RawData) != 0;
        if (mipMapped && (width != height || (width & (width - 1)) != 0))
            throw new InvalidDataException("ACE mipmapped textures must be square and have power-of-two dimensions.");

        ReadExactly(reader, 128);
        var imageCount = 1 + (mipMapped ? (int)Math.Log2(width) : 0);
        long totalDecodedBytes = 0;
        for (var i = 0; i < imageCount; i++)
        {
            var mipWidth = Math.Max(1, width >> i);
            var mipHeight = Math.Max(1, height >> i);
            totalDecodedBytes += (long)mipWidth * mipHeight * 4;
        }
        if (!rawData && totalDecodedBytes > MaximumTextureBytes)
            throw new InvalidDataException("ACE texture decoded pixel data exceeds the supported memory limit.");

        var channels = new AceTextureChannel[channelCount];
        var alphaBits = (byte)0;
        for (var i = 0; i < channelCount; i++)
        {
            var sizeValue = reader.ReadUInt64();
            var typeValue = reader.ReadUInt64();
            if (sizeValue is not (1 or 8))
                throw new InvalidDataException($"ACE texture has unsupported channel size {sizeValue}.");
            if (typeValue < 2 || typeValue > 6)
                throw new InvalidDataException($"ACE texture has unknown channel type {typeValue}.");

            channels[i] = new AceTextureChannel((int)sizeValue, (AceTextureChannelType)typeValue);
            if (typeValue == (ulong)AceTextureChannelType.Alpha)
                alphaBits = 8;
            else if (typeValue == (ulong)AceTextureChannelType.Mask && alphaBits == 0)
                alphaBits = 1;
        }

        if (rawData)
            return ReadRawData(reader, width, height, imageCount, surfaceFormat, options, channels, alphaBits);

        return ReadStructuredData(reader, width, height, imageCount, options, channels, alphaBits);
    }

    private static AceTextureData ReadRawData(
        BinaryReader reader,
        int width,
        int height,
        int imageCount,
        int surfaceFormat,
        AceTextureOptions options,
        IReadOnlyList<AceTextureChannel> channels,
        byte alphaBits)
    {
        var format = surfaceFormat switch
        {
            0x0E => AceTexturePixelFormat.Bgr565,
            0x10 => AceTexturePixelFormat.Bgra5551,
            0x11 => AceTexturePixelFormat.Bgra4444,
            0x12 => AceTexturePixelFormat.Dxt1,
            0x14 => AceTexturePixelFormat.Dxt3,
            0x16 => AceTexturePixelFormat.Dxt5,
            _ => throw new InvalidDataException($"ACE texture has unsupported raw surface format 0x{surfaceFormat:X8}.")
        };

        ReadExactly(reader, checked(imageCount * sizeof(int)));
        var mipLevels = new List<byte[]>(imageCount);
        byte[] buffer = Array.Empty<byte>();
        for (var imageIndex = 0; imageIndex < imageCount; imageIndex++)
        {
            var imageWidth = Math.Max(1, width >> imageIndex);
            var imageHeight = Math.Max(1, height >> imageIndex);
            if (imageWidth >= 4 && imageHeight >= 4)
            {
                var length = reader.ReadInt32();
                var maximumLength = ExpectedRawLength(format, imageWidth, imageHeight);
                if (length < 0 || length > MaximumTextureBytes || length > maximumLength * 4L)
                    throw new InvalidDataException($"ACE mip level byte length {length} is outside the supported range.");
                buffer = ReadExactly(reader, length);
            }
            mipLevels.Add((byte[])buffer.Clone());
        }

        return new AceTextureData(width, height, format, (options & AceTextureOptions.MipMaps) != 0,
            true, alphaBits, mipLevels, channels);
    }

    private static AceTextureData ReadStructuredData(
        BinaryReader reader,
        int width,
        int height,
        int imageCount,
        AceTextureOptions options,
        IReadOnlyList<AceTextureChannel> channels,
        byte alphaBits)
    {
        if (!channels.Any(channel => channel.Type == AceTextureChannelType.Red) ||
            !channels.Any(channel => channel.Type == AceTextureChannelType.Green) ||
            !channels.Any(channel => channel.Type == AceTextureChannelType.Blue))
            throw new InvalidDataException("Structured ACE textures must contain red, green, and blue channels.");

        for (var imageIndex = 0; imageIndex < imageCount; imageIndex++)
        {
            var imageHeight = Math.Max(1, height >> imageIndex);
            ReadExactly(reader, checked(sizeof(int) * imageHeight));
        }

        var mipLevels = new List<byte[]>(imageCount);
        for (var imageIndex = 0; imageIndex < imageCount; imageIndex++)
        {
            var imageWidth = Math.Max(1, width >> imageIndex);
            var imageHeight = Math.Max(1, height >> imageIndex);
            var pixelBytes = new byte[checked(imageWidth * imageHeight * 4)];
            var channelBuffers = new byte[7][];

            for (var y = 0; y < imageHeight; y++)
            {
                foreach (var channel in channels)
                {
                    if (channel.Size == 1)
                    {
                        var packed = ReadExactly(reader, (imageWidth + 7) / 8);
                        var expanded = new byte[imageWidth];
                        for (var x = 0; x < imageWidth; x++)
                            expanded[x] = (byte)(((packed[x / 8] >> (7 - (x % 8))) & 1) * 255);
                        channelBuffers[(int)channel.Type] = expanded;
                    }
                    else
                    {
                        channelBuffers[(int)channel.Type] = ReadExactly(reader, imageWidth);
                    }
                }

                for (var x = 0; x < imageWidth; x++)
                {
                    var pixel = (y * imageWidth + x) * 4;
                    pixelBytes[pixel] = channelBuffers[(int)AceTextureChannelType.Red][x];
                    pixelBytes[pixel + 1] = channelBuffers[(int)AceTextureChannelType.Green][x];
                    pixelBytes[pixel + 2] = channelBuffers[(int)AceTextureChannelType.Blue][x];
                    pixelBytes[pixel + 3] = channelBuffers[(int)AceTextureChannelType.Alpha] is { } alpha
                        ? alpha[x]
                        : channelBuffers[(int)AceTextureChannelType.Mask] is { } mask ? mask[x] : (byte)255;
                }
            }

            mipLevels.Add(pixelBytes);
        }

        return new AceTextureData(width, height, AceTexturePixelFormat.Rgba8,
            (options & AceTextureOptions.MipMaps) != 0, false, alphaBits, mipLevels, channels);
    }

    private static int ExpectedRawLength(AceTexturePixelFormat format, int width, int height) =>
        format switch
        {
            AceTexturePixelFormat.Dxt1 => Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * 8,
            AceTexturePixelFormat.Dxt3 or AceTexturePixelFormat.Dxt5 =>
                Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * 16,
            AceTexturePixelFormat.Bgr565 or AceTexturePixelFormat.Bgra5551 or AceTexturePixelFormat.Bgra4444 => width * height * 2,
            _ => throw new InvalidDataException("Unsupported ACE raw texture format.")
        };

    private static string ReadAscii(BinaryReader reader, int count) =>
        Encoding.ASCII.GetString(ReadExactly(reader, count));

    private static byte[] ReadExactly(BinaryReader reader, int count)
    {
        if (count < 0 || count > MaximumTextureBytes)
            throw new InvalidDataException($"ACE texture byte count {count} is outside the supported range.");
        var bytes = reader.ReadBytes(count);
        if (bytes.Length != count)
            throw new EndOfStreamException($"ACE texture ended after {bytes.Length} of {count} requested bytes.");
        return bytes;
    }
}

[Flags]
public enum AceTextureOptions
{
    None = 0,
    MipMaps = 0x01,
    RawData = 0x10
}

public enum AceTextureChannelType
{
    Mask = 2,
    Red = 3,
    Green = 4,
    Blue = 5,
    Alpha = 6
}

public enum AceTexturePixelFormat
{
    Rgba8,
    Bgr565,
    Bgra5551,
    Bgra4444,
    Dxt1,
    Dxt3,
    Dxt5
}

public sealed record AceTextureChannel(int Size, AceTextureChannelType Type);

public sealed class AceTextureData
{
    public int Width { get; }
    public int Height { get; }
    public AceTexturePixelFormat PixelFormat { get; }
    public bool HasMipMaps { get; }
    public bool IsRawData { get; }
    public byte AlphaBits { get; }
    public IReadOnlyList<byte[]> MipLevels { get; }
    public IReadOnlyList<AceTextureChannel> Channels { get; }

    public AceTextureData(int width, int height, AceTexturePixelFormat pixelFormat, bool hasMipMaps,
        bool isRawData, byte alphaBits, IEnumerable<byte[]> mipLevels, IEnumerable<AceTextureChannel> channels)
    {
        Width = width;
        Height = height;
        PixelFormat = pixelFormat;
        HasMipMaps = hasMipMaps;
        IsRawData = isRawData;
        AlphaBits = alphaBits;
        MipLevels = Array.AsReadOnly(mipLevels.Select(level => (byte[])level.Clone()).ToArray());
        Channels = Array.AsReadOnly(channels.ToArray());
    }
}

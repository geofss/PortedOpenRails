using System.Buffers.Binary;
using System.IO.Compression;

namespace OpenRails.Web.Parsing;

/// <summary>Encodes decoded or packed ACE mip level zero as a browser-displayable PNG.</summary>
public static class AceTexturePngEncoder
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static byte[] Encode(AceTextureData texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (texture.MipLevels.Count == 0)
            throw new InvalidDataException("ACE texture contains no base mip level.");
        var rgba = DecodeBaseLevel(texture);
        using var output = new MemoryStream();
        output.Write(PngSignature);
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header[..4], texture.Width);
        BinaryPrimitives.WriteInt32BigEndian(header.Slice(4, 4), texture.Height);
        header[8] = 8;
        header[9] = 6;
        header[10] = 0;
        header[11] = 0;
        header[12] = 0;
        WriteChunk(output, "IHDR", header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            var rowLength = checked(texture.Width * 4);
            var row = new byte[checked(rowLength + 1)];
            for (var y = 0; y < texture.Height; y++)
            {
                row[0] = 0;
                Buffer.BlockCopy(rgba, y * rowLength, row, 1, rowLength);
                zlib.Write(row);
            }
        }
        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", ReadOnlySpan<byte>.Empty);
        return output.ToArray();
    }

    public static string EncodeDataUri(AceTextureData texture) =>
        "data:image/png;base64," + Convert.ToBase64String(Encode(texture));

    private static byte[] DecodeBaseLevel(AceTextureData texture)
    {
        var source = texture.MipLevels[0];
        var pixelCount = checked(texture.Width * texture.Height);
        if (!texture.IsRawData && texture.PixelFormat == AceTexturePixelFormat.Rgba8)
        {
            if (source.Length != checked(pixelCount * 4))
                throw new InvalidDataException("ACE RGBA base mip has an unexpected byte length.");
            return source;
        }

        var rgba = new byte[checked(pixelCount * 4)];
        switch (texture.PixelFormat)
        {
            case AceTexturePixelFormat.Bgr565:
            case AceTexturePixelFormat.Bgra5551:
            case AceTexturePixelFormat.Bgra4444:
                DecodePacked16(texture, source, rgba);
                return rgba;
            case AceTexturePixelFormat.Dxt1:
            case AceTexturePixelFormat.Dxt3:
            case AceTexturePixelFormat.Dxt5:
                DecodeDxt(texture, source, rgba);
                return rgba;
            default:
                throw new NotSupportedException($"ACE pixel format {texture.PixelFormat} is not supported for PNG export.");
        }
    }

    private static void DecodeDxt(AceTextureData texture, byte[] source, byte[] destination)
    {
        var isDxt1 = texture.PixelFormat == AceTexturePixelFormat.Dxt1;
        var blockBytes = isDxt1 ? 8 : 16;
        var blocksWide = (texture.Width + 3) / 4;
        var blocksHigh = (texture.Height + 3) / 4;
        if (source.Length < blocksWide * blocksHigh * blockBytes)
            throw new InvalidDataException("ACE DXT base mip is shorter than its declared block dimensions.");

        for (var blockY = 0; blockY < blocksHigh; blockY++)
        for (var blockX = 0; blockX < blocksWide; blockX++)
        {
            var offset = (blockY * blocksWide + blockX) * blockBytes;
            var alphaOffset = offset;
            var colourOffset = isDxt1 ? offset : offset + 8;
            var alphaValues = texture.PixelFormat switch
            {
                AceTexturePixelFormat.Dxt3 => DecodeDxt3Alpha(source.AsSpan(alphaOffset, 8)),
                AceTexturePixelFormat.Dxt5 => DecodeDxt5Alpha(source.AsSpan(alphaOffset, 8)),
                _ => null
            };
            var colours = DecodeDxtColours(source.AsSpan(colourOffset, 8), forceFourColours: !isDxt1);
            var indices = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(colourOffset + 4, 4));
            for (var pixel = 0; pixel < 16; pixel++)
            {
                var x = blockX * 4 + pixel % 4;
                var y = blockY * 4 + pixel / 4;
                if (x >= texture.Width || y >= texture.Height)
                    continue;
                var colourIndex = (int)((indices >> (pixel * 2)) & 3);
                var colour = colours[colourIndex];
                var target = (y * texture.Width + x) * 4;
                destination[target] = colour.R;
                destination[target + 1] = colour.G;
                destination[target + 2] = colour.B;
                destination[target + 3] = alphaValues is null ? colour.A : alphaValues[pixel];
            }
        }
    }

    private static (byte R, byte G, byte B, byte A)[] DecodeDxtColours(ReadOnlySpan<byte> block, bool forceFourColours)
    {
        var colour0 = BinaryPrimitives.ReadUInt16LittleEndian(block[..2]);
        var colour1 = BinaryPrimitives.ReadUInt16LittleEndian(block.Slice(2, 2));
        var first = DecodeRgb565(colour0);
        var second = DecodeRgb565(colour1);
        var palette = new (byte R, byte G, byte B, byte A)[4];
        palette[0] = (first.R, first.G, first.B, 255);
        palette[1] = (second.R, second.G, second.B, 255);
        if (colour0 > colour1 || forceFourColours)
        {
            palette[2] = (Average(first.R, second.R, 2, 1), Average(first.G, second.G, 2, 1),
                Average(first.B, second.B, 2, 1), 255);
            palette[3] = (Average(first.R, second.R, 1, 2), Average(first.G, second.G, 1, 2),
                Average(first.B, second.B, 1, 2), 255);
        }
        else
        {
            palette[2] = (Average(first.R, second.R, 1, 1), Average(first.G, second.G, 1, 1),
                Average(first.B, second.B, 1, 1), 255);
            palette[3] = (0, 0, 0, 0);
        }
        return palette;
    }

    private static (byte R, byte G, byte B) DecodeRgb565(ushort value) =>
        (Expand5((value >> 11) & 0x1F), Expand6((value >> 5) & 0x3F), Expand5(value & 0x1F));

    private static byte[] DecodeDxt3Alpha(ReadOnlySpan<byte> block)
    {
        var packed = BinaryPrimitives.ReadUInt64LittleEndian(block);
        var alpha = new byte[16];
        for (var i = 0; i < alpha.Length; i++)
            alpha[i] = Expand4((int)((packed >> (i * 4)) & 0x0F));
        return alpha;
    }

    private static byte[] DecodeDxt5Alpha(ReadOnlySpan<byte> block)
    {
        var first = block[0];
        var second = block[1];
        var table = new byte[8];
        table[0] = first;
        table[1] = second;
        if (first > second)
        {
            for (var i = 1; i <= 6; i++)
                table[i + 1] = (byte)(((7 - i) * first + i * second) / 7);
        }
        else
        {
            for (var i = 1; i <= 4; i++)
                table[i + 1] = (byte)(((5 - i) * first + i * second) / 5);
            table[6] = 0;
            table[7] = 255;
        }
        ulong indices = 0;
        for (var i = 0; i < 6; i++)
            indices |= (ulong)block[i + 2] << (8 * i);
        var alpha = new byte[16];
        for (var i = 0; i < alpha.Length; i++)
            alpha[i] = table[(int)((indices >> (i * 3)) & 7)];
        return alpha;
    }

    private static byte Average(byte first, byte second, int firstWeight, int secondWeight) =>
        (byte)((first * firstWeight + second * secondWeight) / (firstWeight + secondWeight));

    private static void DecodePacked16(AceTextureData texture, byte[] source, byte[] destination)
    {
        if (source.Length != checked(texture.Width * texture.Height * 2))
            throw new InvalidDataException("ACE packed 16-bit base mip has an unexpected byte length.");
        for (var pixel = 0; pixel < texture.Width * texture.Height; pixel++)
        {
            var value = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(pixel * 2, 2));
            var target = pixel * 4;
            if (texture.PixelFormat == AceTexturePixelFormat.Bgr565)
            {
                destination[target] = Expand5((value >> 11) & 0x1F);
                destination[target + 1] = Expand6((value >> 5) & 0x3F);
                destination[target + 2] = Expand5(value & 0x1F);
                destination[target + 3] = 255;
            }
            else if (texture.PixelFormat == AceTexturePixelFormat.Bgra5551)
            {
                destination[target] = Expand5((value >> 10) & 0x1F);
                destination[target + 1] = Expand5((value >> 5) & 0x1F);
                destination[target + 2] = Expand5(value & 0x1F);
                destination[target + 3] = (value & 0x8000) != 0 ? (byte)255 : (byte)0;
            }
            else
            {
                destination[target] = Expand4((value >> 8) & 0x0F);
                destination[target + 1] = Expand4((value >> 4) & 0x0F);
                destination[target + 2] = Expand4(value & 0x0F);
                destination[target + 3] = Expand4((value >> 12) & 0x0F);
            }
        }
    }

    private static byte Expand4(int value) => (byte)(value * 17);
    private static byte Expand5(int value) => (byte)((value << 3) | (value >> 2));
    private static byte Expand6(int value) => (byte)((value << 2) | (value >> 4));

    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        var crcInput = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput.AsSpan(typeBytes.Length));
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, ComputeCrc32(crcInput));
        output.Write(crc);
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }
}

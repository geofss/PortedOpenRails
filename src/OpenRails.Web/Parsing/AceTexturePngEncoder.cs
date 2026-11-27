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
            default:
                throw new NotSupportedException(
                    $"ACE pixel format {texture.PixelFormat} must be decoded by a browser GPU path before PNG export.");
        }
    }

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

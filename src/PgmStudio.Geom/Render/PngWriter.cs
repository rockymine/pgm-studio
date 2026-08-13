using System.IO.Compression;

namespace PgmStudio.Geom.Render;

/// <summary>
/// Writes a 24-bit RGB image as a PNG: signature, IHDR, one IDAT, IEND. Deliberately minimal — no palette,
/// no interlacing, no ancillary chunks — because the only image the studio's block renders emit is a block
/// raster, where every pixel is already one opaque colour. Zero dependencies: every stage image the studio
/// produces, from a world read-back to a rasterized plan, goes through this one encoder rather than pulling
/// in an imaging library for one write call.
///
/// <para>The pixel stream is filtered per row with the <b>Up</b> filter (each byte minus the byte above it).
/// A top-down block render is bands of one terrain colour repeated down the image, so subtracting the row
/// above leaves long zero runs that deflate to almost nothing; the <b>Sub</b> filter would do the same job
/// along a row, but terrain is more consistent between rows than within one. The zlib wrapper deflate needs
/// comes from <see cref="ZLibStream"/>, which writes the header and the trailing Adler-32 itself.</para>
/// </summary>
public static class PngWriter
{
    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Serialises <paramref name="rgb"/> — three bytes per pixel, row-major, <paramref name="width"/>
    /// pixels per row — to <paramref name="path"/>.</summary>
    public static void Write(string path, int width, int height, byte[] rgb)
        => File.WriteAllBytes(path, Encode(width, height, rgb));

    /// <summary>Serialises <paramref name="rgb"/> to a PNG byte array without touching disk — the path an HTTP
    /// endpoint writes into a response body.</summary>
    public static byte[] Encode(int width, int height, byte[] rgb)
    {
        if (rgb.Length != width * height * 3)
            throw new ArgumentException($"expected {width * height * 3} bytes for {width}x{height}, got {rgb.Length}");

        using var file = new MemoryStream();
        file.Write(Signature);

        var header = new byte[13];
        WriteBigEndian(header, 0, width);
        WriteBigEndian(header, 4, height);
        header[8] = 8;      // bit depth
        header[9] = 2;      // colour type 2 = truecolour RGB
        header[10] = 0;     // deflate
        header[11] = 0;     // adaptive filtering
        header[12] = 0;     // no interlace
        WriteChunk(file, "IHDR", header);

        WriteChunk(file, "IDAT", Deflate(width, height, rgb));
        WriteChunk(file, "IEND", []);
        return file.ToArray();
    }

    /// <summary>Row-filtered scanlines through zlib. Each row is prefixed with filter type 2 (Up) and holds
    /// the byte-wise difference against the row above; the first row differences against implicit zeroes,
    /// which leaves it unchanged.</summary>
    private static byte[] Deflate(int width, int height, byte[] rgb)
    {
        var stride = width * 3;
        var filtered = new byte[height * (stride + 1)];
        for (var row = 0; row < height; row++)
        {
            var source = row * stride;
            var target = row * (stride + 1);
            filtered[target] = 2;
            for (var offset = 0; offset < stride; offset++)
            {
                var above = row == 0 ? (byte)0 : rgb[source - stride + offset];
                filtered[target + 1 + offset] = (byte)(rgb[source + offset] - above);
            }
        }

        using var compressed = new MemoryStream();
        using (var deflate = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(filtered);
        return compressed.ToArray();
    }

    private static void WriteChunk(Stream target, string type, byte[] payload)
    {
        var length = new byte[4];
        WriteBigEndian(length, 0, payload.Length);
        target.Write(length);

        var typeBytes = new byte[4];
        for (var index = 0; index < 4; index++) typeBytes[index] = (byte)type[index];
        target.Write(typeBytes);
        target.Write(payload);

        // The CRC covers the type and the payload, but not the length.
        var crc = Crc32(Crc32(0xFFFFFFFF, typeBytes), payload) ^ 0xFFFFFFFF;
        var crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, (int)crc);
        target.Write(crcBytes);
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (var index = 0u; index < 256; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
                value = (value & 1) != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1;
            table[index] = value;
        }
        return table;
    }

    private static uint Crc32(uint seed, byte[] bytes)
    {
        var crc = seed;
        foreach (var value in bytes) crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static void WriteBigEndian(byte[] target, int offset, int value)
    {
        target[offset] = (byte)(value >> 24);
        target[offset + 1] = (byte)(value >> 16);
        target[offset + 2] = (byte)(value >> 8);
        target[offset + 3] = (byte)value;
    }
}

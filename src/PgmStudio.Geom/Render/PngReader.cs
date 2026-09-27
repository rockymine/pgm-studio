using System.Buffers.Binary;
using System.IO.Compression;

namespace PgmStudio.Geom.Render;

/// <summary>An image read out of a PNG: <paramref name="Rgba"/> is four bytes a pixel, row-major, top row
/// first.</summary>
public readonly record struct PngImage(int Width, int Height, byte[] Rgba);

/// <summary>
/// Reads a PNG into RGBA — the counterpart of <see cref="PngWriter"/>, and as deliberately narrow.
///
/// <para>It reads what a block texture is stored as: eight bits a channel, greyscale or truecolour, with or
/// without alpha, not interlaced. Anything else — a palette, sixteen-bit channels, Adam7 — is a
/// <see cref="FormatException"/> naming what it met rather than a guess at the pixels.</para>
/// </summary>
public static class PngReader
{
    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>The pixels <paramref name="png"/> holds, as RGBA.</summary>
    public static PngImage Decode(ReadOnlySpan<byte> png)
    {
        if (png.Length < Signature.Length || !png[..Signature.Length].SequenceEqual(Signature))
            throw new FormatException("not a PNG: the signature is missing");

        int width = 0, height = 0, channels = 0;
        using var compressed = new MemoryStream();
        var at = Signature.Length;
        while (at + 8 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png[at..]);
            var type = System.Text.Encoding.ASCII.GetString(png.Slice(at + 4, 4));
            if (length < 0 || at + 12 + length > png.Length)
                throw new FormatException($"the {type} chunk runs past the end of the file");
            var body = png.Slice(at + 8, length);
            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(body);
                    height = BinaryPrimitives.ReadInt32BigEndian(body[4..]);
                    channels = Channels(bitDepth: body[8], colourType: body[9]);
                    if (body[12] != 0) throw new FormatException("an interlaced PNG is not read");
                    break;
                case "IDAT":
                    compressed.Write(body);
                    break;
                case "IEND":
                    return Unfilter(Inflate(compressed.ToArray()), width, height, channels);
            }
            at += 12 + length;
        }
        throw new FormatException("the PNG ends before its IEND chunk");
    }

    private static int Channels(byte bitDepth, byte colourType)
    {
        if (bitDepth != 8) throw new FormatException($"a bit depth of {bitDepth} is not read; only 8");
        return colourType switch
        {
            0 => 1,
            2 => 3,
            4 => 2,
            6 => 4,
            _ => throw new FormatException($"colour type {colourType} is not read; only greyscale and truecolour"),
        };
    }

    private static byte[] Inflate(byte[] compressed)
    {
        using var zlib = new ZLibStream(new MemoryStream(compressed), CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        return raw.ToArray();
    }

    /// <summary>Undoes the per-row filters and widens every pixel to RGBA.</summary>
    private static PngImage Unfilter(byte[] raw, int width, int height, int channels)
    {
        if (width <= 0 || height <= 0) throw new FormatException("the PNG has no IHDR, or states no size");
        var stride = width * channels;
        if (raw.Length < (stride + 1) * height)
            throw new FormatException($"the image data holds {raw.Length} bytes where {(stride + 1) * height} are needed");

        var rows = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            var source = raw.AsSpan(y * (stride + 1) + 1, stride);
            var row = rows.AsSpan(y * stride, stride);
            var above = y > 0 ? rows.AsSpan((y - 1) * stride, stride) : Span<byte>.Empty;
            for (var i = 0; i < stride; i++)
            {
                int left = i >= channels ? row[i - channels] : 0;
                int up = y > 0 ? above[i] : 0;
                int upLeft = y > 0 && i >= channels ? above[i - channels] : 0;
                row[i] = (byte)(source[i] + filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new FormatException($"row {y} names filter {filter}, which does not exist"),
                });
            }
        }

        var rgba = new byte[width * height * 4];
        for (var pixel = 0; pixel < width * height; pixel++)
        {
            var start = pixel * channels;
            var (red, green, blue, alpha) = channels switch
            {
                1 => (rows[start], rows[start], rows[start], (byte)255),
                2 => (rows[start], rows[start], rows[start], rows[start + 1]),
                3 => (rows[start], rows[start + 1], rows[start + 2], (byte)255),
                _ => (rows[start], rows[start + 1], rows[start + 2], rows[start + 3]),
            };
            rgba[pixel * 4] = red; rgba[pixel * 4 + 1] = green; rgba[pixel * 4 + 2] = blue; rgba[pixel * 4 + 3] = alpha;
        }
        return new PngImage(width, height, rgba);
    }

    private static int Paeth(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        int toLeft = Math.Abs(estimate - left), toUp = Math.Abs(estimate - up), toUpLeft = Math.Abs(estimate - upLeft);
        return toLeft <= toUp && toLeft <= toUpLeft ? left : toUp <= toUpLeft ? up : upLeft;
    }
}

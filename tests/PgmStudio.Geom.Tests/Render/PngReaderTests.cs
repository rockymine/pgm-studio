using System.Buffers.Binary;
using System.IO.Compression;
using PgmStudio.Geom.Render;

namespace PgmStudio.Geom.Tests.Render;

/// <summary>
/// The reader is held to two things: it reads back exactly what the studio's own writer wrote, and it undoes
/// every one of the five row filters, since a texture file uses whichever its encoder chose per row. Anything
/// outside the eight-bit formats it reads is a named refusal rather than wrong pixels.
/// </summary>
public sealed class PngReaderTests
{
    [Test]
    public async Task What_the_writer_writes_the_reader_reads_back_opaque()
    {
        byte[] rgb = [255, 0, 0, 0, 255, 0, 0, 0, 255, 10, 20, 30, 40, 50, 60, 70, 80, 90];
        var image = PngReader.Decode(PngWriter.Encode(3, 2, rgb));

        await Assert.That(image.Width).IsEqualTo(3);
        await Assert.That(image.Height).IsEqualTo(2);
        for (var pixel = 0; pixel < 6; pixel++)
        {
            await Assert.That(image.Rgba[pixel * 4]).IsEqualTo(rgb[pixel * 3]);
            await Assert.That(image.Rgba[pixel * 4 + 1]).IsEqualTo(rgb[pixel * 3 + 1]);
            await Assert.That(image.Rgba[pixel * 4 + 2]).IsEqualTo(rgb[pixel * 3 + 2]);
            await Assert.That(image.Rgba[pixel * 4 + 3]).IsEqualTo((byte)255);
        }
    }

    /// <summary>Five rows of RGBA, each filtered a different way, decode to the same pixels they were filtered
    /// from — which is the only way to know the Paeth predictor's tie-breaking is the standard's.</summary>
    [Test]
    public async Task Every_row_filter_is_undone()
    {
        const int width = 3, height = 5;
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i++) pixels[i] = (byte)((i * 37 + 11) % 251);

        var image = PngReader.Decode(Rgba(width, height, pixels, filters: [0, 1, 2, 3, 4]));

        await Assert.That(image.Rgba).IsEquivalentTo(pixels);
    }

    [Test]
    public async Task A_format_it_does_not_read_is_refused_by_name()
    {
        var sixteenBit = Rgba(1, 1, new byte[4], filters: [0], bitDepth: 16);

        await Assert.That(() => PngReader.Decode(sixteenBit)).Throws<FormatException>();
        await Assert.That(() => PngReader.Decode([1, 2, 3])).Throws<FormatException>();
    }

    /// <summary>An RGBA PNG whose rows are filtered as asked, row by row.</summary>
    private static byte[] Rgba(int width, int height, byte[] pixels, int[] filters, byte bitDepth = 8)
    {
        var stride = width * 4;
        var raw = new byte[(stride + 1) * height];
        for (var y = 0; y < height; y++)
        {
            var filter = filters[y % filters.Length];
            raw[y * (stride + 1)] = (byte)filter;
            for (var i = 0; i < stride; i++)
            {
                int value = pixels[y * stride + i];
                int left = i >= 4 ? pixels[y * stride + i - 4] : 0;
                int up = y > 0 ? pixels[(y - 1) * stride + i] : 0;
                int upLeft = y > 0 && i >= 4 ? pixels[(y - 1) * stride + i - 4] : 0;
                raw[y * (stride + 1) + 1 + i] = (byte)(value - filter switch
                {
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => 0,
                });
            }
        }

        using var file = new MemoryStream();
        file.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = bitDepth;
        header[9] = 6;
        Chunk(file, "IHDR", header);
        using (var compressed = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(raw);
            Chunk(file, "IDAT", compressed.ToArray());
        }
        Chunk(file, "IEND", []);
        return file.ToArray();
    }

    private static void Chunk(Stream file, string type, byte[] body)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, body.Length);
        file.Write(length);
        file.Write(System.Text.Encoding.ASCII.GetBytes(type));
        file.Write(body);
        file.Write(new byte[4]);   // the CRC, which the reader does not check
    }

    private static int Paeth(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        int toLeft = Math.Abs(estimate - left), toUp = Math.Abs(estimate - up), toUpLeft = Math.Abs(estimate - upLeft);
        return toLeft <= toUp && toLeft <= toUpLeft ? left : toUp <= toUpLeft ? up : upLeft;
    }
}

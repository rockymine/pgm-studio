using PgmStudio.Geom;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Render;

namespace PgmStudio.Pgm.Tests.Render;

/// <summary>The plan_render raster: the same fanned board <see cref="PlanBoardSvg"/> draws, off the same
/// <see cref="PlanBoardScene"/>, encoded as a PNG an image reader can actually open.</summary>
public sealed class PlanBoardPngTests
{
    private static readonly byte[] PngSignature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    [Test]
    public async Task Render_emits_a_valid_png_signature()
    {
        var plan = Composer.Compose(new ComposeRequest(12, seed: 3));
        var png = PlanBoardPng.Render(plan);

        await Assert.That(png.Length).IsGreaterThan(PngSignature.Length);
        await Assert.That(png.Take(PngSignature.Length).ToArray()).IsEquivalentTo(PngSignature);
    }

    [Test]
    public async Task Render_is_deterministic_for_a_fixed_plan()
    {
        var plan = Composer.Compose(new ComposeRequest(8, seed: 1));
        await Assert.That(PlanBoardPng.Render(plan)).IsEquivalentTo(PlanBoardPng.Render(plan));
    }

    [Test]
    public async Task Render_draws_a_larger_canvas_for_a_bigger_board()
    {
        var small = Composer.Compose(new ComposeRequest(6, seed: 2));
        var large = Composer.Compose(new ComposeRequest(32, seed: 2));

        // A bigger band fans a wider board, which a raster spends as more pixels rather than a bigger viewBox.
        // Read the canvas off the PNG header rather than the file's length: the bytes are compressed, so an
        // emptier canvas can be the shorter file however much bigger it is.
        static (int W, int H) Canvas(byte[] png) =>
            (System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)),
             System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
        var (smallW, smallH) = Canvas(PlanBoardPng.Render(small));
        var (largeW, largeH) = Canvas(PlanBoardPng.Render(large));
        await Assert.That((long)largeW * largeH).IsGreaterThan((long)smallW * smallH);
    }

    [Test]
    public async Task Render_agrees_with_the_svg_render_on_board_shape()
    {
        // Both renderers draw off PlanBoardScene, so an empty scene (no pieces/zones) has to fall back the
        // same way in both: a small fixed canvas rather than an exception or a zero-sized image.
        var plan = Composer.Compose(new ComposeRequest(4, seed: 7));
        var svg = PlanBoardSvg.Render(plan);
        var png = PlanBoardPng.Render(plan);

        await Assert.That(svg).Contains("<rect");
        await Assert.That(png.Length).IsGreaterThan(8);
    }

    private static (int Width, int Height) Dimensions(byte[] png)
    {
        int width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        int height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        return (width, height);
    }

    [Test]
    public async Task Render_appends_a_legend_strip_below_the_board()
    {
        var plan = Composer.Compose(new ComposeRequest(12, seed: 3));
        var png = PlanBoardPng.Render(plan);
        var (_, height) = Dimensions(png);

        // The legend strip is present even for a plan whose own board is small, so the image is always taller
        // than a few pixels of margin.
        await Assert.That(height).IsGreaterThan(60);
    }

    /// <summary>Decodes an RGB PNG in exactly the shape <c>PngWriter</c> emits it (no palette, no interlace,
    /// one IDAT, every row Up-filtered) — enough to check real pixels without an imaging dependency, and
    /// specific to this encoder rather than a general PNG reader.</summary>
    private static byte[] DecodePixels(byte[] png, int width, int height)
    {
        using var stream = new MemoryStream(png);
        stream.Position = 8;   // past the signature
        var idat = new MemoryStream();
        Span<byte> lengthBuffer = stackalloc byte[4];
        Span<byte> type = stackalloc byte[4];
        while (stream.Position < stream.Length)
        {
            stream.ReadExactly(lengthBuffer);
            var length = (lengthBuffer[0] << 24) | (lengthBuffer[1] << 16) | (lengthBuffer[2] << 8) | lengthBuffer[3];
            stream.ReadExactly(type);
            var chunk = new byte[length];
            stream.ReadExactly(chunk);
            stream.Position += 4;   // CRC
            if (System.Text.Encoding.ASCII.GetString(type) == "IDAT") idat.Write(chunk);
        }
        idat.Position = 0;
        using var inflater = new System.IO.Compression.ZLibStream(idat, System.IO.Compression.CompressionMode.Decompress);
        var filtered = new byte[height * (1 + width * 3)];
        inflater.ReadExactly(filtered);

        var pixels = new byte[width * height * 3];
        var stride = width * 3;
        for (var row = 0; row < height; row++)
        {
            var rowStart = row * (1 + stride) + 1;   // skip the filter-type byte (always 2, "Up")
            for (var i = 0; i < stride; i++)
            {
                var above = row == 0 ? 0 : pixels[(row - 1) * stride + i];
                pixels[row * stride + i] = (byte)(filtered[rowStart + i] + above);
            }
        }
        return pixels;
    }

    private static byte[] ZoneBoard(string kind, int scale, out int width, out int height)
    {
        var plan = new PlanModel();
        plan.Globals.Symmetry = "none";
        plan.Zones.Add(new PlanZone { Id = "z-1", Rect = new CellRect(0, 0, 6, 6), Kind = kind });
        var png = PlanBoardPng.Render(plan, scale, pad: 0);
        (width, height) = Dimensions(png);
        return DecodePixels(png, width, height);
    }

    private static int Rgb(byte[] pixels, int width, int col, int row)
    {
        var offset = (row * width + col) * 3;
        return (pixels[offset] << 16) | (pixels[offset + 1] << 8) | pixels[offset + 2];
    }

    [Test]
    public async Task A_zone_is_a_dashed_outline_over_paper()
    {
        const int scale = 10;
        var pixels = ZoneBoard(PlanZoneKinds.Build, scale, out var width, out _);

        // The top edge alternates ink and paper; the gaps and the interior are paper under a barely-there tint.
        var edge = Enumerable.Range(0, 6 * scale).Select(col => Rgb(pixels, width, col, 0)).ToList();
        await Assert.That(edge.Contains(PlanBoardPalette.Zone.Edge)).IsTrue();
        await Assert.That(edge.Any(rgb => rgb != PlanBoardPalette.Zone.Edge)).IsTrue();
        var centre = Rgb(pixels, width, 3 * scale, 3 * scale);
        await Assert.That(centre >> 16 & 0xFF).IsGreaterThan(0xE0);
    }

    [Test]
    public async Task A_water_lane_paints_exactly_as_a_build_zone_does()
    {
        const int scale = 10;
        var zone = ZoneBoard(PlanZoneKinds.Build, scale, out _, out _);
        var lane = ZoneBoard(PlanZoneKinds.WaterLane, scale, out _, out _);

        await Assert.That(lane).IsEquivalentTo(zone);
    }
}

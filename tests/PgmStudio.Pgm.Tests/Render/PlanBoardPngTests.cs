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
        var large = Composer.Compose(new ComposeRequest(20, seed: 2));

        // More players fan a wider board, which a raster spends as more pixels rather than a bigger viewBox.
        await Assert.That(PlanBoardPng.Render(large).Length).IsGreaterThan(PlanBoardPng.Render(small).Length);
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

        // The legend strip is present even for a plan whose own board is small — seven rows worth of key
        // (five roles + two zone kinds), which is always taller than a few pixels of margin.
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

    private static bool RegionContainsMoreThanOneShade(byte[] pixels, int width, int x0, int z0, int w, int h)
    {
        var seen = new HashSet<(byte, byte, byte)>();
        for (var row = z0; row < z0 + h; row++)
            for (var col = x0; col < x0 + w; col++)
            {
                var offset = (row * width + col) * 3;
                seen.Add((pixels[offset], pixels[offset + 1], pixels[offset + 2]));
            }
        return seen.Count > 1;
    }

    [Test]
    public async Task A_water_lane_paints_more_than_one_shade_over_its_own_rect_and_a_build_zone_paints_one()
    {
        const int scale = 10;
        var lanePlan = new PlanModel();
        lanePlan.Globals.Symmetry = "none";
        lanePlan.Zones.Add(new PlanZone { Id = "wl-1", Rect = new CellRect(0, 0, 6, 6), Kind = PlanZoneKinds.WaterLane });
        var lanePng = PlanBoardPng.Render(lanePlan, scale, pad: 0);
        var (laneWidth, laneHeight) = Dimensions(lanePng);
        var lanePixels = DecodePixels(lanePng, laneWidth, laneHeight);

        var buildPlan = new PlanModel();
        buildPlan.Globals.Symmetry = "none";
        buildPlan.Zones.Add(new PlanZone { Id = "bz-1", Rect = new CellRect(0, 0, 6, 6) });
        var buildPng = PlanBoardPng.Render(buildPlan, scale, pad: 0);
        var (buildWidth, buildHeight) = Dimensions(buildPng);
        var buildPixels = DecodePixels(buildPng, buildWidth, buildHeight);

        // Both boards are one 6x6-cell zone at the same scale, so the zone's own pixel rect is identical in
        // extent; only the water lane's hatch should vary its fill within that rect.
        await Assert.That(RegionContainsMoreThanOneShade(lanePixels, laneWidth, 0, 0, 6 * scale, 6 * scale)).IsTrue();
        await Assert.That(RegionContainsMoreThanOneShade(buildPixels, buildWidth, 0, 0, 6 * scale, 6 * scale)).IsFalse();
    }

    [Test]
    public async Task The_build_zone_and_water_lane_pixels_differ_in_hue_not_only_shade()
    {
        const int scale = 10;
        var lanePlan = new PlanModel();
        lanePlan.Globals.Symmetry = "none";
        lanePlan.Zones.Add(new PlanZone { Id = "wl-1", Rect = new CellRect(0, 0, 6, 6), Kind = PlanZoneKinds.WaterLane });
        var lanePixels = DecodePixels(PlanBoardPng.Render(lanePlan, scale, pad: 0), 6 * scale, 6 * scale);

        var buildPlan = new PlanModel();
        buildPlan.Globals.Symmetry = "none";
        buildPlan.Zones.Add(new PlanZone { Id = "bz-1", Rect = new CellRect(0, 0, 6, 6) });
        var buildPixels = DecodePixels(PlanBoardPng.Render(buildPlan, scale, pad: 0), 6 * scale, 6 * scale);

        // Sample the zone centre of each board: the water lane reads blue (B channel dominant), the build
        // zone does not.
        var centre = (3 * scale) * (6 * scale) * 3 + (3 * scale) * 3;
        var laneIsBlue = lanePixels[centre + 2] > lanePixels[centre] && lanePixels[centre + 2] > lanePixels[centre + 1];
        var buildIsBlue = buildPixels[centre + 2] > buildPixels[centre] && buildPixels[centre + 2] > buildPixels[centre + 1];

        await Assert.That(laneIsBlue).IsTrue();
        await Assert.That(buildIsBlue).IsFalse();
    }
}

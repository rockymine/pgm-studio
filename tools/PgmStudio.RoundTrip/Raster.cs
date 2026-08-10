namespace PgmStudio.RoundTrip;

/// <summary>
/// The pixel-buffer operations every block render shares: a buffer is three bytes per pixel, row-major,
/// one pixel per block until the last step. Colour choice belongs to each render; only the mechanics live
/// here.
/// </summary>
internal static class Raster
{
    /// <summary>Writes a packed <c>0xRRGGBB</c> at a block position.</summary>
    public static void Set(byte[] pixels, int blocksWide, int col, int row, int packedRgb)
    {
        var offset = (row * blocksWide + col) * 3;
        pixels[offset] = (byte)((packedRgb >> 16) & 0xFF);
        pixels[offset + 1] = (byte)((packedRgb >> 8) & 0xFF);
        pixels[offset + 2] = (byte)(packedRgb & 0xFF);
    }

    /// <summary>Mixes a packed colour over what is already at a block position.</summary>
    public static void Over(byte[] pixels, int blocksWide, int col, int row, int packedRgb, double weight)
    {
        var offset = (row * blocksWide + col) * 3;
        pixels[offset] = Mix(pixels[offset], (packedRgb >> 16) & 0xFF, weight);
        pixels[offset + 1] = Mix(pixels[offset + 1], (packedRgb >> 8) & 0xFF, weight);
        pixels[offset + 2] = Mix(pixels[offset + 2], packedRgb & 0xFF, weight);
    }

    private static byte Mix(byte under, int over, double weight) =>
        (byte)Math.Clamp((int)(under * (1 - weight) + over * weight), 0, 255);

    /// <summary>A packed colour scaled by <paramref name="keep"/>, clamped per channel.</summary>
    public static int Scale(int packedRgb, double keep) =>
        (Math.Clamp((int)(((packedRgb >> 16) & 0xFF) * keep), 0, 255) << 16) |
        (Math.Clamp((int)(((packedRgb >> 8) & 0xFF) * keep), 0, 255) << 8) |
        Math.Clamp((int)((packedRgb & 0xFF) * keep), 0, 255);

    /// <summary>Linear interpolation between two packed colours; <paramref name="position"/> runs 0 to 1.</summary>
    public static int Lerp(int from, int to, double position)
    {
        var at = Math.Clamp(position, 0, 1);
        int red = (int)(((from >> 16) & 0xFF) + (((to >> 16) & 0xFF) - ((from >> 16) & 0xFF)) * at);
        int green = (int)(((from >> 8) & 0xFF) + (((to >> 8) & 0xFF) - ((from >> 8) & 0xFF)) * at);
        int blue = (int)((from & 0xFF) + ((to & 0xFF) - (from & 0xFF)) * at);
        return (red << 16) | (green << 8) | blue;
    }

    /// <summary>Nearest-neighbour block magnification — a block is a unit of the world, so it must stay a
    /// crisp square; interpolating would invent terrain between two columns.</summary>
    public static byte[] Upscale(byte[] pixels, int blocksWide, int blocksHigh, int scale)
    {
        if (scale <= 1) return pixels;
        var wide = blocksWide * scale;
        var scaled = new byte[wide * blocksHigh * scale * 3];
        for (var row = 0; row < blocksHigh; row++)
            for (var col = 0; col < blocksWide; col++)
            {
                var source = (row * blocksWide + col) * 3;
                for (var dy = 0; dy < scale; dy++)
                    for (var dx = 0; dx < scale; dx++)
                    {
                        var target = ((row * scale + dy) * wide + col * scale + dx) * 3;
                        scaled[target] = pixels[source];
                        scaled[target + 1] = pixels[source + 1];
                        scaled[target + 2] = pixels[source + 2];
                    }
            }
        return scaled;
    }
}

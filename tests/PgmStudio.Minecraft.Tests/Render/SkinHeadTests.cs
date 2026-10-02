using PgmStudio.Geom.Render;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Minecraft.Tests.Render;

/// <summary>The front of a player's head, read off their skin the way the game draws it.</summary>
public sealed class SkinHeadTests
{
    private static readonly byte[] Face = [200, 150, 100], Hat = [20, 40, 60];

    /// <summary>A 64×32 skin: the face filled one colour, the hat area — the block from (32, 0) to (64, 32) —
    /// another at <paramref name="hatAlpha"/>, with <paramref name="hole"/> left clear where it is given.</summary>
    private static PngImage Skin(byte hatAlpha, (int X, int Y)? hole = null)
    {
        var rgba = new byte[64 * 32 * 4];
        void Paint(int x, int y, byte[] colour, byte alpha)
        {
            var at = ((y * 64) + x) * 4;
            colour.CopyTo(rgba, at);
            rgba[at + 3] = alpha;
        }
        for (var y = 8; y < 16; y++)
            for (var x = 8; x < 16; x++) Paint(x, y, Face, 255);
        for (var y = 0; y < 32; y++)
            for (var x = 32; x < 64; x++) Paint(x, y, Hat, hatAlpha);
        if (hole is { } clear) Paint(clear.X, clear.Y, Hat, 0);
        return new PngImage(64, 32, rgba);
    }

    private static byte[] Pixel(byte[] front, int x, int y) => front[(((y * SkinHead.Size) + x) * 3)..((((y * SkinHead.Size) + x) * 3) + 3)];

    [Test]
    public async Task A_hat_area_opaque_everywhere_is_dropped_and_the_face_shows()
    {
        var front = SkinHead.Front(Skin(255))!;

        for (var y = 0; y < SkinHead.Size; y++)
            for (var x = 0; x < SkinHead.Size; x++)
                await Assert.That(Pixel(front, x, y)).IsEquivalentTo(Face);
    }

    [Test]
    public async Task A_hat_area_clear_somewhere_is_drawn_over_the_face()
    {
        var front = SkinHead.Front(Skin(255, hole: (33, 1)))!;

        await Assert.That(Pixel(front, 0, 0)).IsEquivalentTo(Hat);
        await Assert.That(Pixel(front, 7, 7)).IsEquivalentTo(Hat);
    }

    [Test]
    public async Task A_clear_hat_leaves_the_face()
    {
        var front = SkinHead.Front(Skin(0))!;

        await Assert.That(Pixel(front, 3, 4)).IsEquivalentTo(Face);
    }

    [Test]
    public async Task An_image_smaller_than_a_skin_has_no_head()
    {
        await Assert.That(SkinHead.Front(new PngImage(32, 32, new byte[32 * 32 * 4]))).IsNull();
    }
}

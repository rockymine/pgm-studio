using PgmStudio.Geom.Render;

namespace PgmStudio.Minecraft.Render;

/// <summary>
/// A player's head as the game draws it from the front: the 8×8 face at (8, 8) of their skin with the hat at
/// (40, 8) laid over it. The hat counts only where it is see-through somewhere: the game drops a skin's hat area
/// — the block from (32, 0) to (64, 32) — when every pixel in it is opaque, which is how a skin drawn before the
/// hat layer existed, filled black there, still shows its face.
/// </summary>
public static class SkinHead
{
    /// <summary>The head's edge in pixels.</summary>
    public const int Size = 8;

    /// <summary>The front of the head as opaque RGB, <see cref="Size"/> pixels a side; null where
    /// <paramref name="skin"/> is not a skin — narrower than 64 or shorter than 32.</summary>
    public static byte[]? Front(PngImage skin)
    {
        if (skin.Width < 64 || skin.Height < 32) return null;
        var hat = !Opaque(skin, 32, 0, 64, 32);
        var rgb = new byte[Size * Size * 3];
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var face = (((Size + y) * skin.Width) + Size + x) * 4;
                var over = (((Size + y) * skin.Width) + 40 + x) * 4;
                var alpha = hat ? skin.Rgba[over + 3] / 255.0 : 0;
                for (var channel = 0; channel < 3; channel++)
                    rgb[((y * Size) + x) * 3 + channel] = (byte)Math.Round(
                        (skin.Rgba[over + channel] * alpha) + (skin.Rgba[face + channel] * (1 - alpha)));
            }
        return rgb;
    }

    /// <summary>Whether every pixel from (<paramref name="left"/>, <paramref name="top"/>) up to
    /// (<paramref name="right"/>, <paramref name="bottom"/>) is at least half opaque, the line the game draws.</summary>
    private static bool Opaque(PngImage skin, int left, int top, int right, int bottom)
    {
        for (var y = top; y < bottom; y++)
            for (var x = left; x < right; x++)
                if (skin.Rgba[((y * skin.Width) + x) * 4 + 3] < 128) return false;
        return true;
    }
}

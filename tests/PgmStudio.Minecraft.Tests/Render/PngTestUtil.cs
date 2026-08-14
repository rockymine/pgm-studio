namespace PgmStudio.Minecraft.Tests.Render;

/// <summary>Width/height straight out of a PNG's IHDR chunk (bytes 16..23 of any valid PNG), shared by every
/// render test that checks a legend strip actually grew the written file — no image-decoding dependency
/// needed just to read two big-endian integers.</summary>
internal static class PngTestUtil
{
    public static (int Width, int Height) Dimensions(byte[] png)
    {
        int width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        int height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        return (width, height);
    }
}

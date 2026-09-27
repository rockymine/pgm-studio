using System.IO.Compression;
using PgmStudio.Geom.Render;

namespace PgmStudio.Minecraft.Render;

/// <summary>One block sprite: a square of <paramref name="Size"/> pixels a side, RGBA, the first frame of an
/// animated strip.</summary>
public sealed record BlockSprite(int Size, byte[] Rgba)
{
    /// <summary>The sprite's texel at <paramref name="u"/>, <paramref name="v"/> in <c>[0, 1)</c>, packed
    /// <c>0xAARRGGBB</c>.</summary>
    public uint At(double u, double v)
    {
        var column = Math.Clamp((int)(u * Size), 0, Size - 1);
        var row = Math.Clamp((int)(v * Size), 0, Size - 1);
        var at = (row * Size + column) * 4;
        return ((uint)Rgba[at + 3] << 24) | ((uint)Rgba[at] << 16) | ((uint)Rgba[at + 1] << 8) | Rgba[at + 2];
    }
}

/// <summary>
/// The block sprites of one texture pack, by the name the pack files each under — <c>stone</c>,
/// <c>grass_top</c>, <c>log_oak</c>.
///
/// <para>They are read out of the 1.8.9 client jar, which is Mojang's and is never in this repository or in
/// anything it publishes: the server fetches the jar from Mojang by the hash Mojang's own metadata declares,
/// or reads one the operator already has, and keeps the sprites in memory. What the studio serves is a
/// picture drawn with them, never the sprites.</para>
/// </summary>
public sealed class BlockTextureSet
{
    /// <summary>Where a jar keeps its block sprites.</summary>
    public const string JarFolder = "assets/minecraft/textures/blocks/";

    private readonly Dictionary<string, BlockSprite> _sprites;

    private BlockTextureSet(Dictionary<string, BlockSprite> sprites) => _sprites = sprites;

    /// <summary>How many sprites the set holds.</summary>
    public int Count => _sprites.Count;

    /// <summary>The sprite filed under <paramref name="name"/>, or null for one the pack does not hold.</summary>
    public BlockSprite? Get(string name) => _sprites.GetValueOrDefault(name);

    /// <summary>Every block sprite a client jar holds. An animated strip keeps its first frame.</summary>
    public static BlockTextureSet FromJar(Stream jar)
    {
        using var archive = new ZipArchive(jar, ZipArchiveMode.Read, leaveOpen: true);
        var sprites = new Dictionary<string, BlockSprite>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.StartsWith(JarFolder, StringComparison.Ordinal)
                || !entry.FullName.EndsWith(".png", StringComparison.Ordinal)) continue;
            var name = entry.FullName[JarFolder.Length..^4];
            if (name.Contains('/')) continue;
            using var stream = entry.Open();
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            sprites[name] = FirstFrame(PngReader.Decode(bytes.ToArray()));
        }
        return new BlockTextureSet(sprites);
    }

    /// <summary>A set built from sprites already in hand — what a test draws with, since the real ones are
    /// never in the repository.</summary>
    public static BlockTextureSet Of(IReadOnlyDictionary<string, BlockSprite> sprites) =>
        new(new Dictionary<string, BlockSprite>(sprites, StringComparer.Ordinal));

    private static BlockSprite FirstFrame(PngImage image)
    {
        var size = Math.Min(image.Width, image.Height);
        var rgba = new byte[size * size * 4];
        for (var row = 0; row < size; row++)
            Array.Copy(image.Rgba, row * image.Width * 4, rgba, row * size * 4, size * 4);
        return new BlockSprite(size, rgba);
    }
}

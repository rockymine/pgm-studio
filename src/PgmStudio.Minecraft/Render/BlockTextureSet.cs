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
/// <para>A chest is drawn from an entity texture rather than a block sprite, so its three faces are cut out
/// of <c>entity/chest/normal</c>, <c>trapped</c> and <c>ender</c> and filed as <c>chest_&lt;kind&gt;_top</c>,
/// <c>_side</c> and <c>_front</c>: sixteen-texel sprites with the face placed where the chest's box stands in
/// its cell, the latch painted onto the front.</para>
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

    /// <summary>Where a jar keeps the textures a chest is drawn from.</summary>
    public const string ChestFolder = "assets/minecraft/textures/entity/chest/";

    private static readonly string[] ChestKinds = ["normal", "trapped", "ender"];

    /// <summary>How wide a chest texture is at the game's own resolution, in texels.</summary>
    private const int ChestTextureWidth = 64;

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
            if (entry.FullName.StartsWith(ChestFolder, StringComparison.Ordinal)
                && ChestKinds.FirstOrDefault(kind => entry.FullName == $"{ChestFolder}{kind}.png") is { } chest)
            {
                foreach (var (face, sprite) in ChestFaces(PngReader.Decode(Bytes(entry))))
                    sprites[$"chest_{chest}_{face}"] = sprite;
                continue;
            }
            if (!entry.FullName.StartsWith(JarFolder, StringComparison.Ordinal)
                || !entry.FullName.EndsWith(".png", StringComparison.Ordinal)) continue;
            var name = entry.FullName[JarFolder.Length..^4];
            if (name.Contains('/')) continue;
            sprites[name] = FirstFrame(PngReader.Decode(Bytes(entry)));
        }
        return new BlockTextureSet(sprites);
    }

    private static byte[] Bytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    /// <summary>
    /// A chest's top, side and front, cut out of its entity texture, or none for a texture not laid out the
    /// way the game's is.
    ///
    /// <para>The texture unwraps the lid, a 14 × 5 × 14 box, from its top-left corner and the base, 14 × 10 ×
    /// 14, from 19 texels down, each with its top beside its bottom in the first row and its four sides
    /// under them; the latch's front is the two-by-four at (1, 1). The chest stands a sixteenth in from each
    /// side of its cell and fourteen sixteenths tall, so its side sprite is the lid's five rows over the
    /// base's lowest nine — the lid covers the base's top row — from the second row of the sprite down, and
    /// its top is the lid's top one texel in from each edge. Each face is drawn to the sprite's edges past
    /// that, so a ray at the rim of the box meets the rim of the face.</para>
    /// </summary>
    private static IEnumerable<(string Face, BlockSprite Sprite)> ChestFaces(PngImage texture)
    {
        var scale = texture.Width / ChestTextureWidth;
        if (scale == 0 || texture.Width % ChestTextureWidth != 0 || texture.Height < texture.Width) yield break;

        (int U, int V) Side(int column, int row, int from)
        {
            var across = Math.Clamp(column, scale, 15 * scale - 1) - scale;
            var down = Math.Clamp(row, 2 * scale, 16 * scale - 1);
            return down < 7 * scale
                ? (from + across, 14 * scale + down - 2 * scale)
                : (from + across, 34 * scale + down - 7 * scale);
        }

        yield return ("top", Cut(texture, scale, (column, row) =>
            (14 * scale + Math.Clamp(column, scale, 15 * scale - 1) - scale,
             Math.Clamp(row, scale, 15 * scale - 1) - scale)));
        yield return ("side", Cut(texture, scale, (column, row) => Side(column, row, 0)));
        yield return ("front", Cut(texture, scale, (column, row) =>
            column >= 7 * scale && column < 9 * scale && row >= 5 * scale && row < 9 * scale
                ? (scale + column - 7 * scale, scale + row - 5 * scale)
                : Side(column, row, 14 * scale)));
    }

    /// <summary>A sixteen-texel sprite at <paramref name="scale"/>, each texel taken from where
    /// <paramref name="source"/> says in <paramref name="texture"/>.</summary>
    private static BlockSprite Cut(PngImage texture, int scale, Func<int, int, (int U, int V)> source)
    {
        var size = 16 * scale;
        var rgba = new byte[size * size * 4];
        for (var row = 0; row < size; row++)
            for (var column = 0; column < size; column++)
            {
                var (u, v) = source(column, row);
                Array.Copy(texture.Rgba, (v * texture.Width + u) * 4, rgba, (row * size + column) * 4, 4);
            }
        return new BlockSprite(size, rgba);
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

using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Render;

/// <summary>
/// The sprite a block shows on a face, tinted the way the game tints it — every picture drawn with the game's
/// own textures reads its sprites here. Each sprite is tinted once per colour, and in flat mode reduced to its
/// mean. Not safe to share across threads; a picture holds its own.
/// </summary>
public sealed class BlockSprites(BlockTextureSet textures, bool flat = false)
{
    /// <summary>The colour a block's sprites are multiplied by in <paramref name="biome"/> at
    /// <paramref name="x"/>, <paramref name="z"/>: the face's own fixed tint, else the biome's for the block's
    /// tint channel, else none.</summary>
    public static uint Tint(BlockFaces? faces, int id, int data, byte biome, int x, int z)
    {
        if (faces?.Tint is { } fixedTint) return fixedTint;
        var channel = BlockTints.Of(id, data);
        return channel == TintChannel.None ? 0xFFFFFF : BiomeTint.Of(biome, channel, x, z);
    }

    /// <summary>The sprite <paramref name="id"/>:<paramref name="data"/> shows from above
    /// (<paramref name="top"/>) or from the side, tinted for <paramref name="biome"/>; null for a block the
    /// pack has no sprite for or that draws nothing.</summary>
    public BlockSprite? Face(int id, int data, bool top, byte biome, int x, int z)
    {
        if (BlockFaces.Of(id, data) is not { Form: not FaceForm.Hidden } faces) return null;
        var tint = Tint(faces, id, data, biome, x, z);
        if (top || faces.Form == FaceForm.Cross) return Get(faces.Top, tint);
        return Get(faces.Side, faces.SideOverlay is null ? tint : 0xFFFFFF, faces.SideOverlay, tint)
               ?? Get(faces.Top, tint);
    }

    private readonly Dictionary<(string Name, uint Tint, string? Overlay, uint OverlayTint), BlockSprite?> _made = [];

    public BlockSprite? Get(string name, uint tint, string? overlay = null, uint overlayTint = 0xFFFFFF)
    {
        if (_made.TryGetValue((name, tint, overlay, overlayTint), out var made)) return made;
        BlockSprite? sprite = null;
        if (textures.Get(name) is { } source)
        {
            var rgba = Tinted(source.Rgba, tint);
            if (overlay is not null && textures.Get(overlay) is { Size: var size } mask && size == source.Size)
                Lay(rgba, Tinted(mask.Rgba, overlayTint));
            sprite = new BlockSprite(source.Size, flat ? Mean(rgba) : rgba);
        }
        return _made[(name, tint, overlay, overlayTint)] = sprite;
    }

    public BlockSprite Solid(uint rgb) => new(1, [(byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255]);

    private static byte[] Tinted(byte[] rgba, uint tint)
    {
        var copy = (byte[])rgba.Clone();
        if (tint == 0xFFFFFF) return copy;
        int red = (int)(tint >> 16) & 0xFF, green = (int)(tint >> 8) & 0xFF, blue = (int)tint & 0xFF;
        for (var i = 0; i < copy.Length; i += 4)
        {
            copy[i] = (byte)(copy[i] * red / 255);
            copy[i + 1] = (byte)(copy[i + 1] * green / 255);
            copy[i + 2] = (byte)(copy[i + 2] * blue / 255);
        }
        return copy;
    }

    private static void Lay(byte[] under, byte[] over)
    {
        for (var i = 0; i < under.Length; i += 4)
        {
            var weight = over[i + 3] / 255.0;
            for (var channel = 0; channel < 3; channel++)
                under[i + channel] = (byte)(under[i + channel] * (1 - weight) + over[i + channel] * weight);
        }
    }

    /// <summary>Every opaque texel replaced by the mean of the opaque ones; transparency is kept, so a leaf
    /// still lets the ray through where it did.</summary>
    private static byte[] Mean(byte[] rgba)
    {
        long red = 0, green = 0, blue = 0, count = 0;
        for (var i = 0; i < rgba.Length; i += 4)
        {
            if (rgba[i + 3] < 128) continue;
            red += rgba[i]; green += rgba[i + 1]; blue += rgba[i + 2]; count++;
        }
        if (count == 0) return rgba;
        var flatRgba = (byte[])rgba.Clone();
        for (var i = 0; i < flatRgba.Length; i += 4)
        {
            if (flatRgba[i + 3] < 128) continue;
            flatRgba[i] = (byte)(red / count); flatRgba[i + 1] = (byte)(green / count); flatRgba[i + 2] = (byte)(blue / count);
        }
        return flatRgba;
    }
}

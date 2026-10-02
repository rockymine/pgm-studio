using PgmStudio.Geom.Render;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Minecraft.Views;

/// <summary>One block of a preview grid, and the biome that tints it where the picture states one.</summary>
public readonly record struct GridBlock(int Id, int Data, byte? Biome = null);

/// <summary>
/// A preview picture as the blocks it shows: a grid of cells, each a block or nothing. It is drawn either as
/// each block's palette colour (<see cref="Colours"/>, the <see cref="CellRaster"/> every card and PNG read
/// draws) or with each block's own sprite face (<see cref="SpritePng"/>), so the two are one derivation.
/// </summary>
public sealed record BlockGrid(int Columns, int Rows, Func<int, int, GridBlock?> BlockAt)
{
    /// <summary>The grid in palette colours, <paramref name="cell"/> pixels a block.</summary>
    public CellRaster Colours(int cell) => new(Columns, Rows, cell, (x, y) => BlockAt(x, y) is { } block
        ? block.Biome is { } biome ? BlockPalette.Hex(block.Id, block.Data, biome, x, y) : BlockPalette.Hex(block.Id, block.Data)
        : null);

    /// <summary>
    /// The grid drawn with the game's sprites, <paramref name="cell"/> pixels a block, as a PNG: the face seen
    /// from above where <paramref name="top"/>, else the face seen from the side. A sprite is box-filtered down
    /// to the cell, so a block keeps its texture's average colour at any size; a block with no sprite takes its
    /// palette colour. A cell with no block is left transparent, as the colour view leaves it unpainted, and a
    /// see-through texel shows <see cref="CellRaster.Background"/>.
    /// </summary>
    public byte[] SpritePng(BlockTextureSet textures, bool top, int cell)
        => PngWriter.EncodeRgba(Columns * cell, Rows * cell, SpritePixels(textures, top, cell));

    /// <summary>The same picture as raw RGBA rows, <see cref="SpritePng"/> before it is encoded.</summary>
    public byte[] SpritePixels(BlockTextureSet textures, bool top, int cell)
    {
        var sprites = new BlockSprites(textures);
        int width = Columns * cell, height = Rows * cell;
        var rgba = new byte[width * height * 4];
        var back = Unpack(Convert.ToUInt32(CellRaster.Background[1..], 16));

        for (var row = 0; row < Rows; row++)
        for (var column = 0; column < Columns; column++)
        {
            var block = BlockAt(column, row);
            var sprite = block is { } shown
                ? sprites.Face(shown.Id, shown.Data, top, shown.Biome ?? Biome.Plains, column, row)
                : null;
            var flat = block is { } painted && sprite is null
                ? Unpack((uint)BlockPalette.PackedRgb(painted.Id, painted.Data))
                : back;

            for (var py = 0; py < cell; py++)
            for (var px = 0; px < cell; px++)
            {
                var (red, green, blue) = sprite is null ? flat : Filtered(sprite, px, py, cell, back);
                var at = ((row * cell + py) * width + column * cell + px) * 4;
                rgba[at] = red; rgba[at + 1] = green; rgba[at + 2] = blue;
                rgba[at + 3] = block is null ? (byte)0 : (byte)255;
            }
        }
        return rgba;
    }

    /// <summary>The mean of the texels pixel <paramref name="px"/>, <paramref name="py"/> of a
    /// <paramref name="cell"/>-pixel block covers, each blended over <paramref name="back"/> by its alpha.</summary>
    private static (byte, byte, byte) Filtered(BlockSprite sprite, int px, int py, int cell, (byte R, byte G, byte B) back)
    {
        int from = px * sprite.Size / cell, to = Math.Max(from + 1, (px + 1) * sprite.Size / cell);
        int fromRow = py * sprite.Size / cell, toRow = Math.Max(fromRow + 1, (py + 1) * sprite.Size / cell);
        double red = 0, green = 0, blue = 0;
        var count = 0;
        for (var ty = fromRow; ty < toRow; ty++)
        for (var tx = from; tx < to; tx++)
        {
            var at = (ty * sprite.Size + tx) * 4;
            var alpha = sprite.Rgba[at + 3] / 255.0;
            red += sprite.Rgba[at] * alpha + back.R * (1 - alpha);
            green += sprite.Rgba[at + 1] * alpha + back.G * (1 - alpha);
            blue += sprite.Rgba[at + 2] * alpha + back.B * (1 - alpha);
            count++;
        }
        return ((byte)(red / count), (byte)(green / count), (byte)(blue / count));
    }

    private static (byte R, byte G, byte B) Unpack(uint rgb) => ((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
}

using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Render;
using PgmStudio.Minecraft.Views;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// A preview grid drawn two ways from one derivation: its palette colours are the colours a block has, and
/// its sprite picture puts each block's own sprite face in its cell — the top from above, the side from the
/// side — filtered down to the cell, with a block the pack has no sprite for drawn in its palette colour.
/// </summary>
public sealed class BlockGridTests
{
    private const int Stone = 1, Dirt = 3, Log = 17, Cell = 4;

    private static BlockTextureSet Sprites() => BlockTextureSet.Of(new Dictionary<string, BlockSprite>
    {
        ["stone"] = Uniform(100, 100, 100),
        ["log_oak_top"] = Uniform(200, 160, 100),
        ["log_oak"] = Uniform(60, 40, 20),
        ["dirt"] = Checker(),
    });

    private static BlockSprite Uniform(byte red, byte green, byte blue) =>
        new(2, [red, green, blue, 255, red, green, blue, 255, red, green, blue, 255, red, green, blue, 255]);

    /// <summary>Black and white texels, alternating, sixteen a side: a sprite whose mean is grey.</summary>
    private static BlockSprite Checker()
    {
        var rgba = new byte[16 * 16 * 4];
        for (var i = 0; i < 16 * 16; i++)
        {
            var white = ((i % 16) + (i / 16)) % 2 == 0 ? (byte)255 : (byte)0;
            rgba[i * 4] = white; rgba[i * 4 + 1] = white; rgba[i * 4 + 2] = white; rgba[i * 4 + 3] = 255;
        }
        return new BlockSprite(16, rgba);
    }

    private static (byte R, byte G, byte B) Pixel(byte[] rgba, int width, int x, int y)
    {
        var at = (y * width + x) * 4;
        return (rgba[at], rgba[at + 1], rgba[at + 2]);
    }

    private static byte Alpha(byte[] rgba, int width, int x, int y) => rgba[(y * width + x) * 4 + 3];

    [Test]
    public async Task Each_cell_shows_its_block_s_sprite_face_seen_from_where_the_picture_looks()
    {
        var grid = new BlockGrid(2, 1, (x, _) => new GridBlock(x == 0 ? Stone : Log, 0));

        var above = grid.SpritePixels(Sprites(), top: true, Cell);
        var side = grid.SpritePixels(Sprites(), top: false, Cell);

        await Assert.That(Pixel(above, 2 * Cell, 1, 1)).IsEqualTo(((byte)100, (byte)100, (byte)100));
        await Assert.That(Pixel(above, 2 * Cell, Cell + 1, 1)).IsEqualTo(((byte)200, (byte)160, (byte)100));
        await Assert.That(Pixel(side, 2 * Cell, Cell + 1, 1)).IsEqualTo(((byte)60, (byte)40, (byte)20));
    }

    [Test]
    public async Task A_sprite_shrunk_to_a_cell_keeps_its_mean_colour()
    {
        var grid = new BlockGrid(1, 1, (_, _) => new GridBlock(Dirt, 0));
        var rgb = grid.SpritePixels(Sprites(), top: true, Cell);

        for (var y = 0; y < Cell; y++)
            for (var x = 0; x < Cell; x++)
                await Assert.That(Pixel(rgb, Cell, x, y).R).IsBetween((byte)120, (byte)135);
    }

    [Test]
    public async Task A_block_without_a_sprite_is_drawn_in_its_palette_colour_and_no_block_is_left_transparent()
    {
        const int Gold = 41;
        var grid = new BlockGrid(2, 1, (x, _) => x == 0 ? new GridBlock(Gold, 0) : null);
        var rgb = grid.SpritePixels(Sprites(), top: true, Cell);

        var gold = BlockPalette.PackedRgb(Gold, 0);
        await Assert.That(Pixel(rgb, 2 * Cell, 0, 0))
            .IsEqualTo(((byte)(gold >> 16), (byte)(gold >> 8), (byte)gold));
        await Assert.That(Alpha(rgb, 2 * Cell, 0, 0)).IsEqualTo((byte)255);
        await Assert.That(Alpha(rgb, 2 * Cell, Cell, 0)).IsEqualTo((byte)0);
    }

    [Test]
    public async Task The_colour_view_is_the_palette_colour_of_each_block()
    {
        var grid = new BlockGrid(2, 1, (x, _) => new GridBlock(x == 0 ? Stone : Dirt, 0));
        var raster = grid.Colours(Cell);

        await Assert.That(raster.ColorAt(0, 0)).IsEqualTo(BlockPalette.Hex(Stone, 0));
        await Assert.That(raster.ColorAt(1, 0)).IsEqualTo(BlockPalette.Hex(Dirt, 0));
    }
}

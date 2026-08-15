using PgmStudio.Minecraft.Palette;
namespace PgmStudio.Minecraft.Views;

/// <summary>One rectangle of the unit cube a block fills, measured from the cell's top-left in cell widths.
/// <see cref="SeeThrough"/> marks glass, which is drawn but not drawn over.</summary>
public readonly record struct BlockPiece(double Left, double Top, double Wide, double Tall, bool SeeThrough);

/// <summary>
/// What fraction of its cube a block actually fills — a slab one half, a stair a half plus a quarter on the
/// side it climbs toward, a ladder two rails and a rung, everything else the whole thing.
///
/// <para>This is block knowledge rather than drawing knowledge, which is why it sits beside
/// <see cref="BlockVariants"/> rather than inside a renderer: in the legacy format a block's metadata
/// <em>is</em> its geometry — which way a stair climbs, which half a slab occupies — and reading that is the
/// same act whatever the reader means to do with it.</para>
///
/// <para>It exists because a renderer that draws every block as a cube draws a stair lattice as a solid 2×2
/// patch of wall, which is exactly the picture that window is designed not to be.</para>
/// </summary>
public static class BlockShapes
{
    private const int OakStairs = 53, CobblestoneStairs = 67;

    private static readonly int[] Stairs =
        [OakStairs, CobblestoneStairs, 108, 109, 114, 128, 134, 135, 136, 163, 164, 180];

    private static readonly int[] Slabs = [44, 126, 182];

    /// <summary>The pieces a block draws as, in paint order.</summary>
    public static IEnumerable<BlockPiece> Of(int id, int data)
    {
        if (Stairs.Contains(id))
        {
            var upsideDown = (data & Blocks.StairUpsideDown) != 0;
            // The raised half is on the side the stair climbs toward: east is +x, and x runs rightward in
            // every view that asks for this.
            var rightward = (data & 3) is Blocks.StairEast or Blocks.StairSouth;
            yield return new BlockPiece(0, upsideDown ? 0 : 0.5, 1, 0.5, false);
            yield return new BlockPiece(rightward ? 0.5 : 0, upsideDown ? 0.5 : 0, 0.5, 0.5, false);
            yield break;
        }
        if (Slabs.Contains(id))
        {
            yield return new BlockPiece(0, (data & Blocks.SlabUpperHalf) != 0 ? 0 : 0.5, 1, 0.5, false);
            yield break;
        }
        if (id is Blocks.GlassPane or Blocks.StainedGlassPane or Blocks.Glass or Blocks.StainedGlass)
        {
            yield return new BlockPiece(0, 0, 1, 1, true);
            yield break;
        }
        // Two rails and a rung, because a ladder drawn as a cube is a wall — and a cutaway whose whole subject
        // is the way up between two storeys cannot afford to draw the way up as the thing blocking it.
        if (id == Blocks.Ladder)
        {
            yield return new BlockPiece(0.14, 0, 0.14, 1, false);
            yield return new BlockPiece(0.72, 0, 0.14, 1, false);
            yield return new BlockPiece(0.14, 0.4, 0.72, 0.2, false);
            yield break;
        }
        yield return new BlockPiece(0, 0, 1, 1, false);
    }
}

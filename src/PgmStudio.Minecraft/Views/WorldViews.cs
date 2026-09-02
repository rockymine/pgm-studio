using System.Globalization;
using System.Text;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Views;

/// <summary>
/// The three ways a stamped <see cref="VoxelWorld"/> is drawn flat: <b>plan</b> for what its roof does,
/// <b>section</b> for the course stack behind its front wall, and <b>elevation</b> for one wall at the scale
/// of the pieces in it. What a building <em>looks like</em> is drawn in 3-D from the world itself
/// (<see cref="PgmStudio.Minecraft.Anvil.WorldColumns"/>), not flattened to a picture here.
///
/// <para>They live here, below every consumer, because there are two of those and they must not drift: the
/// studio's library cards and the house-generation showcase draw the same buildings, and a picture the studio
/// gets right and the showcase gets wrong (or the reverse) is worse than either being wrong alone. The
/// pictures are also the one check on the stamper that a block count cannot make — a gap in a roof is
/// invisible in a total and obvious standing under it.</para>
///
/// <para>Nothing here reads a style. A view takes a world and a box, so it draws whatever was stamped and
/// cannot promise a building the export would not put down.</para>
/// </summary>
public static class WorldViews
{
    // ── the flat reads ────────────────────────────────────────────────────────────────────────────────
    /// <summary>The highest block of each column: what the roof does, its hole and whether its eave oversails
    /// the walls, and nothing else.</summary>
    public static string Plan(VoxelWorld world, BlockBox box, int cell = 9)
        => PlanRaster(world, box, cell).Svg();

    /// <summary>The same picture before an encoding is chosen, so the SVG a card shows and the PNG an agent
    /// saves are one derivation rather than two renders that agree today.</summary>
    public static CellRaster PlanRaster(VoxelWorld world, BlockBox box, int cell = 9)
        => new(box.Width, box.Depth, cell, (column, row) =>
        {
            for (var y = box.MaxY; y >= box.MinY; y--)
            {
                var (id, data) = world.GetBlock(box.MinX + column, y, box.MinZ + row);
                if (id != Blocks.Air) return BlockPalette.Hex(id, data);
            }
            return null;
        });

    /// <summary>One horizontal slice seen from above — the read a roof would otherwise hide, which is how a
    /// zoned floor is looked at.</summary>
    public static string Slice(VoxelWorld world, BlockBox box, int y, int cell = 9)
        => SvgRaster.Raster(box.Width, box.Depth, cell, (column, row) =>
        {
            var (id, data) = world.GetBlock(box.MinX + column, y, box.MinZ + row);
            return id == Blocks.Air ? null : BlockPalette.Hex(id, data);
        });

    /// <summary>The building projected onto its front, nearest block first and shaded by how far back it
    /// stands — so a doorway reads as an opening rather than as a hole in the picture. A projection rather
    /// than a cut, so a door on the near wall does not hide the wall behind it.</summary>
    public static string Section(VoxelWorld world, BlockBox box, int cell = 9)
        => SectionRaster(world, box, cell).Svg();

    /// <summary>The section before an encoding is chosen — see <see cref="PlanRaster"/>.</summary>
    public static CellRaster SectionRaster(VoxelWorld world, BlockBox box, int cell = 9)
    {
        var view = BlockSideView.Project(world, box.MinX, box.MaxX, box.MinZ, box.MaxZ, box.MinY, box.MaxY);
        return new CellRaster(view.Columns, box.Height, cell, (column, row) =>
            view.At(view.FromX + column, box.MaxY - row) is { } block
                ? BlockPalette.Hex(block.Id, block.Data, block.Depth)
                : null);
    }

    // ── the elevation ─────────────────────────────────────────────────────────────────────────────────
    /// <summary>One plane of the world drawn at the scale of the pieces in it, and the only view that draws a
    /// block as its own shape rather than as a cube. It has to: a stair lattice's whole trick is the quarter
    /// each of its four stairs is missing, and a renderer that draws every block as a cube shows that window
    /// as a solid 2×2 patch — exactly the picture the window is designed not to be. The shapes come from
    /// <see cref="BlockShapes"/>, which reads the block's own metadata, so the drawing follows the world
    /// rather than a second opinion about it.
    ///
    /// <para>Drawn on the plane <paramref name="z"/>, which makes it a cutaway when the plane is inside the
    /// building — the only view that shows a slab, the clear under it and the way through it at once.</para></summary>
    public static string Elevation(VoxelWorld world, BlockBox box, int z, int cell = 22)
    {
        var svg = new StringBuilder();
        int columns = box.Width, rows = box.Height;
        svg.Append($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {columns * cell} {rows * cell}' ")
           .Append($"width='{columns * cell}' height='{rows * cell}' shape-rendering='crispEdges' role='img'>");

        for (var y = box.MaxY; y >= box.MinY; y--)
            for (var x = box.MinX; x <= box.MaxX; x++)
            {
                var (id, data) = world.GetBlock(x, y, z);
                if (id == Blocks.Air) continue;
                int column = x - box.MinX, row = box.MaxY - y;
                var fill = BlockPalette.Hex(id, data);
                foreach (var piece in BlockShapes.Of(id, data))
                    svg.Append(string.Create(CultureInfo.InvariantCulture,
                        $"<rect x='{(column + piece.Left) * cell:0.##}' y='{(row + piece.Top) * cell:0.##}' " +
                        $"width='{piece.Wide * cell:0.##}' height='{piece.Tall * cell:0.##}' fill='{fill}'") +
                        (piece.SeeThrough ? " opacity='0.55'/>" : "/>"));
            }

        svg.Append("</svg>");
        return svg.ToString();
    }
}

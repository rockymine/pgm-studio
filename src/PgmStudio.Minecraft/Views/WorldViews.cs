using System.Globalization;
using System.Text;
using PgmStudio.Geom;

namespace PgmStudio.Minecraft.Views;

/// <summary>
/// The four ways a stamped <see cref="VoxelWorld"/> is drawn: <b>isometric</b> for what a building looks like,
/// <b>plan</b> for what its roof does, <b>section</b> for the course stack behind its front wall, and
/// <b>elevation</b> for one wall at the scale of the pieces in it.
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
    // ── the isometric ─────────────────────────────────────────────────────────────────────────────────
    /// <summary>A block world seen from above one corner. The camera stands off the box's <b>−z</b> side, which
    /// is the wall a house fronts on — its door, its porch and most of its windows are there, and a view of the
    /// back of a house is a view of a box.
    ///
    /// <para>The view direction is (−1, −1, +1), so a cell is nearer the camera the larger x + y − z is, which
    /// is the paint order — and each of the three faces that can face the camera is drawn only when the
    /// neighbour it points at is air. <b>Face</b> culling rather than block culling is what keeps the payload
    /// small: a wall shows one face per block rather than three, and a building's interior draws nothing at
    /// all.</para>
    ///
    /// <para>Consecutive faces of one colour then coalesce into a single path, because a house is a few
    /// materials repeated thousands of times and a per-face fill attribute is most of the bytes. Consecutive
    /// rather than all-of-one-colour: a nearer face still has to paint over a farther one — an eave over the
    /// wall behind it — so the depth order is what the emission follows and the colour run is only how far it
    /// can be carried.</para>
    ///
    /// <para>Scale is even and every projected coordinate a multiple of half a tile, so the whole picture is
    /// integer arithmetic and no coordinate carries a decimal point.</para></summary>
    public static string Isometric(VoxelWorld world, BlockBox box, int scale = 10)
    {
        int halfWide = Math.Max(2, scale), halfTall = halfWide / 2, side = halfWide;   // a 2:1 tile, one cell tall

        int ScreenX(int x, int z) => (x - z) * halfWide;
        int ScreenY(int x, int y, int z) => (x + z) * halfTall - y * side;

        // The drawn extent, so the viewBox fits the building rather than the block box it was read out of.
        int left = (box.MinX - box.MaxZ) * halfWide - halfWide, right = (box.MaxX - box.MinZ) * halfWide + halfWide;
        int top = ScreenY(box.MinX, box.MaxY, box.MinZ);
        int bottom = ScreenY(box.MaxX, box.MinY, box.MaxZ) + 2 * halfTall + side;

        var faces = new List<(int Depth, string Color, string Path)>();
        for (var x = box.MinX; x <= box.MaxX; x++)
            for (var z = box.MinZ; z <= box.MaxZ; z++)
                for (var y = box.MinY; y <= box.MaxY; y++)
                {
                    var (id, data) = Block(x, y, z);
                    if (id == Blocks.Air) continue;
                    var rgb = BlockPalette.Color(id, data);
                    int px = ScreenX(x, z) - left, py = ScreenY(x, y, z) - top;

                    // Three faces, three shades: the top full, the +z face three-quarters, the +x face half.
                    // Depth in a flat picture has to come from the light, and a single fill turns a roof into
                    // a silhouette.
                    if (!Opaque(x, y + 1, z))
                        Face(x + y + z, rgb, 1.0,
                            px, py, px + halfWide, py + halfTall, px, py + 2 * halfTall, px - halfWide, py + halfTall);
                    if (!Opaque(x, y, z + 1))
                        Face(x + y + z, rgb, 0.74,
                            px - halfWide, py + halfTall, px, py + 2 * halfTall,
                            px, py + 2 * halfTall + side, px - halfWide, py + halfTall + side);
                    if (!Opaque(x + 1, y, z))
                        Face(x + y + z, rgb, 0.52,
                            px, py + 2 * halfTall, px + halfWide, py + halfTall,
                            px + halfWide, py + halfTall + side, px, py + 2 * halfTall + side);
                }

        // Nearer last, and stably, so the three faces of one block keep the order they were built in.
        var ordered = faces.OrderBy(face => face.Depth).ToList();

        var svg = new StringBuilder();
        int width = right - left, height = bottom - top;
        svg.Append($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {width} {height}' ")
           .Append($"width='{width}' height='{height}' role='img'>");
        for (var at = 0; at < ordered.Count;)
        {
            var run = new StringBuilder(ordered[at].Path);
            var color = ordered[at].Color;
            var next = at + 1;
            while (next < ordered.Count && ordered[next].Color == color) run.Append(ordered[next++].Path);
            svg.Append($"<path fill='{color}' d='{run}'/>");
            at = next;
        }
        svg.Append("</svg>");
        return svg.ToString();

        // The one mirror: a drawn cell at z reads the world at its reflection, so the −z wall is the one
        // facing out of the page.
        (int Id, int Data) Block(int x, int y, int z) => world.GetBlock(x, y, box.MinZ + box.MaxZ - z);

        bool Opaque(int x, int y, int z) => Block(x, y, z).Id != Blocks.Air;

        void Face(int depth, BlockPalette.Rgb rgb, double shade, params int[] points)
            => faces.Add((depth, Shade(rgb, shade),
                $"M{points[0]} {points[1]}L{points[2]} {points[3]}L{points[4]} {points[5]}L{points[6]} {points[7]}Z"));
    }

    /// <summary>A colour dimmed by a constant factor — how a face says which way it points.</summary>
    public static string Shade(BlockPalette.Rgb rgb, double factor) => string.Create(
        CultureInfo.InvariantCulture,
        $"#{(int)Math.Round(rgb.R * factor):x2}{(int)Math.Round(rgb.G * factor):x2}{(int)Math.Round(rgb.B * factor):x2}");

    // ── the flat reads ────────────────────────────────────────────────────────────────────────────────
    /// <summary>The highest block of each column: what the roof does, its hole and whether its eave oversails
    /// the walls, and nothing else.</summary>
    public static string Plan(VoxelWorld world, BlockBox box, int cell = 9)
        => SvgRaster.Raster(box.Width, box.Depth, cell, (column, row) =>
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
    {
        var view = BlockSideView.Project(world, box.MinX, box.MaxX, box.MinZ, box.MaxZ, box.MinY, box.MaxY);
        return SvgRaster.Raster(view.Columns, box.Height, cell, (column, row) =>
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

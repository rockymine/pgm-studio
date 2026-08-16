using System.Text.Json;
using PgmStudio.Data.Features;
using PgmStudio.Export;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Views;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;

namespace PgmStudio.Api.Services;

/// <summary>
/// Whole-map terrain previews (docs/world-export/terrain-painting.md TP10): what a plan or a sketch actually
/// looks like once its terrain is painted. Both go through the real painter and <see cref="BlockPalette"/> (the
/// block-id → colour table), so a preview shows the blocks the export would place rather than a second guess at
/// them. The library's per-material and per-theme pictures are <see cref="StylePreview"/>'s — sampling one
/// material over a sample grid is a different question from painting a map.
/// </summary>
public static class TerrainPreview
{
    /// <summary>The themed-map render plus the block-space AABB it covers, so a caller can place the raster in
    /// the plan's own world frame (the plan canvas's block coordinates) rather than only show it standalone.
    /// <see cref="MinX"/>/<see cref="MinZ"/> is the top-left block; <see cref="SpanX"/>/<see cref="SpanZ"/> the
    /// block extent — the image spans world rect (MinX, MinZ)–(MinX+SpanX, MinZ+SpanZ).</summary>
    public readonly record struct MapPaint(string Svg, int MinX, int MinZ, int SpanX, int SpanZ);

    /// <summary>A whole plan compiled, its terrain painted (through the scoped theme resolver), and rendered
    /// top-down: each footprint cell shows its highest painted block, coloured via <see cref="BlockPalette"/>.
    /// Structures above the terrain (rooms, approach walls) show their own top; the floating observer platform
    /// is above the scan window and excluded. Returns the render and the block AABB it spans.</summary>
    public static MapPaint MapSvg(string planJson)
    {
        if (PlanWorld.Compile(planJson) is not { } compiled) return EmptyPaint("bad plan");
        var (layoutJson, intent) = compiled;

        var surface = new Dictionary<(int X, int Z), int>();
        foreach (var (x, z, _, top) in SketchRasterizer.RasterizeColumns(layoutJson))
            if (!surface.TryGetValue((x, z), out var cur) || top > cur) surface[(x, z)] = top;
        if (surface.Count == 0) return EmptyPaint("empty plan");

        var world = SketchWorldBuilder.Build(layoutJson, intent).World;

        int minX = surface.Keys.Min(c => c.X), maxX = surface.Keys.Max(c => c.X);
        int minZ = surface.Keys.Min(c => c.Z), maxZ = surface.Keys.Max(c => c.Z);
        int spanX = maxX - minX + 1, spanZ = maxZ - minZ + 1;
        int cell = Math.Clamp(600 / Math.Max(spanX, spanZ), 2, 8);

        var seenFromAbove = new Dictionary<(int X, int Z), string>(surface.Count);
        foreach (var (cellPos, top) in surface)
        {
            // scan a short window above the column's surface (catches an approach wall at ~+4, skips the
            // observer platform at ~+15) down to the first solid block.
            for (var y = top + 5; y >= 1; y--)
            {
                var (id, data) = world.GetBlock(cellPos.X, y, cellPos.Z);
                if (id == 0) continue;
                seenFromAbove[cellPos] = BlockPalette.Hex(id, data);
                break;
            }
        }

        // A cell with no block emits no rect: this render is blitted onto the plan canvas as an overlay, so the
        // void between and around the painted terrain stays transparent and the canvas grid shows through.
        var svg = SvgRaster.Raster(spanX, spanZ, cell,
            (x, z) => seenFromAbove.GetValueOrDefault((minX + x, minZ + z)));
        return new MapPaint(svg, minX, minZ, spanX, spanZ);
    }

    /// <summary>The terrain paint a sketch layout exports, one entry per footprint cell: the block seen from
    /// directly above, with its data value. The real export path produces it — rasterise the columns, build the
    /// terrain, classify it into <see cref="TerrainProfile"/> columns, then take each column's top block from
    /// <see cref="TerrainPainter.TopBlock"/>, resolved through <see cref="TerrainThemeScope"/>'s per-cell theme
    /// and <see cref="TeamTerritory"/>'s ownership — so a cell carries the block the export actually places,
    /// patterns and team tints included. Empty when nothing is drawn. The caller shapes these into the
    /// block-pixel payload the canvas block overlays blit.
    /// <para>Only the tops are resolved, not the whole world: painting every block of every column and then
    /// reading one of them back was the bulk of this call. It is the same painter either way —
    /// <see cref="TerrainPainter.ColumnBlocks"/> is the single place a band becomes blocks, and the full paint
    /// walks the sequence this stops after the first element of.</para></summary>
    public static IReadOnlyList<SurfaceCell> SketchPaintCells(string layoutJson, MapIntent intent)
    {
        var terrain = SketchTerrainBuilder.Build(SketchRasterizer.RasterizeColumns(layoutJson));
        var surface = terrain.SurfaceTop;
        if (surface.Count == 0) return [];

        var themeAt = TerrainThemeScope.ThemeAt(layoutJson);
        var teamAt = TeamTerritory.DamageAt(surface.Keys, intent);
        var profile = new TerrainProfile(terrain.World, surface);

        var cells = new List<SurfaceCell>(surface.Count);
        foreach (var (cell, top) in surface)
        {
            if (profile.TryGetColumn(cell, out var column)
                && TerrainPainter.TopBlock(cell.X, cell.Z, column, themeAt(cell.X, cell.Z), teamAt(cell.X, cell.Z)) is { } painted)
            {
                cells.Add(new SurfaceCell(cell.X, cell.Z, painted.Id, painted.Data));
                continue;
            }
            // Not paintable (a bare bedrock course): the terrain's own block stands. SurfaceTop is the first
            // air Y above the column, so scan down from just under it for the first solid.
            for (var y = Math.Min(top, VoxelWorld.MaxHeight - 1); y >= 0; y--)
            {
                var (id, data) = terrain.World.GetBlock(cell.X, y, cell.Z);
                if (id == 0) continue;
                cells.Add(new SurfaceCell(cell.X, cell.Z, id, data));
                break;
            }
        }
        return cells;
    }

    private static MapPaint EmptyPaint(string reason) => new(Empty(reason), 0, 0, 0, 0);

    private static string Empty(string _) =>
        "<svg xmlns='http://www.w3.org/2000/svg' width='120' height='40'><rect width='120' height='40' fill='#0a1120'/></svg>";
}

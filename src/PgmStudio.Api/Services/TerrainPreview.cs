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
/// Whole-map terrain previews (docs/world-export/terrain-painting.md TP10): what a sketch actually looks like
/// once its terrain is painted. It goes through the real painter and <see cref="BlockPalette"/> (the
/// block-id → colour table), so a preview shows the blocks the export would place rather than a second guess at
/// them. The library's per-material and per-theme pictures are <see cref="StylePreview"/>'s — sampling one
/// material over a sample grid is a different question from painting a map.
/// </summary>
public static class TerrainPreview
{
    /// <summary>A sketch's painted surface: the block every footprint cell shows from above, and the biome
    /// each of those columns carries. The two travel together because the colour a cell is drawn in is the
    /// block's <em>and</em> the biome's — grass, leaves and water are tinted by the ground they stand on, so a
    /// caller handed only the blocks would have to resolve the field a second time to draw them right.</summary>
    /// <param name="Cells">One entry per footprint cell, the block seen from directly above.</param>
    /// <param name="BiomeAt">The biome one column carries, by block coordinates.</param>
    public readonly record struct SketchPaint(
        IReadOnlyList<SurfaceCell> Cells, Func<int, int, byte> BiomeAt);

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
    public static SketchPaint SketchPaintCells(string layoutJson, MapIntent intent)
    {
        var terrain = TerrainBuilder.Build(SketchRasterizer.RasterizeColumns(layoutJson));
        var surface = terrain.SurfaceTop;
        if (surface.Count == 0) return new SketchPaint([], (_, _) => Biome.Plains);

        // The biome the export would write, written into this world too, so the overlay's colours come off
        // the same pass the world does rather than off a second reading of the same document.
        BiomeScope.Paint(terrain.World, BiomeScope.FieldOf(layoutJson),
                         DressingScope.SymmetryOf(layoutJson).Canonical);

        var themeAt = TerrainThemeScope.ThemeAt(layoutJson);
        var teamAt = TeamTerritory.DamageAt(surface.Keys, intent);
        var profile = new TerrainProfile(terrain.World, surface);

        // A top-down preview shows what a board looks like from above, so each cell resolves its theme on the
        // layer whose surface is the one being seen — the highest that reaches this cell's top.
        var topLayer = new Dictionary<(int X, int Z), string>(surface.Count);
        foreach (var (layer, tops) in terrain.SurfaceByLayer)
            foreach (var (cell, top) in tops)
                if (surface.TryGetValue(cell, out var whole) && top == whole) topLayer[cell] = layer;

        var cells = new List<SurfaceCell>(surface.Count);
        foreach (var (cell, top) in surface)
        {
            if (profile.TryGetColumn(cell, out var column) && !column.Structure
                && themeAt(topLayer.GetValueOrDefault(cell, ""), cell.X, cell.Z) is var paint
                && TerrainPainter.TopBlock(cell.X, cell.Z, column, paint.Theme,
                       teamAt(cell.X, cell.Z), ground: profile.Ground, turn: paint.Turn,
                       rule: PaintRule.First(paint.Rules, cell, column, cell)) is { } painted)
            {
                cells.Add(new SurfaceCell(cell.X, cell.Z, painted.Id, painted.Data));
                continue;
            }
            // A stamp or a bare bedrock course tops this column, so what the board shows from above is the
            // block that is there. SurfaceTop is the first air Y above the column, so scan down from just
            // under it for the first solid.
            for (var y = Math.Min(top, VoxelWorld.MaxHeight - 1); y >= 0; y--)
            {
                var (id, data) = terrain.World.GetBlock(cell.X, y, cell.Z);
                if (id == 0) continue;
                cells.Add(new SurfaceCell(cell.X, cell.Z, id, data));
                break;
            }
        }
        return new SketchPaint(cells, terrain.World.GetBiome);
    }
}

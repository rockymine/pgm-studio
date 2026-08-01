using System.Text;
using System.Text.Json;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Services;

/// <summary>
/// Top-down SVG previews for the Theme rail (docs/world-export/terrain-painting.md TP10). Both reuse the real
/// terrain-paint materials and <see cref="BlockPalette"/> (the block-id → colour table), so a preview shows the
/// same blocks the export would place — no second implementation of the patterns. <see cref="MaterialSvg"/> is a
/// per-material swatch over a sample grid (so a voronoi / noise / wall-run reads at a glance); <see cref="MapSvg"/>
/// paints a whole plan's terrain and renders its top blocks from above.
/// </summary>
public static class TerrainPreview
{
    /// <summary>A material rendered to an <paramref name="n"/>×<paramref name="n"/> top-down swatch. The left
    /// half samples a neutral cell, the right half a team cell, so a team tint shows both; the perimeter arc is
    /// the x column, so a wall-run's stripes read across the swatch.</summary>
    public static string MaterialSvg(TerrainMaterial material, TerrainBucket bucket, int n = 40, int cell = 4)
    {
        var sb = new StringBuilder();
        int size = n * cell;
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{size}' height='{size}' viewBox='0 0 {size} {size}' shape-rendering='crispEdges'>");
        for (var z = 0; z < n; z++)
        for (var x = 0; x < n; x++)
        {
            int team = x < n / 2 ? -1 : 14;   // neutral | a sample team (red), so a tint shows its fallback + colour
            var (id, data) = material.Resolve(new BucketContext(x, 0, z, bucket, 0, team, x));
            sb.Append($"<rect x='{x * cell}' y='{z * cell}' width='{cell}' height='{cell}' fill='{BlockPalette.Hex(id, data)}'/>");
        }
        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>One swatch per themeable bucket for a serialized theme (rim / wall / surface / fill).</summary>
    public static Dictionary<string, string> ThemeSwatches(string themeJson)
    {
        var theme = TerrainThemeJson.Deserialize(themeJson);
        return new()
        {
            ["rim"] = MaterialSvg(theme.MaterialFor(TerrainBucket.Rim), TerrainBucket.Rim),
            ["wall"] = MaterialSvg(theme.MaterialFor(TerrainBucket.Wall), TerrainBucket.Wall),
            ["surface"] = MaterialSvg(theme.MaterialFor(TerrainBucket.Surface), TerrainBucket.Surface),
            ["fill"] = MaterialSvg(theme.MaterialFor(TerrainBucket.Fill), TerrainBucket.Fill),
        };
    }

    /// <summary>A whole plan compiled, its terrain painted (through the scoped theme resolver), and rendered
    /// top-down: each footprint cell shows its highest painted block, coloured via <see cref="BlockPalette"/>.
    /// Structures above the terrain (rooms, approach walls) show their own top; the floating observer platform
    /// is above the scan window and excluded.</summary>
    public static string MapSvg(string planJson)
    {
        var plan = PlanModel.Parse(planJson);
        if (plan is null) return Empty("bad plan");
        var (layout, intent) = PlanCompiler.Compile(plan);
        var layoutJson = JsonSerializer.Serialize(layout, SketchLayout.Json);

        var surface = new Dictionary<(int X, int Z), int>();
        foreach (var (x, z, _, top) in SketchRasterizer.RasterizeColumns(layoutJson))
            if (!surface.TryGetValue((x, z), out var cur) || top > cur) surface[(x, z)] = top;
        if (surface.Count == 0) return Empty("empty plan");

        var world = SketchWorldBuilder.Build(layoutJson, intent).World;

        int minX = surface.Keys.Min(c => c.X), maxX = surface.Keys.Max(c => c.X);
        int minZ = surface.Keys.Min(c => c.Z), maxZ = surface.Keys.Max(c => c.Z);
        int spanX = maxX - minX + 1, spanZ = maxZ - minZ + 1;
        int cell = Math.Clamp(600 / Math.Max(spanX, spanZ), 2, 8);
        int w = spanX * cell, h = spanZ * cell;

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{w}' height='{h}' viewBox='0 0 {w} {h}' shape-rendering='crispEdges'>");
        sb.Append($"<rect x='0' y='0' width='{w}' height='{h}' fill='#0a1120'/>");
        foreach (var (cellPos, top) in surface)
        {
            // scan a short window above the column's surface (catches an approach wall at ~+4, skips the
            // observer platform at ~+15) down to the first solid block.
            string? hex = null;
            for (var y = top + 5; y >= 1; y--)
            {
                var (id, data) = world.GetBlock(cellPos.X, y, cellPos.Z);
                if (id != 0) { hex = BlockPalette.Hex(id, data); break; }
            }
            if (hex is null) continue;
            int px = (cellPos.X - minX) * cell, py = (cellPos.Z - minZ) * cell;
            sb.Append($"<rect x='{px}' y='{py}' width='{cell}' height='{cell}' fill='{hex}'/>");
        }
        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string Empty(string _) =>
        "<svg xmlns='http://www.w3.org/2000/svg' width='120' height='40'><rect width='120' height='40' fill='#0a1120'/></svg>";
}

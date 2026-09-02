using System.Text;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Export;

/// <summary>
/// Counts a built board's ground cells by the terrain-paint theme that colours them
/// (docs/world-scan/read-backs.md, <c>GET /map/{slug}/themes/census</c>): how many cells each theme owns,
/// what materials its cells carry in the finished world, and which theme borders which. Resolved through the
/// same shape and layer ownership <see cref="TerrainThemeScope"/> paints against, so a census and the board
/// it describes cannot disagree.
/// </summary>
public static class ThemeCensus
{
    /// <summary>One theme's share of the board — a registered theme id, or the map default where no shape
    /// claims a cell. <see cref="Materials"/> is the distinct surface blocks its cells carry, as
    /// <c>id:data name</c>, most frequent first and cut at twelve; <see cref="MaterialCount"/> is the true count
    /// whether or not the list was cut.</summary>
    public sealed record Row(string Id, int Cells, double Share, IReadOnlyList<string> Materials, int MaterialCount);

    /// <summary>Two themes that share a border, <see cref="A"/> ordered before <see cref="B"/> so a pair is
    /// named once, and how many 4-neighbour cell pairs cross from one to the other.</summary>
    public sealed record Border(string A, string B, int Cells);

    /// <summary>The census: how many themes the ground carries, how many distinct <c>id:data</c> surface blocks
    /// the whole board spends, one row per theme largest first, and every bordering pair largest first.</summary>
    public sealed record Result(int Themes, int Palette, IReadOnlyList<Row> ByTheme, IReadOnlyList<Border> Adjacency);

    // The two neighbours that, taken over every cell, enumerate every 4-neighbour pair once: a cell's own
    // right and down neighbour cover every horizontal and vertical adjacency on the board exactly once.
    private static readonly (int Dx, int Dz)[] HalfNeighbours = [(1, 0), (0, 1)];

    /// <summary>The census over every cell of <paramref name="built"/>'s surface.</summary>
    public static Result Compute(BuiltWorld built, string layoutJson)
    {
        var layout = SketchLayout.Parse(layoutJson);
        var mapThemeId = layout?.MapTheme is { Length: > 0 } named ? named : "default";

        // (layer, shape) → the theme id it paints with — the same walk TerrainThemeScope.ThemeAt takes,
        // stopping short of resolving the id into a TerrainTheme: the census counts ids, not fill material.
        var knownThemeIds = layout?.Themes?.Keys.ToHashSet() ?? [];
        var shapeThemeId = new Dictionary<(string Layer, string Shape), string>();
        foreach (var layer in SketchLayout.Stack(layout))
            foreach (var shape in layer.Shapes)
                if (shape.Theme is { } themeId && knownThemeIds.Contains(themeId))
                    shapeThemeId[(layer.Id!, shape.Id)] = themeId;

        var cellShape = shapeThemeId.Count == 0
            ? new Dictionary<(string, int, int), string>()
            : SketchRasterizer.ShapeThemeOwners(layoutJson);

        var layerOf = LayerPerCell(built);
        var themeAt = new Dictionary<(int X, int Z), string>();
        foreach (var cell in built.Surface.Keys)
            themeAt[cell] = layerOf.TryGetValue(cell, out var layer)
                && cellShape.TryGetValue((layer, cell.X, cell.Z), out var shapeId)
                && shapeThemeId.TryGetValue((layer, shapeId), out var themeId)
                ? themeId : mapThemeId;

        var cellsOf = new Dictionary<string, List<(int X, int Z)>>();
        foreach (var (cell, themeId) in themeAt)
        {
            if (!cellsOf.TryGetValue(themeId, out var cells)) cellsOf[themeId] = cells = [];
            cells.Add(cell);
        }

        var total = built.Surface.Count;
        var palette = new HashSet<(int Id, int Data)>();
        var byTheme = cellsOf
            .Select(entry => RowOf(built, entry.Key, entry.Value, total, palette))
            .OrderByDescending(row => row.Cells)
            .ToList();

        var borders = new Dictionary<(string A, string B), int>();
        foreach (var (cell, here) in themeAt)
            foreach (var (dx, dz) in HalfNeighbours)
            {
                var side = (cell.X + dx, cell.Z + dz);
                if (!themeAt.TryGetValue(side, out var there) || there == here) continue;
                var pair = string.CompareOrdinal(here, there) <= 0 ? (here, there) : (there, here);
                borders[pair] = borders.GetValueOrDefault(pair) + 1;
            }
        var adjacency = borders
            .OrderByDescending(entry => entry.Value)
            .Select(entry => new Border(entry.Key.A, entry.Key.B, entry.Value))
            .ToList();

        return new Result(byTheme.Count, palette.Count, byTheme, adjacency);
    }

    private static Row RowOf(BuiltWorld built, string themeId, List<(int X, int Z)> cells,
        int total, HashSet<(int Id, int Data)> palette)
    {
        var materials = new Dictionary<(int Id, int Data), int>();
        foreach (var cell in cells)
        {
            var block = SurfaceBlock(built, cell);
            palette.Add(block);
            materials[block] = materials.GetValueOrDefault(block) + 1;
        }
        var named = materials
            .OrderByDescending(entry => entry.Value)
            .Take(12)
            .Select(entry => $"{entry.Key.Id}:{entry.Key.Data} {BlockPalette.Name(entry.Key.Id, entry.Key.Data)}")
            .ToList();
        return new Row(themeId, cells.Count, total == 0 ? 0 : (double)cells.Count / total,
            named, materials.Count);
    }

    /// <summary>The census as characters: one line per theme with its share and its materials, then every
    /// border between two themes and how many cells cross it.</summary>
    public static string Render(Result census)
    {
        var cells = census.ByTheme.Sum(row => row.Cells);
        var text = new StringBuilder();
        text.Append($"THEMES  {census.Themes} themes over {cells} ground cells, {census.Palette} distinct "
            + "surface blocks\n");
        foreach (var row in census.ByTheme)
            text.Append($"  {row.Id}  {row.Cells} cells ({row.Share * 100:F1}%)  "
                + $"{string.Join(", ", row.Materials)}\n");
        text.Append("borders:\n");
        foreach (var border in census.Adjacency)
            text.Append($"  {border.A} | {border.B}  {border.Cells} cells\n");
        return text.ToString();
    }

    /// <summary>The block a cell's surface reads as in the finished world — the first block found scanning
    /// down from the course under <see cref="BuiltWorld.Surface"/>, since a cut or a levelling leaves the
    /// recorded surface over air, and air is not a material a theme spends. Air where the column is empty.</summary>
    private static (int Id, int Data) SurfaceBlock(BuiltWorld built, (int X, int Z) cell)
    {
        for (var y = built.Surface[cell] - 1; y >= 0; y--)
        {
            var block = built.World.GetBlock(cell.X, y, cell.Z);
            if (block.Id != 0) return block;
        }
        return (0, 0);
    }

    /// <summary>The layer whose own run reaches each surface cell's height — the storey
    /// <see cref="TerrainThemeScope"/> painted that cell against, read back off the column segments the build
    /// stood on rather than re-derived. A cell <see cref="BuiltWorld.Surface"/> carries always has a segment
    /// reaching that exact height, because the surface is itself the tallest of them.</summary>
    private static Dictionary<(int X, int Z), string> LayerPerCell(BuiltWorld built)
    {
        var layerOf = new Dictionary<(int X, int Z), string>();
        foreach (var segment in built.Columns ?? [])
        {
            if (!built.Surface.TryGetValue(segment.Cell, out var top) || segment.YTop != top) continue;
            layerOf.TryAdd(segment.Cell, segment.Layer);
        }
        return layerOf;
    }
}

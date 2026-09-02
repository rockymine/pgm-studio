using System.Text.Json;
using PgmStudio.Export;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Stamping;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Export.Tests;

/// <summary>
/// The theme census (WS22): a board's ground cells counted by the theme that paints them, resolved through
/// the same shape and layer ownership <see cref="TerrainThemeScope"/> paints against.
/// </summary>
public sealed class ThemeCensusTests
{
    private static JsonElement Surfaced(int id) => JsonSerializer.Deserialize<JsonElement>(
        TerrainThemeJson.Serialize(TerrainTheme.Default with
        {
            Surface = TerrainTheme.Default.Surface with { Material = new SolidMaterial(id), Depth = 1 },
        }));

    private static SketchShape Rect(string id, double minX, double minZ, double maxX, double maxZ, string? theme) =>
        new()
        {
            Id = id, Type = "rectangle", Operation = "add",
            MinX = minX, MinZ = minZ, MaxX = maxX, MaxZ = maxZ, BaseHeight = 1, Theme = theme,
        };

    private static string Layout(Dictionary<string, JsonElement> themes, string mapTheme, params SketchShape[] shapes) =>
        new SketchLayout
        {
            Setup = new SketchSetup { MirrorMode = "none", Center = new SketchCenter { Cx = 0, Cz = 0 } },
            Themes = themes,
            MapTheme = mapTheme,
            Layers = [SketchLayer.Ground(shapes.ToList(),
                [new SketchGroup { Id = "i", Mirrors = false, ShapeIds = shapes.Select(s => s.Id).ToList() }])],
        }.ToJson();

    // No WorldBuilder here: the census reads nothing a stamp places, so the terrain build and the painter
    // are the whole of what a BuiltWorld needs to carry for it.
    private static BuiltWorld Built(string layoutJson)
    {
        var columns = SketchRasterizer.RasterizeColumns(layoutJson);
        var terrain = TerrainBuilder.Build(columns);
        TerrainPainter.Paint(terrain.World, terrain.SurfaceByLayer, TerrainThemeScope.ThemeAt(layoutJson));
        return new BuiltWorld(terrain.World, 0, 1, 0, new MapIntent(), new WorldProvenance(),
            null, columns, default, terrain.Ground);
    }

    [Test]
    public async Task Two_themed_rectangles_give_two_rows_that_sum_to_the_surface_and_one_border()
    {
        var themes = new Dictionary<string, JsonElement>
            { ["map"] = Surfaced(100), ["red"] = Surfaced(200), ["blue"] = Surfaced(300) };
        var layoutJson = Layout(themes, "map",
            Rect("r", 0, 0, 10, 10, "red"), Rect("b", 10, 0, 20, 10, "blue"));
        var built = Built(layoutJson);

        var census = ThemeCensus.Compute(built, layoutJson);

        await Assert.That(census.Themes).IsEqualTo(2);
        await Assert.That(census.ByTheme.Sum(row => row.Cells)).IsEqualTo(built.Surface.Count);
        await Assert.That(census.Adjacency.Count).IsEqualTo(1);
        await Assert.That(census.Adjacency[0].Cells).IsGreaterThan(0);
        await Assert.That(new[] { census.Adjacency[0].A, census.Adjacency[0].B })
            .IsEquivalentTo(new[] { "red", "blue" });
    }

    [Test]
    public async Task A_board_with_one_theme_has_no_border()
    {
        var themes = new Dictionary<string, JsonElement> { ["map"] = Surfaced(100) };
        var layoutJson = Layout(themes, "map", Rect("r", 0, 0, 10, 10, null));
        var built = Built(layoutJson);

        var census = ThemeCensus.Compute(built, layoutJson);

        await Assert.That(census.Themes).IsEqualTo(1);
        await Assert.That(census.ByTheme[0].Cells).IsEqualTo(built.Surface.Count);
        await Assert.That(census.Adjacency).IsEmpty();
    }
}

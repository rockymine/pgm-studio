using System.Text.Json;
using PgmStudio.Export;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Minecraft.Stamping;
using PgmStudio.Minecraft.Painting;

namespace PgmStudio.Export.Tests;

/// <summary>
/// Scoped theme resolution for the export (docs/world-export/terrain-painting.md TP10): <see cref="TerrainThemeScope"/>
/// turns the theme data on the sketch layout into a per-cell theme — a themed shape paints its theme, everything
/// else the map default, and the smaller (most specific) shape wins an overlap. Because the scope is the shape,
/// rasterised fresh, reshaping a shape moves its paint. Pure (no DB): themes are built from real theme JSON and
/// read back through their fill material.
/// </summary>
public sealed class TerrainThemeScopeTests
{
    private static JsonElement Fill(int id) => JsonSerializer.Deserialize<JsonElement>(
        TerrainThemeJson.Serialize(TerrainTheme.Default with { Fill = new SolidMaterial(id) }));
    private static int FillId(TerrainTheme theme) => theme.Fill.Resolve(new BucketContext(0, 0, 0, TerrainBucket.Fill, 0)).Id;

    /// <summary>A theme whose top course is a distinctive solid — what a painted surface reads back as.</summary>
    private static JsonElement Surfaced(int id) => JsonSerializer.Deserialize<JsonElement>(
        TerrainThemeJson.Serialize(TerrainTheme.Default with
        {
            Surface = TerrainTheme.Default.Surface with { Material = new SolidMaterial(id), Depth = 1 },
        }));

    private static SketchShape Rect(string id, double minX, double minZ, double maxX, double maxZ, string? theme) =>
        new() { Id = id, Type = "rectangle", Operation = "add", MinX = minX, MinZ = minZ, MaxX = maxX, MaxZ = maxZ, Theme = theme };

    // A non-mirroring island so the shapes theme exactly their own footprint (no orbit fan to reason about).
    private static string Layout(Dictionary<string, JsonElement>? themes, string? mapTheme, params SketchShape[] shapes)
    {
        var layout = new SketchLayout
        {
            Setup = new SketchSetup { MirrorMode = "rot_180", Center = new SketchCenter { Cx = 0, Cz = 0 } },
            Themes = themes,
            MapTheme = mapTheme,
            Layers = [SketchLayer.Ground(shapes.ToList(),
                [new SketchGroup { Id = "i", Mirrors = false, ShapeIds = shapes.Select(s => s.Id).ToList() }])],
        };
        return layout.ToJson();
    }

    [Test]
    public async Task No_theming_resolves_the_builtin_default_everywhere()
    {
        var at = TerrainThemeScope.ThemeAt(Layout(null, null, Rect("s0", 0, 0, 4, 4, null)));
        await Assert.That(at("ground", 3, 3)).IsEqualTo(TerrainTheme.Default);
        await Assert.That(at("ground", -99, 99)).IsEqualTo(TerrainTheme.Default);
    }

    [Test]
    public async Task A_themed_shape_paints_its_theme_and_the_rest_the_map_default()
    {
        var themes = new Dictionary<string, JsonElement> { ["map"] = Fill(100), ["red"] = Fill(200) };
        var at = TerrainThemeScope.ThemeAt(Layout(themes, "map",
            Rect("p1", 0, 0, 4, 4, "red"),
            Rect("p2", 10, 10, 14, 14, null)));   // present but unthemed
        await Assert.That(FillId(at("ground", 2, 2))).IsEqualTo(200);     // inside p1 → red theme
        await Assert.That(FillId(at("ground", 12, 12))).IsEqualTo(100);   // inside p2, unthemed → map default
        await Assert.That(FillId(at("ground", 50, 50))).IsEqualTo(100);   // no shape → map default
    }

    [Test]
    public async Task Smallest_shape_wins_an_overlapped_cell()
    {
        var themes = new Dictionary<string, JsonElement> { ["big"] = Fill(400), ["small"] = Fill(300) };
        var at = TerrainThemeScope.ThemeAt(Layout(themes, null,
            Rect("big", 0, 0, 10, 10, "big"),
            Rect("small", 1, 1, 3, 3, "small")));
        await Assert.That(FillId(at("ground", 2, 2))).IsEqualTo(300);   // in both → the smaller (more specific) shape
        await Assert.That(FillId(at("ground", 6, 6))).IsEqualTo(400);   // only the big shape
    }

    [Test]
    public async Task Reshaping_a_shape_moves_its_paint()
    {
        var themes = new Dictionary<string, JsonElement> { ["map"] = Fill(100), ["red"] = Fill(200) };
        var before = TerrainThemeScope.ThemeAt(Layout(themes, "map", Rect("s0", 0, 0, 2, 2, "red")));
        await Assert.That(FillId(before("ground", 1, 1))).IsEqualTo(200);
        await Assert.That(FillId(before("ground", 5, 5))).IsEqualTo(100);

        // Same shape id, moved: the theme follows the new footprint with no re-assignment.
        var after = TerrainThemeScope.ThemeAt(Layout(themes, "map", Rect("s0", 4, 4, 6, 6, "red")));
        await Assert.That(FillId(after("ground", 1, 1))).IsEqualTo(100);
        await Assert.That(FillId(after("ground", 5, 5))).IsEqualTo(200);
    }

    [Test]
    public async Task An_unknown_map_theme_id_falls_back_to_the_builtin_default()
    {
        var at = TerrainThemeScope.ThemeAt(Layout(null, "missing", Rect("s0", 0, 0, 4, 4, null)));
        await Assert.That(at("ground", 0, 0)).IsEqualTo(TerrainTheme.Default);
    }

    // ── A stacked board wears one theme per storey ───────────────────────────────────────────────────────
    private static SketchShape Slab(string id, int floor, int height, string? theme) => new()
    {
        Id = id, Type = "rectangle", Operation = "add",
        MinX = 0, MinZ = 0, MaxX = 8, MaxZ = 8,
        Floor = floor, BaseHeight = height, Theme = theme,
    };

    private static string Stacked(Dictionary<string, JsonElement> themes, string mapTheme,
                                  SketchShape lower, SketchShape upper) =>
        new SketchLayout
        {
            Setup = new SketchSetup { MirrorMode = "rot_180", Center = new SketchCenter { Cx = 0, Cz = 0 } },
            Themes = themes,
            MapTheme = mapTheme,
            Layers =
            [
                new SketchLayer { Id = "ground", BaseY = 0, Layout = new SketchShapes { Shapes = [lower] } },
                new SketchLayer { Id = "deck", BaseY = 20, Layout = new SketchShapes { Shapes = [upper] } },
            ],
        }.ToJson();

    /// <summary>One cell, two storeys, two themes. The scope has to tell them apart or the upper slab's shape
    /// owns the ground beneath it — which is what made a deck wear the paint of the gallery under it.</summary>
    [Test]
    public async Task A_cell_on_two_layers_resolves_a_theme_for_each()
    {
        var themes = new Dictionary<string, JsonElement>
            { ["map"] = Fill(100), ["floor"] = Fill(200), ["roof"] = Fill(300) };
        var at = TerrainThemeScope.ThemeAt(Stacked(themes, "map",
            Slab("gallery", 0, 4, "floor"), Slab("deck", 0, 6, "roof")));

        await Assert.That(FillId(at("ground", 4, 4))).IsEqualTo(200);
        await Assert.That(FillId(at("deck", 4, 4))).IsEqualTo(300);
    }

    /// <summary>A shape id is unique within its layer and not across the stack: two made things compiled by one
    /// tool number their shapes alike, and each layer's cell has to paint its own shape's theme rather than
    /// whichever layer stated that id last.</summary>
    [Test]
    public async Task Two_layers_carrying_a_shape_of_one_id_each_paint_their_own_theme()
    {
        var themes = new Dictionary<string, JsonElement>
            { ["map"] = Fill(100), ["red"] = Fill(200), ["blue"] = Fill(300) };
        var at = TerrainThemeScope.ThemeAt(Stacked(themes, "map",
            Slab("statue-0", 0, 4, "red"), Slab("statue-0", 0, 6, "blue")));

        await Assert.That(FillId(at("ground", 4, 4))).IsEqualTo(200);
        await Assert.That(FillId(at("deck", 4, 4))).IsEqualTo(300);
    }

    /// <summary>And the paint follows: each storey's own surface takes its own theme's material, so a themed
    /// upper slab lands on blocks rather than on nothing.</summary>
    [Test]
    public async Task Each_storey_is_painted_in_its_own_theme()
    {
        var themes = new Dictionary<string, JsonElement>
            { ["map"] = Surfaced(100), ["floor"] = Surfaced(200), ["roof"] = Surfaced(300) };
        var layoutJson = Stacked(themes, "map", Slab("gallery", 0, 4, "floor"), Slab("deck", 0, 6, "roof"));

        var terrain = TerrainBuilder.Build(SketchRasterizer.RasterizeColumns(layoutJson));
        TerrainPainter.Paint(terrain.World, terrain.SurfaceByLayer, TerrainThemeScope.ThemeAt(layoutJson));

        var lowerTop = terrain.SurfaceByLayer["ground"][(4, 4)];
        var upperTop = terrain.SurfaceByLayer["deck"][(4, 4)];
        await Assert.That(upperTop).IsGreaterThan(lowerTop);

        await Assert.That(terrain.World.GetBlock(4, lowerTop - 1, 4).Id).IsEqualTo(200);
        await Assert.That(terrain.World.GetBlock(4, upperTop - 1, 4).Id).IsEqualTo(300);
    }
}

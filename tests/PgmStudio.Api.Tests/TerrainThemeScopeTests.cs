using System.Text.Json;
using PgmStudio.Api.Services;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Tests;

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
            Layout = new SketchShapes
            {
                Shapes = shapes.ToList(),
                Islands = [new SketchIsland { Id = "i", Mirrors = false, ShapeIds = shapes.Select(s => s.Id).ToList() }],
            },
        };
        return layout.ToJson();
    }

    [Test]
    public async Task No_theming_resolves_the_builtin_default_everywhere()
    {
        var at = TerrainThemeScope.ThemeAt(Layout(null, null, Rect("s0", 0, 0, 4, 4, null)));
        await Assert.That(at(3, 3)).IsEqualTo(TerrainTheme.Default);
        await Assert.That(at(-99, 99)).IsEqualTo(TerrainTheme.Default);
    }

    [Test]
    public async Task A_themed_shape_paints_its_theme_and_the_rest_the_map_default()
    {
        var themes = new Dictionary<string, JsonElement> { ["map"] = Fill(100), ["red"] = Fill(200) };
        var at = TerrainThemeScope.ThemeAt(Layout(themes, "map",
            Rect("p1", 0, 0, 4, 4, "red"),
            Rect("p2", 10, 10, 14, 14, null)));   // present but unthemed
        await Assert.That(FillId(at(2, 2))).IsEqualTo(200);     // inside p1 → red theme
        await Assert.That(FillId(at(12, 12))).IsEqualTo(100);   // inside p2, unthemed → map default
        await Assert.That(FillId(at(50, 50))).IsEqualTo(100);   // no shape → map default
    }

    [Test]
    public async Task Smallest_shape_wins_an_overlapped_cell()
    {
        var themes = new Dictionary<string, JsonElement> { ["big"] = Fill(400), ["small"] = Fill(300) };
        var at = TerrainThemeScope.ThemeAt(Layout(themes, null,
            Rect("big", 0, 0, 10, 10, "big"),
            Rect("small", 1, 1, 3, 3, "small")));
        await Assert.That(FillId(at(2, 2))).IsEqualTo(300);   // in both → the smaller (more specific) shape
        await Assert.That(FillId(at(6, 6))).IsEqualTo(400);   // only the big shape
    }

    [Test]
    public async Task Reshaping_a_shape_moves_its_paint()
    {
        var themes = new Dictionary<string, JsonElement> { ["map"] = Fill(100), ["red"] = Fill(200) };
        var before = TerrainThemeScope.ThemeAt(Layout(themes, "map", Rect("s0", 0, 0, 2, 2, "red")));
        await Assert.That(FillId(before(1, 1))).IsEqualTo(200);
        await Assert.That(FillId(before(5, 5))).IsEqualTo(100);

        // Same shape id, moved: the theme follows the new footprint with no re-assignment.
        var after = TerrainThemeScope.ThemeAt(Layout(themes, "map", Rect("s0", 4, 4, 6, 6, "red")));
        await Assert.That(FillId(after(1, 1))).IsEqualTo(100);
        await Assert.That(FillId(after(5, 5))).IsEqualTo(200);
    }

    [Test]
    public async Task An_unknown_map_theme_id_falls_back_to_the_builtin_default()
    {
        var at = TerrainThemeScope.ThemeAt(Layout(null, "missing", Rect("s0", 0, 0, 4, 4, null)));
        await Assert.That(at(0, 0)).IsEqualTo(TerrainTheme.Default);
    }
}

using System.Text.Json;
using PgmStudio.Api.Services;
using PgmStudio.Data.Features;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Minecraft.Painting;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The sketch's terrain paint as the block-pixel payload the Blocks overlay blits
/// (docs/world-export/terrain-painting.md TP10). <see cref="TerrainPreview.SketchPaintCells"/> runs the export's own
/// path — rasterise, build the terrain, paint through the scoped resolver — so what the overlay shows is the
/// block the export places, per cell. That is the property under test: a themed shape's own surface block
/// reaches the payload, buckets other than the surface do too, and the whole footprint is covered.
/// </summary>
public sealed class SketchPaintCellsTests
{
    private const int Grass = 2, Sandstone = 24, Quartz = 155;

    // A theme whose surface is one solid block, so a cell's block names the theme that painted it.
    private static JsonElement Surface(int id) => JsonSerializer.Deserialize<JsonElement>(
        TerrainThemeJson.Serialize(TerrainTheme.Default with
        {
            Surface = TerrainTheme.Default.Surface with { Material = new SolidMaterial(id) },
        }));

    private static SketchShape Rect(string id, double minX, double minZ, double maxX, double maxZ, string? theme) =>
        new()
        {
            Id = id, Type = "rectangle", Operation = "add",
            MinX = minX, MinZ = minZ, MaxX = maxX, MaxZ = maxZ,
            BaseHeight = 4, Floor = 0, Theme = theme,
        };

    // A non-mirroring island, so each shape paints exactly its own footprint.
    private static string Layout(Dictionary<string, JsonElement>? themes, string? mapTheme, params SketchShape[] shapes)
        => new SketchLayout
        {
            Setup = new SketchSetup { MirrorMode = "rot_180", Center = new SketchCenter { Cx = 0, Cz = 0 } },
            Themes = themes,
            MapTheme = mapTheme,
            Layers = [SketchLayer.Ground([.. shapes],
                [new SketchGroup { Id = "i", Mirrors = false, ShapeIds = [.. shapes.Select(s => s.Id)] }])],
        }.ToJson();

    private static IReadOnlyList<SurfaceCell> Paint(string layoutJson)
        => TerrainPreview.SketchPaintCells(layoutJson, new MapIntent());

    [Test]
    public async Task Each_themed_shape_contributes_its_own_surface_block()
    {
        var themes = new Dictionary<string, JsonElement> { ["a"] = Surface(Grass), ["b"] = Surface(Sandstone) };
        var cells = Paint(Layout(themes, null,
            Rect("s0", 0, 0, 20, 20, "a"),
            Rect("s1", 40, 40, 60, 60, "b")));

        var blocks = cells.Select(c => c.BlockId).ToHashSet();
        await Assert.That(blocks).Contains(Grass);
        await Assert.That(blocks).Contains(Sandstone);
        // Every cell of the first shape's interior is its theme's block, not the other's.
        await Assert.That(cells.Where(c => c.X is > 2 and < 18 && c.Z is > 2 and < 18).All(c => c.BlockId == Grass)).IsTrue();
    }

    [Test]
    public async Task The_rim_bucket_reaches_the_payload_not_just_the_surface()
    {
        // Meadow's rim is quartz and only the footprint edge takes it, so a flat "one colour per theme"
        // preview cannot produce it — its presence is what proves the real painter ran.
        var themes = new Dictionary<string, JsonElement>
            { ["map"] = JsonSerializer.Deserialize<JsonElement>(TerrainThemeJson.Serialize(ThemePresets.Meadow)) };
        var cells = Paint(Layout(themes, "map", Rect("s0", 0, 0, 20, 20, null)));
        await Assert.That(cells.Any(c => c.BlockId == Quartz)).IsTrue();
        await Assert.That(cells.Any(c => c.BlockId != Quartz)).IsTrue();   // an interior that is not rim
    }

    [Test]
    public async Task Every_footprint_cell_is_painted_exactly_once()
    {
        var cells = Paint(Layout(null, null, Rect("s0", 0, 0, 10, 10, null)));
        await Assert.That(cells.Count).IsEqualTo(cells.Select(c => (c.X, c.Z)).Distinct().Count());
        await Assert.That(cells.Count).IsEqualTo(100);   // a 10×10 rectangle covers its cells, max exclusive
    }

    [Test]
    public async Task An_empty_layout_paints_nothing()
    {
        await Assert.That(Paint(Layout(null, null)).Count).IsEqualTo(0);
    }
}

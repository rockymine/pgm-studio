using System.Text.RegularExpressions;
using PgmStudio.Api.Services;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The dressing stage's map-facing half (G161): resolving which recipe governs a cell, what the pass must
/// leave bare, and how the map is mirrored — plus the preview, which is asserted by what it <em>grew</em>
/// rather than by the bytes it drew.
/// </summary>
public sealed class DressingScopeTests
{
    private const string Meadow = """{"flora":{"coverage":0.6}}""";
    private const string Scree = """{"boulders":{"density":0.5}}""";

    private static string Layout(string body) => $$$"""
        {"setup":{"bbox":{"min_x":-40,"max_x":40,"min_z":-40,"max_z":40},
                  "center":{"cx":0,"cz":0},"mirror_mode":"rot_180"},
         {{{body}}}}
        """;

    // ── which recipe governs a cell ────────────────────────────────────────────────────────────────
    [Test]
    public async Task An_undressed_sketch_grows_nothing_anywhere()
    {
        var recipeAt = DressingScope.RecipeAt(Layout("""
            "layers":[{"base_y":0,"layout":{"shapes":[{"id":"a","type":"rectangle","min_x":0,"min_z":0,"max_x":20,"max_z":20}]}}]
            """));
        // The map that never opened the phase exports exactly as it did before the stage existed.
        await Assert.That(recipeAt(5, 5).IsBare).IsTrue();
    }

    [Test]
    public async Task A_shapes_own_dressing_wins_over_the_map_default()
    {
        var recipeAt = DressingScope.RecipeAt(Layout($$$"""
            "dressings":{"meadow":{{{Meadow}}},"scree":{{{Scree}}}},
            "mapDressing":"meadow",
            "layers":[{"base_y":0,"layout":{"shapes":[
              {"id":"ground","type":"rectangle","min_x":-30,"min_z":-30,"max_x":30,"max_z":30},
              {"id":"rocks","type":"rectangle","min_x":0,"min_z":0,"max_x":10,"max_z":10,"dressing":"scree"}]}}]
            """));

        await Assert.That(recipeAt(5, 5).Boulders).IsNotNull();     // inside the scree shape
        await Assert.That(recipeAt(5, 5).Flora).IsNull();
        await Assert.That(recipeAt(-20, -20).Flora).IsNotNull();    // outside it, the map default
        await Assert.That(recipeAt(-20, -20).Boulders).IsNull();
    }

    [Test]
    public async Task The_map_default_covers_every_cell_no_shape_claims()
    {
        var recipeAt = DressingScope.RecipeAt(Layout($$$"""
            "dressings":{"meadow":{{{Meadow}}}}, "mapDressing":"meadow",
            "layers":[{"base_y":0,"layout":{"shapes":[]}}]
            """));
        await Assert.That(recipeAt(0, 0).Flora).IsNotNull();
        await Assert.That(recipeAt(999, -999).Flora).IsNotNull();
    }

    [Test]
    public async Task The_maps_symmetry_is_what_the_pass_fans_props_through()
    {
        var symmetry = DressingScope.SymmetryOf(Layout("""
            "layers":[{"base_y":0,"layout":{"shapes":[]}}]
            """));
        await Assert.That(symmetry.Mode).IsEqualTo("rot_180");
        await Assert.That(symmetry.Order).IsEqualTo(2);
        await Assert.That(symmetry.ImageCell(3, 4, 1)).IsEqualTo((-4, -5));
    }

    // ── what must be left bare ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Nothing_is_placed_on_what_the_map_is_played_through()
    {
        var world = new VoxelWorld();
        var surface = new Dictionary<(int X, int Z), int>();
        for (var z = 0; z < 40; z++)
        for (var x = 0; x < 40; x++)
        {
            world.SetBlock(x, 7, z, Blocks.Grass);
            surface[(x, z)] = 8;
        }
        // A monument's wool standing on one column — a stamp, which the pass has no business planting on.
        world.SetBlock(30, 7, 30, Blocks.Wool, 14);

        var intent = new MapIntent
        {
            Spawns = [new SpawnIntent { Team = "red", Point = new Pt(5, 8, 5) }],
            Wools = [new WoolIntent { Owner = "red", Color = "red", Spawn = new Pt(20, 8, 20) }],
        };
        var isProtected = DressingScope.ProtectedAt(world, surface, intent);

        await Assert.That(isProtected(5, 5)).IsTrue();      // the spawn
        await Assert.That(isProtected(6, 6)).IsTrue();      // and its margin
        await Assert.That(isProtected(20, 20)).IsTrue();    // the wool
        await Assert.That(isProtected(30, 30)).IsTrue();    // the column a stamp stands on
        await Assert.That(isProtected(15, 34)).IsFalse();   // plain terrain
    }

    // ── the preview ────────────────────────────────────────────────────────────────────────────────
    private static HashSet<string> Fills(string svg)
        => Regex.Matches(svg, "fill='(#[0-9a-f]{6})'").Select(m => m.Groups[1].Value).ToHashSet();

    [Test]
    public async Task The_preview_grows_the_recipe_rather_than_drawing_an_impression_of_it()
    {
        var views = DressingPreview.Views(
            new DressingRecipe { Flora = new FloraSpec(Coverage: 0.7, FlowerShare: 0.4) }, TerrainTheme.Default);

        await Assert.That(views.Counts.Plants).IsGreaterThan(100);
        await Assert.That(views.Counts.Trees).IsEqualTo(0);
        // Flowers are the point of a flower field, so the picture has to carry their colours and not one
        // generic plant green.
        await Assert.That(Fills(views.Plan)).Contains(BlockPalette.Hex(DressingPalette.RedFlower, 0));
        await Assert.That(Fills(views.Plan)).Contains(BlockPalette.Hex(DressingPalette.YellowFlower, 0));
    }

    [Test]
    public async Task The_theme_the_preview_grows_on_is_what_decides_whether_anything_grows()
    {
        var meadow = new DressingRecipe { Flora = new FloraSpec(Coverage: 0.8) };
        var paved = TerrainTheme.Default with { Surface = new TopBand(new SolidMaterial(Blocks.QuartzBlock), 1) };

        await Assert.That(DressingPreview.Views(meadow, TerrainTheme.Default).Counts.Plants).IsGreaterThan(0);
        await Assert.That(DressingPreview.Views(meadow, paved).Counts.Plants).IsEqualTo(0);
    }

    [Test]
    public async Task The_section_crops_to_what_is_there_so_ground_cover_and_a_forest_read_at_the_same_scale()
    {
        // A fixed sky would draw a meadow as one green line under thirty courses of nothing.
        var meadow = DressingPreview.Views(new DressingRecipe { Flora = new FloraSpec() }, TerrainTheme.Default);
        var forest = DressingPreview.Views(
            new DressingRecipe { Trees = new TreeSpec(Density: 0.8, GroveThreshold: 1.0) }, TerrainTheme.Default);

        await Assert.That(Height(forest.Section)).IsGreaterThan(Height(meadow.Section));
    }

    private static int Height(string svg)
        => int.Parse(Regex.Match(svg, "height='(\\d+)'").Groups[1].Value);
}

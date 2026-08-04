using System.Text.RegularExpressions;
using PgmStudio.Api.Services;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The dressing stage's map-facing half (G161): reading what an author placed, what the pass must leave bare,
/// and how the map is mirrored — plus the preview, which is asserted by what it <em>placed</em> rather than by
/// the bytes it drew.
/// </summary>
public sealed class DressingScopeTests
{
    private static string Layout(string body) => $$$"""
        {"setup":{"bbox":{"min_x":-40,"max_x":40,"min_z":-40,"max_z":40},
                  "center":{"cx":0,"cz":0},"mirror_mode":"rot_180"},
         "layers":[{"base_y":0,"layout":{"shapes":[]}}]{{{body}}}}
        """;

    // ── what the author placed ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task An_undressed_sketch_places_nothing()
    {
        // The map that never opened the phase exports exactly as it did before the stage existed.
        await Assert.That(DressingScope.PropsOf(Layout(""))).IsEmpty();
    }

    [Test]
    public async Task Every_kind_of_prop_is_read_back_as_the_kind_it_was_written_as()
    {
        var props = DressingScope.PropsOf(Layout("""
            ,"dressing":{"props":[
              {"kind":"path","id":"p","points":[[0,0],[20,10]],"radius":3,"style":"stones","seed":5,
               "blocks":[{"id":4,"data":0}]},
              {"kind":"tree","id":"t","x":10,"z":12,"species":"birch","height":22,"seed":9},
              {"kind":"boulder","id":"b","x":-8,"z":4,"form":"cairn","size":4,"seed":11},
              {"kind":"flora","id":"f","points":[[0,0],[10,0],[10,10]],"spec":{"coverage":0.8},"seed":13}]}
            """));

        await Assert.That(props.Count).IsEqualTo(4);
        await Assert.That(((PathProp)props[0]).Style).IsEqualTo(PathStyle.Stones);
        await Assert.That(((TreeProp)props[1]).Species).IsEqualTo("birch");
        await Assert.That(((BoulderProp)props[2]).Form).IsEqualTo(BoulderForm.Cairn);
        await Assert.That(((FloraProp)props[3]).Spec.Coverage).IsEqualTo(0.8);
    }

    [Test]
    public async Task The_maps_symmetry_is_what_the_pass_fans_props_through()
    {
        var symmetry = DressingScope.SymmetryOf(Layout(""));
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

    private static int Height(string svg)
        => int.Parse(Regex.Match(svg, "height='(\\d+)'").Groups[1].Value);

    [Test]
    public async Task The_preview_places_the_prop_rather_than_drawing_an_impression_of_it()
    {
        var views = DressingPreview.Views(
            new FloraProp { Points = [[0, 0], [40, 0], [40, 40], [0, 40]], Spec = new FloraSpec(Coverage: 0.9, FlowerShare: 0.5), Seed = 7 },
            TerrainTheme.Default);

        await Assert.That(views.Counts.Plants).IsGreaterThan(100);
        await Assert.That(views.Counts.Trees).IsEqualTo(0);
        // Flowers are the point of a flower field, so the picture has to carry their colours and not one
        // generic plant green.
        await Assert.That(Fills(views.Plan)).Contains(BlockPalette.Hex(DressingPalette.RedFlower, 0));
        await Assert.That(Fills(views.Plan)).Contains(BlockPalette.Hex(DressingPalette.YellowFlower, 0));
    }

    [Test]
    public async Task A_prop_is_re_centred_on_the_sample_so_a_card_shows_it_wherever_it_was_placed()
    {
        // A tree placed at (900, -400) on a real map must still be the thing in the middle of its own card.
        var far = DressingPreview.Views(new TreeProp { X = 900, Z = -400, Species = "oak", Seed = 5 }, TerrainTheme.Default);
        await Assert.That(far.Counts.Trees).IsEqualTo(1);
    }

    [Test]
    public async Task The_theme_underneath_is_what_decides_whether_anything_grows()
    {
        var meadow = new FloraProp { Points = [[0, 0], [40, 0], [40, 40], [0, 40]], Spec = new FloraSpec(Coverage: 0.8), Seed = 7 };
        var paved = TerrainTheme.Default with { Surface = new TopBand(new SolidMaterial(Blocks.QuartzBlock), 1) };

        await Assert.That(DressingPreview.Views(meadow, TerrainTheme.Default).Counts.Plants).IsGreaterThan(0);
        await Assert.That(DressingPreview.Views(meadow, paved).Counts.Plants).IsEqualTo(0);
    }

    [Test]
    public async Task The_section_crops_to_what_is_there_so_a_path_and_a_tree_read_at_the_same_scale()
    {
        // A fixed sky would draw a path as one grey line under forty courses of nothing.
        var path = DressingPreview.Views(new PathProp
        {
            Points = [[0, 20], [40, 20]], Radius = 3, Seed = 5, Blocks = [new PaveBlock(Blocks.Gravel, 0)],
        }, TerrainTheme.Default);
        var tree = DressingPreview.Views(new TreeProp { Species = "spruce", Height = 24, Seed = 5 }, TerrainTheme.Default);

        await Assert.That(path.Counts.PathCells).IsGreaterThan(50);
        await Assert.That(Height(tree.Section)).IsGreaterThan(Height(path.Section));
    }

    [Test]
    public async Task Every_picker_offers_exactly_what_the_pass_can_build()
    {
        // The cards are the real algorithm at card size, so a picker can never promise a look the export does
        // not produce — which is only true if every option actually draws something.
        var styles = DressingPreview.PathStyleCards(
            new PathProp { Radius = 3, Seed = 5, Blocks = [new PaveBlock(Blocks.Gravel, 0)] }, TerrainTheme.Default);
        var forms = DressingPreview.BoulderFormCards(new BoulderProp { Size = 3, Seed = 3 }, TerrainTheme.Default);
        var species = DressingPreview.SpeciesCards(TerrainTheme.Default);

        await Assert.That(styles.Select(card => card.Key))
            .IsEquivalentTo(Enum.GetValues<PathStyle>().Select(style => style.ToString().ToLowerInvariant()));
        await Assert.That(forms.Count).IsEqualTo(4);
        await Assert.That(species.Select(card => card.Key)).IsEquivalentTo(DressingPalette.Species.Select(row => row.Name));

        foreach (var card in styles.Concat(forms).Concat(species))
        {
            await Assert.That(card.Svg).Contains("<rect");
            await Assert.That(card.Label).IsNotEmpty();
        }
    }
}

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
            Points = [[0, 20], [40, 20]], Radius = 3, Seed = 5, Pave = new SolidMaterial(Blocks.Gravel),
        }, TerrainTheme.Default);
        var tree = DressingPreview.Views(new TreeProp { Species = "spruce", Height = 24, Seed = 5 }, TerrainTheme.Default);

        await Assert.That(path.Counts.PathCells).IsGreaterThan(50);
        await Assert.That(Height(tree.Section)).IsGreaterThan(Height(path.Section));
    }

    [Test]
    public async Task Every_card_in_a_set_is_cropped_the_same_way_so_their_floors_line_up()
    {
        // Cropping each card to its own content puts every floor at a different height once they sit in a
        // grid, and makes a spruce and an acacia look the same size. One crop per set fixes both: the tallest
        // option decides the top, the ground decides the bottom, and the heights compare honestly.
        var species = DressingPreview.SpeciesCards(TerrainTheme.Default);
        var forms = DressingPreview.BoulderFormCards(new BoulderProp { Size = 3, Seed = 3 }, TerrainTheme.Default);

        await Assert.That(species.Select(card => Height(card.Svg)).Distinct().Count()).IsEqualTo(1);
        await Assert.That(forms.Select(card => Height(card.Svg)).Distinct().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task The_side_view_is_a_projection_so_a_crown_reads_whole_rather_than_speckled()
    {
        // A cut through one row meets a crown wherever that row falls — as often through the air between leaf
        // clusters as through them. Projecting every row is what makes the silhouette the shape it is.
        var tree = DressingPreview.Views(new TreeProp { Species = "oak", Height = 20, Seed = 5 }, TerrainTheme.Default);

        // Depth shades a block without repainting it, so a projected crown is many shades of the one leaf
        // colour — where a cut would give one flat shade with holes through it.
        //
        // A crown fill is recognised by asking the palette to produce it: a shade the view can emit is
        // Hex(leaf, depth) for some depth, so reproducing the fill from the shade function IS the membership
        // test. Guessing instead — "green-dominant, same channels at zero" — reads the ground as crown the
        // moment grass and oak leaves are near hues, which is what a colour table sharpened against the real
        // textures made them.
        var front = BlockPalette.Hex(Blocks.Leaves, DressingPalette.LeafNoDecay, 0);
        var leafShades = Enumerable.Range(0, 1001)
            .Select(step => BlockPalette.Hex(Blocks.Leaves, DressingPalette.LeafNoDecay, step / 1000.0))
            .ToHashSet();

        var crown = Fills(tree.Section).Where(leafShades.Contains).ToList();
        await Assert.That(crown.Count).IsGreaterThan(3);
        // And every one of them is still that leaf, darkened: no shade is brighter than the block's own.
        await Assert.That(crown.All(hex => Green(hex) <= Green(front))).IsTrue();
        // The ground is not the crown: grass shares the leaf's hue but no shade of a leaf is ever that bright.
        await Assert.That(crown).DoesNotContain(BlockPalette.Hex(Blocks.Grass, 0));

        static int Green(string hex) => Convert.ToInt32(hex[3..5], 16);
    }

    [Test]
    public async Task Both_views_look_inside_the_sample_rather_than_at_the_wall_around_it()
    {
        // The painter finishes a footprint's perimeter as an edge — a rim course over a wall — so the sample's
        // outermost ring is its own boundary, not ground. Seen from the side that ring is the entire front
        // face, and a preview that included it showed a tree standing behind a wall instead of in the grass
        // the plan view plainly draws under it. Both views carry the same ground.
        var tree = DressingPreview.Views(new TreeProp { Species = "oak", Height = 20, Seed = 5 }, TerrainTheme.Default);

        var ground = BlockPalette.Hex(Blocks.Grass, 0);
        await Assert.That(Fills(tree.Plan)).Contains(ground);
        await Assert.That(Fills(tree.Section)).Contains(ground);
    }

    [Test]
    public async Task The_wood_cards_change_the_colour_and_nothing_else()
    {
        // A wood is a material, so six wood cards are one tree six times. If they differed in shape the picker
        // would be claiming the wood decides the silhouette, which is the claim the species picker makes and
        // this one must not.
        var woods = DressingPreview.WoodCards(new TreeProp { Form = TreeForm.Grown, Height = 18, Seed = 5 }, TerrainTheme.Default);

        await Assert.That(woods.Select(card => card.Key)).IsEquivalentTo(DressingPalette.Woods.Select(wood => wood.Name));
        await Assert.That(woods.Select(card => Cells(card.Svg)).Distinct().Count()).IsEqualTo(1);
        await Assert.That(woods.Select(card => card.Svg).Distinct().Count()).IsEqualTo(woods.Count);

        // How many blocks a card draws — the same tree in another wood fills the same cells.
        static int Cells(string svg) => Regex.Matches(svg, "<rect").Count;
    }

    [Test]
    public async Task A_species_is_a_silhouette_and_not_a_colour()
    {
        // The inverse claim, and the reason the two pickers are two pickers: the species cards must differ in
        // shape, or six of them are one tree wearing six palettes.
        var species = DressingPreview.SpeciesCards(TerrainTheme.Default);

        await Assert.That(species.Select(card => Regex.Matches(card.Svg, "<rect").Count).Distinct().Count())
            .IsGreaterThan(4);
    }

    [Test]
    public async Task Every_picker_offers_exactly_what_the_pass_can_build()
    {
        // The cards are the real algorithm at card size, so a picker can never promise a look the export does
        // not produce — which is only true if every option actually draws something.
        var styles = DressingPreview.PathStyleCards(
            new PathProp { Radius = 3, Seed = 5, Pave = new SolidMaterial(Blocks.Gravel) }, TerrainTheme.Default);
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

using PgmStudio.Domain;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Painting;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The dressing pass (G161, docs/world-export/decoration.md): the third walk over a realized world, adding the
/// terrain's life on top of the paint. The cases that matter are the ones a preview cannot show — that a prop
/// lands where it was placed and nowhere else, that a path repaints rather than builds, that the paint
/// underneath really gates flora, that protected ground is really left alone, and above all that a prop lands
/// identically for every team.
/// </summary>
public sealed class DecoratorTests
{
    /// <summary>A flat painted plateau: <paramref name="size"/> square of grass over stone, its first air
    /// course at y = 8 — the same shape the painter hands the pass in a real export.</summary>
    private static (VoxelWorld World, Dictionary<(int X, int Z), int> SurfaceTop) Plateau(
        int size = 40, int surfaceBlock = Blocks.Grass, int from = 0)
    {
        var world = new VoxelWorld();
        var top = new Dictionary<(int X, int Z), int>();
        for (var z = from; z < from + size; z++)
        for (var x = from; x < from + size; x++)
        {
            for (var y = 0; y < 7; y++) world.SetBlock(x, y, z, Blocks.Stone);
            world.SetBlock(x, 7, z, surfaceBlock);
            top[(x, z)] = 8;
        }
        return (world, top);
    }

    private static DressingContext Context(
        Dictionary<(int X, int Z), int> top, IReadOnlyList<PlacedProp> props,
        Func<int, int, bool>? isProtected = null, string? symmetry = null, double centerX = 0, double centerZ = 0)
        => new(top, props, isProtected ?? ((_, _) => false), new DressingSymmetry(symmetry, centerX, centerZ));

    private static IEnumerable<(int X, int Y, int Z, int Id, int Data)> Placed(
        VoxelWorld world, IEnumerable<(int X, int Z)> cells, int fromY, int toY)
    {
        foreach (var (x, z) in cells)
        for (var y = fromY; y <= toY; y++)
        {
            var (id, data) = world.GetBlock(x, y, z);
            if (id != Blocks.Air) yield return (x, y, z, id, data);
        }
    }

    // A square area covering the whole plateau — what "grow cover here" looks like when the case under test is
    // about something other than where the area's edge falls.
    private static double[][] AreaOver(int size) => [[0, 0], [size - 1, 0], [size - 1, size - 1], [0, size - 1]];

    // ── nothing by default ─────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task An_undressed_map_is_left_exactly_as_the_painter_left_it()
    {
        var (world, top) = Plateau();
        var placed = Decorator.Decorate(world, Context(top, []));

        await Assert.That(placed).IsEqualTo(new DressingPlacement());
        await Assert.That(Placed(world, top.Keys, 8, 40)).IsEmpty();
    }

    // ── a prop stands where it was placed ──────────────────────────────────────────────────────────
    [Test]
    public async Task A_tree_grows_where_it_was_placed_and_nowhere_else()
    {
        // The whole point of the rework: dressing is authored, so a tree is at (14, 20) because someone put it
        // there. Nothing anywhere else on a forty-block plateau.
        var (world, top) = Plateau();
        var tally = Decorator.Decorate(world, Context(top,
            [new TreeProp { Id = "t", X = 14, Z = 20, Species = "oak", Height = 16, Seed = 5 }]));

        await Assert.That(tally.Trees).IsEqualTo(1);
        var logs = Placed(world, top.Keys, 8, 40).Where(b => b.Id == Blocks.Log).ToList();
        await Assert.That(logs).IsNotEmpty();
        await Assert.That(logs.Min(b => b.Y)).IsEqualTo(8);                  // seated on the surface, not sunk
        // and standing at its own cell: limbs reach out, but the whole tree is within a crown's radius of
        // where it was put, not somewhere else on a forty-block plateau.
        await Assert.That(logs.Max(b => Math.Abs(b.X - 14))).IsLessThan(9);
        await Assert.That(logs.Max(b => Math.Abs(b.Z - 20))).IsLessThan(9);
    }

    // ── the measured crown radius the point-and-radius foliage render reads ───────────────────────────
    [Test]
    [Arguments("oak", TreeForm.Template, 16.0)]
    [Arguments("spruce", TreeForm.Template, 20.0)]
    [Arguments("birch", TreeForm.Grown, 18.0)]
    public async Task Canopy_radius_never_falls_short_of_the_crown_the_tree_actually_builds(
        string wood, TreeForm form, double height)
    {
        // Decorator.CanopyRadius is read before any world exists — a caller placing the point-and-radius render
        // has no build to measure. This is the check that the number it answers with is honest: no real leaf
        // block, in the tree the same prop actually stamps, stands further from the trunk than it claims.
        var tree = new TreeProp { Id = "t", X = 20, Z = 20, Form = form, Species = wood, Wood = wood, Height = height, Seed = 7 };
        var (world, top) = Plateau();
        Decorator.Decorate(world, Context(top, [tree]));

        var radius = Decorator.CanopyRadius(tree);
        var leaves = Placed(world, top.Keys, 8, 40).Where(b => b.Id is Blocks.Leaves or Blocks.Leaves2).ToList();

        await Assert.That(leaves).IsNotEmpty();
        await Assert.That(radius).IsGreaterThan(0);
        foreach (var leaf in leaves)
        {
            var reach = Math.Sqrt(Math.Pow(leaf.X - tree.X, 2) + Math.Pow(leaf.Z - tree.Z, 2));
            await Assert.That(reach).IsLessThanOrEqualTo(radius);
        }
    }

    [Test]
    public async Task A_taller_tree_of_the_same_species_reads_a_larger_canopy_radius()
    {
        // The measured figure tracks what the tree is actually asked to be, which a species-nominal constant
        // could not — the same species at two heights builds two different crowns.
        var small = new TreeProp { Id = "t", X = 0, Z = 0, Species = "oak", Height = 8, Seed = 3 };
        var large = new TreeProp { Id = "t", X = 0, Z = 0, Species = "oak", Height = 30, Seed = 3 };

        await Assert.That(Decorator.CanopyRadius(large)).IsGreaterThan(Decorator.CanopyRadius(small));
    }

    [Test]
    public async Task The_two_tree_forms_build_two_different_trees()
    {
        // They are different things, not settings of one thing. A vanilla spruce is a notched cone on a
        // straight trunk; the grower has no such profile in it, and asking it for one gets its own crown in
        // spruce blocks. Six grower presets named after species is exactly what this rules out.
        var (vanilla, vanillaTop) = Plateau();
        Decorator.Decorate(vanilla, Context(vanillaTop,
            [new TreeProp { Id = "t", X = 20, Z = 20, Form = TreeForm.Template, Species = "spruce", Height = 15, Seed = 5 }]));

        var (grown, grownTop) = Plateau();
        Decorator.Decorate(grown, Context(grownTop,
            [new TreeProp { Id = "t", X = 20, Z = 20, Form = TreeForm.Grown, Wood = "spruce", Height = 15, Seed = 5 }]));

        // Same wood in both — the material is the one thing a form does not decide. The wood is the low two data
        // bits; the rest carry the all-bark orientation, so it is masked off to read the species.
        var vanillaLogs = Logs(vanilla, vanillaTop);
        var grownLogs = Logs(grown, grownTop);
        await Assert.That(vanillaLogs.All(b => (b.Data & 3) == 1)).IsTrue();
        await Assert.That(grownLogs.All(b => (b.Data & 3) == 1)).IsTrue();

        // A vanilla trunk is one straight column; a grown one wanders and throws limbs, so it occupies many.
        await Assert.That(Columns(vanillaLogs)).IsEqualTo(1);
        await Assert.That(Columns(grownLogs)).IsGreaterThan(3);

        static List<(int X, int Y, int Z, int Id, int Data)> Logs(
            VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> top)
            => [.. Placed(world, top.Keys, 8, 40).Where(b => b.Id == Blocks.Log)];
        static int Columns(List<(int X, int Y, int Z, int Id, int Data)> logs)
            => logs.Select(b => (b.X, b.Z)).Distinct().Count();
    }

    [Test]
    public async Task Every_vanilla_species_builds_a_silhouette_of_its_own()
    {
        // The picker offers six species, so six crowns have to come out — and what separates them is where a
        // crown carries its width. A conifer is widest where it meets the trunk and ends in a spire; an acacia
        // is a flat disc, wider than anything else and only a few courses deep; a blob is widest in between.
        var spruce = Crown("spruce");
        var acacia = Crown("acacia");
        var oak = Crown("oak");

        await Assert.That(spruce[0]).IsEqualTo(spruce.Max());
        await Assert.That(spruce[^1]).IsLessThan(spruce[0]);

        await Assert.That(acacia.Count).IsLessThanOrEqualTo(3);
        await Assert.That(acacia.Max()).IsGreaterThan(oak.Max());

        await Assert.That(oak[0]).IsLessThan(oak.Max());
        await Assert.That(oak[^1]).IsLessThan(oak.Max());

        // How wide the crown is at each of its courses, bottom first.
        List<int> Crown(string name)
        {
            var species = DressingPalette.Species.First(row => row.Name == name);
            var (world, top) = Plateau();
            Decorator.Decorate(world, Context(top,
                [new TreeProp { Id = "t", X = 20, Z = 20, Species = name, Height = species.Height, Seed = 5 }]));

            return [.. Placed(world, top.Keys, 8, 40).Where(b => b.Id is Blocks.Leaves or Blocks.Leaves2)
                .GroupBy(b => b.Y).OrderBy(course => course.Key)
                .Select(course => course.Max(b => b.X) - course.Min(b => b.X) + 1)];
        }
    }

    [Test]
    public async Task A_vanilla_tree_comes_out_the_height_it_was_asked_for()
    {
        // Height is the number in the inspector, so a tree that overshot it would make the slider a lie — and
        // a tree's height is a sightline, which is a thing about how the map plays.
        foreach (var species in DressingPalette.Species)
        foreach (var height in (double[])[8, 14, 22])
        {
            var (world, top) = Plateau();
            Decorator.Decorate(world, Context(top,
                [new TreeProp { Id = "t", X = 20, Z = 20, Species = species.Name, Height = height, Seed = 5 }]));

            var tree = Placed(world, top.Keys, 8, 40).ToList();
            var courses = tree.Max(b => b.Y) - 8 + 1;
            await Assert.That(Math.Abs(courses - height)).IsLessThanOrEqualTo(2);
        }
    }

    [Test]
    public async Task A_boulder_is_half_buried_so_it_reads_as_emerging_from_the_ground()
    {
        var (world, top) = Plateau();
        var tally = Decorator.Decorate(world, Context(top,
            [new BoulderProp { Id = "b", X = 20, Z = 20, Size = 3, Mossy = false, Seed = 3 }]));

        await Assert.That(tally.Boulders).IsEqualTo(1);
        var rock = Placed(world, top.Keys, 4, 20).Where(b => b.Id == Blocks.Stone && b.Y >= 7).ToList();
        await Assert.That(rock.Any(b => b.Y >= 9)).IsTrue();                 // it stands above the ground
        await Assert.That(world.GetBlock(20, 6, 20).Id).IsEqualTo(Blocks.Stone);   // and reaches into it
    }

    [Test]
    public async Task Every_leaf_carries_the_no_decay_bit()
    {
        // A leaf placed without the flag is checked for decay and the crown falls apart the moment a player
        // joins — a built map has no growing tree behind it.
        var (world, top) = Plateau();
        Decorator.Decorate(world, Context(top, [new TreeProp { Id = "t", X = 20, Z = 20, Seed = 5 }]));

        var leaves = Placed(world, top.Keys, 8, 40).Where(b => b.Id == Blocks.Leaves).ToList();
        await Assert.That(leaves).IsNotEmpty();
        await Assert.That(leaves.All(b => (b.Data & DressingPalette.LeafNoDecay) != 0)).IsTrue();
    }

    [Test]
    public async Task Every_log_is_the_all_bark_variant()
    {
        // A built tree's wood is scenery, so it wears bark on every face rather than the pale end grain of an
        // upright log wherever a limb turns. The wood the log paints as still reads through the low two bits.
        var (world, top) = Plateau();
        Decorator.Decorate(world, Context(top,
            [new TreeProp { Id = "t", X = 20, Z = 20, Form = TreeForm.Grown, Wood = "birch", Height = 16, Seed = 5 }]));

        var logs = Placed(world, top.Keys, 8, 40).Where(b => b.Id == Blocks.Log).ToList();
        await Assert.That(logs).IsNotEmpty();
        await Assert.That(logs.All(b => (b.Data & DressingPalette.LogAllBark) == DressingPalette.LogAllBark)).IsTrue();
        await Assert.That(logs.All(b => (b.Data & 3) == 2)).IsTrue();   // still birch
    }

    // ── paths repaint, they do not build ───────────────────────────────────────────────────────────
    [Test]
    public async Task A_path_swaps_the_surface_it_crosses_and_adds_no_cell()
    {
        // The distinction the phase is built on: terrain is the draw phase's, a finish is dressing's. A path
        // that added a cell would lift itself into a kerb and would belong in the other phase.
        var (world, top) = Plateau();
        var tally = Decorator.Decorate(world, Context(top, [new PathProp
        {
            Id = "p", Points = [[4, 20], [35, 20]], Radius = 2, Seed = 5,
            Pave = new SolidMaterial(Blocks.Gravel),
        }]));

        await Assert.That(tally.PathCells).IsGreaterThan(60);
        await Assert.That(Placed(world, top.Keys, 8, 40)).IsEmpty();          // nothing above the surface
        await Assert.That(world.GetBlock(20, 7, 20).Id).IsEqualTo(Blocks.Gravel);
        await Assert.That(world.GetBlock(20, 7, 30).Id).IsEqualTo(Blocks.Grass);   // clear of the stroke
    }

    [Test]
    public async Task The_style_is_what_separates_a_road_from_a_trail_from_stones()
    {
        // Three gates on one distance field. Solid paves its whole band; worn thins it; stones leave the gaps
        // that make them stones — which is exactly what a single closed outline could not express.
        int Paved(PathStyle style, double coverage = 0.7)
        {
            var (world, top) = Plateau();
            return Decorator.Decorate(world, Context(top, [new PathProp
            {
                Id = "p", Points = [[4, 20], [35, 20]], Radius = 2, Style = style, Coverage = coverage, Seed = 5,
                Pave = new SolidMaterial(Blocks.Gravel),
            }])).PathCells;
        }

        var solid = Paved(PathStyle.Solid);
        await Assert.That(Paved(PathStyle.Worn, 0.5)).IsLessThan(solid);
        await Assert.That(Paved(PathStyle.Stones)).IsLessThan(solid);
        await Assert.That(Paved(PathStyle.Stones)).IsGreaterThan(0);
        await Assert.That(Paved(PathStyle.Tapered)).IsLessThan(solid);       // thin at both ends
    }

    [Test]
    public async Task A_patterned_pave_spends_every_material_it_was_given()
    {
        // The paving is a terrain material, resolved cell by cell — so a cobbled road is a cell pattern at a
        // small patch size rather than a mode of the stroke, and every material in it reaches the ground.
        var (world, top) = Plateau();
        Decorator.Decorate(world, Context(top, [new PathProp
        {
            Id = "p", Points = [[4, 20], [35, 20]], Radius = 3, Seed = 5,
            Pave = new CellMaterial(5, 3, 100, 0,
            [
                new SolidMaterial(Blocks.Cobblestone), new SolidMaterial(Blocks.Gravel), new SolidMaterial(Blocks.Stone),
            ]),
        }]));

        var paved = top.Keys.Select(cell => world.GetBlock(cell.X, 7, cell.Z).Id).ToHashSet();
        await Assert.That(paved).Contains(Blocks.Cobblestone);
        await Assert.That(paved).Contains(Blocks.Gravel);
        await Assert.That(paved).Contains(Blocks.Stone);
    }

    [Test]
    public async Task A_path_does_not_eat_what_the_map_is_played_through()
    {
        // A monument's wool standing on a column the stroke crosses. The painter skips a stamped surface for
        // the same reason, and a route has even less claim on one.
        var (world, top) = Plateau();
        world.SetBlock(20, 7, 20, Blocks.Wool, 14);

        Decorator.Decorate(world, Context(top, [new PathProp
        {
            Id = "p", Points = [[4, 20], [35, 20]], Radius = 2, Seed = 5, Pave = new SolidMaterial(Blocks.Gravel),
        }]));

        await Assert.That(world.GetBlock(20, 7, 20).Id).IsEqualTo(Blocks.Wool);
    }

    [Test]
    public async Task Nothing_grows_through_a_road()
    {
        // The one ordering rule the pass has: paths go down first and their cells become bare ground.
        var (world, top) = Plateau();
        Decorator.Decorate(world, Context(top,
        [
            new PathProp { Id = "p", Points = [[4, 20], [35, 20]], Radius = 3, Seed = 5, Pave = new SolidMaterial(Blocks.Gravel) },
            new FloraProp { Id = "f", Points = AreaOver(40), Spec = new FloraSpec(Coverage: 1.0), Seed = 7 },
        ]));

        var overRoad = Placed(world, [(20, 20), (18, 20), (24, 20)], 8, 10).ToList();
        await Assert.That(overRoad).IsEmpty();
    }

    // ── water carves and fills ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_channel_cuts_a_bed_and_fills_it_with_water()
    {
        // The distinction water is built on: a path repaints the surface, water takes the surface *out*. So the
        // centerline is water down through several courses, over a bed floor — not a single repainted block.
        var (world, top) = Plateau();
        var tally = Decorator.Decorate(world, Context(top, [new WaterProp
        {
            Id = "w", Points = [[4, 20], [35, 20]], Radius = 4, Depth = 3, Seed = 5, Shore = 0,
            Bank = new SolidMaterial(Blocks.Sand),
        }]));

        await Assert.That(tally.WaterCells).IsGreaterThan(60);
        await Assert.That(world.GetBlock(20, 7, 20).Id).IsEqualTo(Blocks.StationaryWater);   // the old surface, now water
        await Assert.That(world.GetBlock(20, 6, 20).Id).IsEqualTo(Blocks.StationaryWater);   // cut deeper on the line
        await Assert.That(world.GetBlock(20, 4, 20).Id).IsEqualTo(Blocks.Sand);              // a sand bed under it
        await Assert.That(world.GetBlock(20, 7, 34).Id).IsEqualTo(Blocks.Grass);             // clear of the channel + its beach
    }

    [Test]
    public async Task A_channels_bank_is_a_material_the_shallows_and_the_beach_share()
    {
        // The bed floor and the shore are one material, not one block — a voronoi of sand, gravel and coarse
        // dirt by default. So the floor under the water and the beach beside it are drawn from the same palette.
        var (world, top) = Plateau();
        var bank = new HashSet<int> { Blocks.Sand, Blocks.Gravel, Blocks.Dirt };
        Decorator.Decorate(world, Context(top, [new WaterProp
        {
            Id = "w", Points = [[4, 20], [35, 20]], Radius = 4, Depth = 3, Shore = 4, Seed = 5,
        }]));

        // The bed floor under the centerline is one of the bank's blocks.
        await Assert.That(bank).Contains(world.GetBlock(20, 4, 20).Id);
        // A beach was laid: some grass columns off the water became bank material on the surface, and the water
        // itself is untouched grass nowhere near it.
        var beach = top.Keys.Count(cell => bank.Contains(world.GetBlock(cell.X, 7, cell.Z).Id)
            && world.GetBlock(cell.X, 7, cell.Z).Id != Blocks.StationaryWater);
        await Assert.That(beach).IsGreaterThan(0);
    }

    [Test]
    public async Task With_no_beach_the_water_meets_the_grass_at_its_edge()
    {
        // Shore 0 is a valid channel: a hard bank, no sand. Nothing but water and the bed is written, so the
        // grass runs right up to the water.
        var (world, top) = Plateau();
        Decorator.Decorate(world, Context(top, [new WaterProp
        {
            Id = "w", Points = [[4, 20], [35, 20]], Radius = 3, Depth = 3, Shore = 0, Seed = 5,
            Bank = new SolidMaterial(Blocks.Sand),
        }]));

        // No sand on the surface anywhere — the bank only ever appears as the bed floor, below the water.
        var surfaceSand = top.Keys.Count(cell => world.GetBlock(cell.X, 7, cell.Z).Id == Blocks.Sand);
        await Assert.That(surfaceSand).IsEqualTo(0);
    }

    [Test]
    public async Task Water_replaces_only_existing_terrain_and_never_floats()
    {
        // The rule the tool has to keep: it lowers the ground and fills the hollow, but it writes nothing into
        // what was already air. So there is no water above the old surface anywhere on the plateau.
        var (world, top) = Plateau();
        Decorator.Decorate(world, Context(top, [new WaterProp
        {
            Id = "w", Points = [[4, 20], [35, 20]], Radius = 4, Depth = 3, Seed = 5,
        }]));

        await Assert.That(Placed(world, top.Keys, 8, 40)).IsEmpty();   // nothing stands above the surface
    }

    [Test]
    public async Task A_channel_over_a_void_leaves_the_void_alone()
    {
        // A column the surface map does not carry is a hole the terrain left, and water fills a bed cut into
        // ground — not one hung across a gap. The cell is skipped rather than floored with water.
        var (world, top) = Plateau();
        top.Remove((20, 20));
        Decorator.Decorate(world, Context(top, [new WaterProp
        {
            Id = "w", Points = [[4, 20], [35, 20]], Radius = 4, Depth = 3, Seed = 5,
        }]));

        for (var y = 0; y <= 20; y++)
            await Assert.That(world.GetBlock(20, y, 20).Id).IsNotEqualTo(Blocks.StationaryWater);
    }

    [Test]
    public async Task Water_does_not_eat_what_the_map_is_played_through()
    {
        // A monument's wool on a column the channel crosses. Water has no more claim on a stamp than a path does.
        var (world, top) = Plateau();
        world.SetBlock(20, 7, 20, Blocks.Wool, 14);

        Decorator.Decorate(world, Context(top, [new WaterProp
        {
            Id = "w", Points = [[4, 20], [35, 20]], Radius = 4, Depth = 3, Seed = 5,
        }]));

        await Assert.That(world.GetBlock(20, 7, 20).Id).IsEqualTo(Blocks.Wool);
    }

    [Test]
    public async Task A_channel_is_mirrored_as_a_whole_route_for_every_team()
    {
        var (world, top) = Plateau(80, from: -40);
        var tally = Decorator.Decorate(world, Context(top, [new WaterProp
        {
            Id = "w", Points = [[6, 6], [20, 14], [30, 8]], Radius = 3, Depth = 3, Seed = 5,
        }], symmetry: "rot_180"));

        await Assert.That(tally.WaterCells).IsGreaterThan(80);
        var wet = top.Keys.Where(cell => world.GetBlock(cell.X, 7, cell.Z).Id == Blocks.StationaryWater).ToHashSet();
        var unmirrored = wet.Count(cell => !wet.Contains((-cell.X - 1, -cell.Z - 1)));
        await Assert.That(unmirrored).IsEqualTo(0);
    }

    // ── areas of cover ─────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Cover_grows_inside_the_drawn_area_and_stops_at_its_edge()
    {
        var (world, top) = Plateau();
        var tally = Decorator.Decorate(world, Context(top,
            [new FloraProp { Id = "f", Points = [[4, 4], [16, 4], [16, 16], [4, 16]], Spec = new FloraSpec(Coverage: 1.0), Seed = 7 }]));

        await Assert.That(tally.Plants).IsGreaterThan(50);
        await Assert.That(Placed(world, [(10, 10)], 8, 9)).IsNotEmpty();      // inside
        await Assert.That(Placed(world, [(30, 30), (2, 2), (25, 10)], 8, 9)).IsEmpty();   // outside
    }

    [Test]
    public async Task The_paint_underneath_is_what_decides_whether_cover_grows()
    {
        var lush = Plateau(20, Blocks.Grass);
        var paved = Plateau(20, Blocks.QuartzBlock);
        var area = new FloraProp { Id = "f", Points = AreaOver(20), Spec = new FloraSpec(Coverage: 1.0), Seed = 7 };

        await Assert.That(Decorator.Decorate(lush.World, Context(lush.SurfaceTop, [area])).Plants).IsGreaterThan(50);
        await Assert.That(Decorator.Decorate(paved.World, Context(paved.SurfaceTop, [area])).Plants).IsEqualTo(0);
    }

    [Test]
    public async Task A_two_block_plant_is_placed_as_a_pair_with_its_top_half_flagged()
    {
        // Without the upper-half flag a double plant drops as an item on the first block update.
        var (world, top) = Plateau();
        Decorator.Decorate(world, Context(top,
            [new FloraProp { Id = "f", Points = AreaOver(40), Spec = new FloraSpec(Coverage: 1.0, TallShare: 1.0, FlowerShare: 0), Seed = 7 }]));

        var tall = Placed(world, top.Keys, 8, 8).Where(b => b.Id == DressingPalette.DoublePlant).ToList();
        await Assert.That(tall).IsNotEmpty();
        foreach (var stem in tall.Take(20))
            await Assert.That(world.GetBlock(stem.X, stem.Y + 1, stem.Z))
                .IsEqualTo((DressingPalette.DoublePlant, DressingPalette.DoublePlantUpper));
    }

    // ── what must be left bare ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Nothing_is_placed_on_ground_the_map_is_played_through()
    {
        var (world, top) = Plateau();
        var tally = Decorator.Decorate(world, Context(top,
        [
            new TreeProp { Id = "t", X = 20, Z = 20, Seed = 5 },
            new BoulderProp { Id = "b", X = 20, Z = 20, Size = 3, Seed = 3 },
            new FloraProp { Id = "f", Points = AreaOver(40), Spec = new FloraSpec(Coverage: 1.0), Seed = 7 },
        ], isProtected: (x, z) => Math.Abs(x - 20) < 8 && Math.Abs(z - 20) < 8));

        await Assert.That(tally.Trees).IsEqualTo(0);
        await Assert.That(tally.Boulders).IsEqualTo(0);
        await Assert.That(Placed(world, [(20, 20), (16, 18)], 8, 40)).IsEmpty();
    }

    [Test]
    public async Task A_tree_may_lean_its_crown_over_a_drop()
    {
        // What a prop *rests* on needs ground; what it merely occupies does not, or no tree could grow within
        // a crown's reach of a shoreline or an island edge.
        var stand = new TreeProp { Id = "t", X = 7, Z = 7, Species = "oak", Seed = 5 };
        var (edge, edgeTop) = Plateau(14);

        await Assert.That(Decorator.Decorate(edge, Context(edgeTop, [stand])).Trees).IsEqualTo(1);
    }

    [Test]
    public async Task A_trees_crown_may_overhang_something_protected_but_its_trunk_may_not_root_in_it()
    {
        // Protection is decided on the same footprint ground and occupancy are (B78): the cells a tree
        // actually rests on, not the whole volume a tall crown happens to pass over. A trunk on a monument is
        // the fault; a canopy reaching over one at height is not — a hand-built map's trees overhang its
        // structures too, and testing the whole volume would make a tree taller by refusing to build it: a
        // wider crown claims more protected columns the taller it grows, which silently empties a forest.
        var stand = new TreeProp { Id = "t", X = 10, Z = 10, Species = "oak", Height = 20, Seed = 5 };

        var (open, openTop) = Plateau(20);
        await Assert.That(Decorator.Decorate(open, Context(openTop, [stand])).Trees).IsEqualTo(1);

        // Everything but a small core around the trunk is protected, so the crown necessarily overhangs it —
        // and the tree still stands, because its trunk never leaves the unprotected core.
        var (overhung, overhungTop) = Plateau(20);
        var overhanging = Decorator.Decorate(overhung, Context(overhungTop, [stand],
            isProtected: (x, z) => x is < 9 or > 11 || z is < 9 or > 11));
        await Assert.That(overhanging.Trees).IsEqualTo(1);

        // But a trunk asked to root on the protected column itself is still refused.
        var (rooted, rootedTop) = Plateau(20);
        var onProtection = Decorator.Decorate(rooted, Context(rootedTop, [stand],
            isProtected: (x, z) => x == 10 && z == 10));
        await Assert.That(onProtection.Trees).IsEqualTo(0);
    }

    // ── nothing stands inside anything else (B85) ──────────────────────────────────────────────────
    [Test]
    public async Task A_tree_does_not_root_where_a_building_already_stands()
    {
        // The building is placed first, so its cells are already claimed by the time the tree is considered.
        // Before B85 the pass only skipped the individual wood/leaf cells that landed on a non-air block,
        // which still let the rest of the tree stand half inside the walls; now the whole tree is refused.
        var (world, top) = Plateau();
        var house = new HouseProp { Id = "h", Wings = [new AuthoredWing([[16, 16], [24, 24]])], Style = new HouseStyle
        {
            Doorway = new Doorway
            {
                Door = DoorMaterial.Air,
            },
        } };
        var tree = new TreeProp { Id = "t", X = 20, Z = 20, Species = "oak", Height = 14, Seed = 5 };

        var tally = Decorator.Decorate(world, Context(top, [house, tree]));

        await Assert.That(tally.Houses).IsEqualTo(1);
        await Assert.That(tally.Trees).IsEqualTo(0);
        var logsInsideTheHouse = Placed(world, [(20, 20)], 8, 20).Where(b => b.Id == Blocks.Log);
        await Assert.That(logsInsideTheHouse).IsEmpty();
    }

    [Test]
    public async Task A_second_building_does_not_stand_where_the_first_one_does()
    {
        // Two authored rectangles that overlap are two buildings colliding, not one winning a race to write
        // the same blocks — the second is refused outright rather than raised through the first's walls.
        var (world, top) = Plateau();
        var first = new HouseProp { Id = "h1", Wings = [new AuthoredWing([[10, 10], [18, 18]])], Style = new HouseStyle
        {
            Doorway = new Doorway
            {
                Door = DoorMaterial.Air,
            },
        } };
        var second = new HouseProp { Id = "h2", Wings = [new AuthoredWing([[14, 14], [22, 22]])], Style = new HouseStyle
        {
            Doorway = new Doorway
            {
                Door = DoorMaterial.Air,
            },
        } };

        var tally = Decorator.Decorate(world, Context(top, [first, second]));

        await Assert.That(tally.Houses).IsEqualTo(1);
    }

    [Test]
    public async Task Two_boulders_do_not_share_the_ground_they_rest_on()
    {
        // The first boulder claims its footprint; the second, anchored at the same spot, has nowhere of its
        // own to rest and is refused rather than merging into (or overwriting) the first rock.
        var (world, top) = Plateau();
        var first = new BoulderProp { Id = "b1", X = 20, Z = 20, Size = 3, Mossy = false, Seed = 3 };
        var second = new BoulderProp { Id = "b2", X = 20, Z = 20, Size = 3, Mossy = false, Seed = 11 };

        var tally = Decorator.Decorate(world, Context(top, [first, second]));

        await Assert.That(tally.Boulders).IsEqualTo(1);
    }

    [Test]
    public async Task A_trees_canopy_may_still_overhang_a_building_it_does_not_root_in()
    {
        // Overlap is decided on the cells a prop *rests* on, the same footprint protection is decided on
        // (B78) — not on everything a tall crown happens to pass over. A trunk planted clear of a low building
        // still gets to spread its canopy over the roof, the way a real tree overhangs a shed beside it.
        var (world, top) = Plateau();
        var house = new HouseProp { Id = "h", Wings = [new AuthoredWing([[24, 16], [30, 24]])], Style = new HouseStyle
        {
            Doorway = new Doorway
            {
                Door = DoorMaterial.Air,
            },
        } };
        var tree = new TreeProp { Id = "t", X = 20, Z = 20, Species = "oak", Height = 16, Seed = 5 };

        var tally = Decorator.Decorate(world, Context(top, [house, tree]));

        await Assert.That(tally.Houses).IsEqualTo(1);
        await Assert.That(tally.Trees).IsEqualTo(1);
    }

    // ── fairness (G162) ────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_prop_lands_identically_for_every_team()
    {
        // Fanning the *position* is not enough and this is the case that shows it: a rock whose lobe points
        // east must point west on the mirrored half, or the cover each team gets differs cell by cell.
        var (world, top) = Plateau(80, from: -40);
        var tally = Decorator.Decorate(world, Context(top,
            [new BoulderProp { Id = "b", X = 12, Z = 9, Form = BoulderForm.Cairn, Size = 3, Mossy = false, Seed = 3 }],
            symmetry: "rot_180"));

        await Assert.That(tally.Boulders).IsEqualTo(2);
        var unmirrored = top.Keys.Count(cell =>
            Solid(world, cell) != Solid(world, (-cell.X - 1, -cell.Z - 1)));
        await Assert.That(unmirrored).IsEqualTo(0);

        static bool Solid(VoxelWorld world, (int X, int Z) cell)
        {
            for (var y = 8; y < 20; y++) if (world.GetBlock(cell.X, y, cell.Z).Id != Blocks.Air) return true;
            return false;
        }
    }

    [Test]
    public async Task A_path_is_mirrored_as_a_whole_route_rather_than_cell_by_cell()
    {
        var (world, top) = Plateau(80, from: -40);
        var tally = Decorator.Decorate(world, Context(top, [new PathProp
        {
            Id = "p", Points = [[6, 6], [20, 14], [30, 8]], Radius = 2, Seed = 5,
            Pave = new SolidMaterial(Blocks.Gravel),
        }], symmetry: "rot_180"));

        await Assert.That(tally.PathCells).IsGreaterThan(80);
        var paved = top.Keys.Where(cell => world.GetBlock(cell.X, 7, cell.Z).Id == Blocks.Gravel).ToHashSet();
        var unmirrored = paved.Count(cell => !paved.Contains((-cell.X - 1, -cell.Z - 1)));
        await Assert.That(unmirrored).IsEqualTo(0);
    }

    [Test]
    public async Task Without_symmetry_a_prop_is_placed_once()
    {
        var (world, top) = Plateau(80, from: -40);
        var tally = Decorator.Decorate(world, Context(top,
            [new BoulderProp { Id = "b", X = 12, Z = 9, Size = 3, Seed = 3 }]));

        await Assert.That(tally.Boulders).IsEqualTo(1);
    }

    [Test]
    public async Task A_prop_is_decided_once_for_its_whole_orbit_not_once_per_image()
    {
        // Only one of the two mirrored sites is protected. The old per-image "continue" would still raise the
        // other one, leaving a rock on one side of the board and nothing where its mirror should stand — the
        // exact asymmetry a mirrored map is not allowed to show. Deciding for the whole orbit drops both.
        var (world, top) = Plateau(80, from: -40);
        var tally = Decorator.Decorate(world, Context(top,
            [new BoulderProp { Id = "b", X = 12, Z = 9, Size = 3, Seed = 3 }],
            isProtected: (x, z) => Math.Abs(x - 12) < 3 && Math.Abs(z - 9) < 3,
            symmetry: "rot_180"));

        await Assert.That(tally.Boulders).IsEqualTo(0);
        await Assert.That(Placed(world, [(12, 9), (-13, -10)], 8, 20)).IsEmpty();
    }

    [Test]
    public async Task A_building_is_decided_once_for_its_whole_orbit_not_once_per_image()
    {
        // Same rule, the building's own version of it: an image whose ground is missing fails the whole orbit
        // rather than raising the building on one side of a mirrored map and leaving the other bare. Only the
        // rectangle the author actually drew loses its ground here — the old per-image "continue" would still
        // have raised the mirrored copy on the far side of the map with nothing standing opposite it.
        var (world, top) = Plateau(80, from: -40);
        for (var x = 4; x <= 10; x++)
            for (var z = 4; z <= 8; z++)
                top.Remove((x, z));

        var tally = Decorator.Decorate(world, Context(top,
            [new HouseProp { Id = "h", Wings = [new AuthoredWing([[4, 4], [10, 8]])], Style = new HouseStyle
            {
                Doorway = new Doorway
                {
                    Door = DoorMaterial.Air,
                },
            } }],
            symmetry: "rot_180"));

        await Assert.That(tally.Houses).IsEqualTo(0);
        // Nothing raised at the mirrored site either — the far corner from (4,4)/(10,8) under a 180° turn.
        var raised = Placed(world, [(-10, -8), (-4, -4), (-7, -6)], 8, 40);
        await Assert.That(raised).IsEmpty();
    }

    // ── determinism & the wire format ──────────────────────────────────────────────────────────────
    [Test]
    public async Task The_same_props_dress_the_same_world_the_same_way()
    {
        PlacedProp[] props =
        [
            new PathProp { Id = "p", Points = [[4, 20], [35, 20]], Radius = 2, Style = PathStyle.Worn, Seed = 5, Pave = new SolidMaterial(Blocks.Gravel) },
            new TreeProp { Id = "t", X = 12, Z = 12, Seed = 5 },
            new BoulderProp { Id = "b", X = 28, Z = 28, Seed = 3 },
            new FloraProp { Id = "f", Points = AreaOver(40), Spec = new FloraSpec(), Seed = 7 },
        ];

        var first = Plateau();
        var again = Plateau();
        var one = Decorator.Decorate(first.World, Context(first.SurfaceTop, props));
        var two = Decorator.Decorate(again.World, Context(again.SurfaceTop, props));

        await Assert.That(one).IsEqualTo(two);
        await Assert.That(Placed(first.World, first.SurfaceTop.Keys, 1, 40))
            .IsEquivalentTo(Placed(again.World, again.SurfaceTop.Keys, 1, 40));
    }

    [Test]
    public async Task Every_kind_of_prop_round_trips_through_its_json()
    {
        // One list holds four different shapes of thing, so the discriminator is what makes the wire format
        // readable at all — and a prop that lost its kind on the way back would be silently dropped.
        var doc = new DressingDoc
        {
            Props =
            [
                new PathProp { Id = "p", Points = [[1, 2], [3, 4]], Radius = 4, Style = PathStyle.Rough, Seed = 5,
                               Pave = new CellMaterial(5, 3, 100, 0, [new SolidMaterial(4), new SolidMaterial(13)]) },
                new TreeProp { Id = "t", X = 5, Z = 6, Species = "birch", Height = 22, Stems = 2, Seed = 9 },
                new BoulderProp { Id = "b", X = 7, Z = 8, Form = BoulderForm.Cairn, Size = 4, Seed = 11 },
                new FloraProp { Id = "f", Points = [[0, 0], [8, 0], [8, 8]], Spec = new FloraSpec(Coverage: 0.9), Seed = 13 },
            ],
        };

        var back = DressingJson.Deserialize(DressingJson.Serialize(doc));

        await Assert.That(back.Props.Select(prop => prop.GetType().Name))
            .IsEquivalentTo(doc.Props.Select(prop => prop.GetType().Name));
        await Assert.That(((PathProp)back.Props[0]).Style).IsEqualTo(PathStyle.Rough);
        await Assert.That(((PathProp)back.Props[0]).Pave).IsEqualTo(((PathProp)doc.Props[0]).Pave);
        await Assert.That(((TreeProp)back.Props[1]).Species).IsEqualTo("birch");
        await Assert.That(((BoulderProp)back.Props[2]).Form).IsEqualTo(BoulderForm.Cairn);
        await Assert.That(((FloraProp)back.Props[3]).Spec.Coverage).IsEqualTo(0.9);
    }

    // ── a path and a rock are finished with a material, like everything else ──────────────────────────
    /// <summary>A boulder's material is resolved in the boulder's <em>own</em> frame — offsets from its anchor,
    /// before it knows where on the map it goes. That is what makes a mirrored pair the same rock: resolving
    /// against map coordinates would give two teams the same shape in different colours, which is the thing the
    /// whole fan exists to prevent.</summary>
    [Test]
    public async Task A_mirrored_pair_of_patterned_rocks_are_the_same_rock()
    {
        var (world, top) = Plateau(80, from: -40);
        var rock = new CellMaterial(9, 3, 100, 0,
            [new SolidMaterial(Blocks.Gravel), new SolidMaterial(Blocks.Cobblestone), new SolidMaterial(Blocks.Sand)]);
        var tally = Decorator.Decorate(world, Context(top,
            [new BoulderProp { Id = "b", X = 12, Z = 9, Size = 3, Mossy = false, Seed = 3, Rock = rock }],
            symmetry: "rot_180"));

        await Assert.That(tally.Boulders).IsEqualTo(2);

        // The pattern actually varies over the rock — otherwise the comparison below would hold for a solid too.
        var first = Rock(world, 12, 9);
        await Assert.That(first.Select(cell => cell.Id).Distinct().Count()).IsGreaterThan(1);
        // The turn maps a cell to -x-1, -z-1, so the image stands on (-13, -10) with both its axes reversed.
        await Assert.That(Rock(world, -13, -10, turned: true)).IsEquivalentTo(first);
    }

    /// <summary>Depth on a rock is measured from the rock's own crust, not the map's surface, so a layer stack
    /// reads as a weathered skin over a core rather than as the terrain bands it names anywhere else.</summary>
    [Test]
    public async Task A_layered_rock_weathers_its_crust_and_not_the_ground_it_sits_in()
    {
        var (world, top) = Plateau();
        Decorator.Decorate(world, Context(top, [new BoulderProp
        {
            Id = "b", X = 20, Z = 20, Size = 3, Mossy = false, Seed = 3,
            Rock = new LayeredMaterial(new BandStack([
                new Band(new SolidMaterial(Blocks.Cobblestone), 1),
                new Band(new SolidMaterial(Blocks.Stone), 1)])),
        }]));

        var column = Column(world, 20, 20);
        await Assert.That(column.Count).IsGreaterThanOrEqualTo(2);
        await Assert.That(column[^1].Id).IsEqualTo(Blocks.Cobblestone);      // the crust, one course
        await Assert.That(column[^2].Id).IsEqualTo(Blocks.Stone);            // the core under it
    }

    /// <summary>A boulder that stored one block, from before a rock was a material, keeps that block. A silent
    /// repaint on the next export is worse than a refusal, because nothing says it happened.</summary>
    [Test]
    public async Task A_boulder_written_before_the_rock_material_keeps_the_block_it_named()
    {
        var prop = (BoulderProp)DressingJson.DeserializeProp(
            "{\"kind\":\"boulder\",\"id\":\"b\",\"x\":1,\"z\":2,\"blockId\":4,\"blockData\":3}")!;
        await Assert.That(prop.Rock).IsEqualTo((TerrainMaterial)new SolidMaterial(4, 3));
    }

    /// <summary>A cobbled path stored its blocks and a style that tiled them over a jittered grid. The style is
    /// gone — the tiling is what the cell pattern does — so the stored one becomes that pattern, over the same
    /// grid it was already tiled by, and its style falls back to the band it always paved.</summary>
    [Test]
    public async Task A_cobbled_path_written_before_the_pave_material_keeps_its_tiling()
    {
        var prop = (PathProp)DressingJson.DeserializeProp(
            "{\"kind\":\"path\",\"id\":\"p\",\"seed\":5,\"style\":\"cobble\","
            + "\"blocks\":[{\"id\":4,\"data\":0},{\"id\":13,\"data\":0}]}")!;

        await Assert.That(prop.Style).IsEqualTo(PathStyle.Solid);
        await Assert.That(prop.Pave).IsEqualTo((TerrainMaterial)new CellMaterial(34, 3, 100, 0,
            [new SolidMaterial(4), new SolidMaterial(13)]));

        // A path of any other style spent only its first block, so that is the solid it becomes.
        var plain = (PathProp)DressingJson.DeserializeProp(
            "{\"kind\":\"path\",\"id\":\"p\",\"seed\":5,\"blocks\":[{\"id\":13,\"data\":0}]}")!;
        await Assert.That(plain.Pave).IsEqualTo((TerrainMaterial)new SolidMaterial(13));
    }

    // The part of one column standing above the plateau's surface, bottom-up — the half of a boulder that is
    // the boulder rather than the ground it is set into, and so the half a block id identifies unambiguously.
    private static List<(int Id, int Data)> Column(VoxelWorld world, int x, int z)
    {
        var column = new List<(int Id, int Data)>();
        for (var y = 8; y <= 20; y++)
        {
            var (id, data) = world.GetBlock(x, y, z);
            if (id != Blocks.Air) column.Add((id, data));
        }
        return column;
    }

    /// <summary>A whole rock as its blocks against their offsets from its own anchor. Two images of one boulder
    /// have to agree on exactly this, and comparing two positions on the map cannot say it —
    /// <paramref name="turned"/> reads an image the half-turn reversed both axes of.</summary>
    private static List<(int X, int Y, int Z, int Id, int Data)> Rock(
        VoxelWorld world, int anchorX, int anchorZ, bool turned = false)
    {
        var facing = turned ? -1 : 1;
        var cells = new List<(int, int, int, int, int)>();
        for (var dx = -6; dx <= 6; dx++)
        for (var dz = -6; dz <= 6; dz++)
        for (var y = 8; y <= 20; y++)
        {
            var (id, data) = world.GetBlock(anchorX + dx, y, anchorZ + dz);
            if (id != Blocks.Air) cells.Add((dx * facing, y, dz * facing, id, data));
        }
        return cells;
    }

    // ── knobs out of range ─────────────────────────────────────────────────────────────────────────
    /// <summary>A prop's cost is superlinear in its reach, so an out-of-range knob is not a strange picture
    /// but a build that never returns — the failure a mis-parsed query value produced. The bounded readings
    /// are what every builder and preview uses, and they hold whatever the stored value says.</summary>
    [Test]
    public async Task A_tree_knob_outside_its_range_is_held_to_the_range()
    {
        var absurd = new TreeProp
        {
            Form = TreeForm.Grown, Height = 999, Stems = 99, Levels = 99,
            Leader = 55, Flow = 45, BranchAngle = 55, LeafSize = 60,
        };

        await Assert.That(absurd.Reach).IsEqualTo(40);
        await Assert.That(absurd.LeafCluster).IsEqualTo(1);
        var shape = absurd.Shape;
        await Assert.That(shape.Height).IsEqualTo(40);
        await Assert.That(shape.Stems).IsEqualTo(3);
        await Assert.That(shape.Levels).IsEqualTo(3);
        await Assert.That(shape.Leader).IsEqualTo(1);
        await Assert.That(shape.Flow).IsEqualTo(1);
        await Assert.That(shape.BranchAngle).IsEqualTo(1.5);

        // and the other way: a knob below its range is lifted rather than left to build nothing
        var tiny = new TreeProp { Form = TreeForm.Grown, Height = -10, Leader = -5, LeafSize = 0 };
        await Assert.That(tiny.Reach).IsEqualTo(5);
        await Assert.That(tiny.Shape.Leader).IsEqualTo(0);
        await Assert.That(tiny.LeafCluster).IsEqualTo(0.2);
    }

    [Test]
    public async Task A_boulder_size_outside_its_range_is_held_to_the_range()
    {
        await Assert.That(new BoulderProp { Size = 999 }.Reach).IsEqualTo(7);
        await Assert.That(new BoulderProp { Size = 0 }.Reach).IsEqualTo(1);
    }

    [Test]
    public async Task A_tree_stored_with_absurd_knobs_still_builds_a_tree()
    {
        // The bound is not merely arithmetic: the pass has to finish and place wood on the plateau it was
        // given, which is what a runaway shape did not do.
        var (world, top) = Plateau();
        Decorator.Decorate(world, Context(top, [new TreeProp
        {
            Form = TreeForm.Grown, X = 20, Z = 20, Seed = 3,
            Height = 999, Stems = 99, Levels = 99, Leader = 55, Flow = 45, BranchAngle = 55, LeafSize = 60,
        }]));

        var logs = Placed(world, top.Keys, 8, 60).Where(block => block.Id == Blocks.Log).ToList();
        await Assert.That(logs).IsNotEmpty().Because("a bounded tree is still a tree");
    }
}

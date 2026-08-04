using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft.Dressing;

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
        var tally = Decorator.Decorate(world, Context(top, []));

        await Assert.That(tally).IsEqualTo(new DressingTally());
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
        var logs = Placed(world, top.Keys, 8, 40).Where(b => b.Id == DressingPalette.Log).ToList();
        await Assert.That(logs).IsNotEmpty();
        await Assert.That(logs.Min(b => b.Y)).IsEqualTo(8);                  // seated on the surface, not sunk
        // and standing at its own cell: limbs reach out, but the whole tree is within a crown's radius of
        // where it was put, not somewhere else on a forty-block plateau.
        await Assert.That(logs.Max(b => Math.Abs(b.X - 14))).IsLessThan(9);
        await Assert.That(logs.Max(b => Math.Abs(b.Z - 20))).IsLessThan(9);
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

        var leaves = Placed(world, top.Keys, 8, 40).Where(b => b.Id == DressingPalette.Leaves).ToList();
        await Assert.That(leaves).IsNotEmpty();
        await Assert.That(leaves.All(b => (b.Data & DressingPalette.LeafNoDecay) != 0)).IsTrue();
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
            Blocks = [new PaveBlock(Blocks.Gravel, 0)],
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
                Blocks = [new PaveBlock(Blocks.Gravel, 0)],
            }])).PathCells;
        }

        var solid = Paved(PathStyle.Solid);
        await Assert.That(Paved(PathStyle.Worn, 0.5)).IsLessThan(solid);
        await Assert.That(Paved(PathStyle.Stones)).IsLessThan(solid);
        await Assert.That(Paved(PathStyle.Stones)).IsGreaterThan(0);
        await Assert.That(Paved(PathStyle.Tapered)).IsLessThan(solid);       // thin at both ends
    }

    [Test]
    public async Task A_cobbled_path_spends_every_block_it_was_given()
    {
        var (world, top) = Plateau();
        Decorator.Decorate(world, Context(top, [new PathProp
        {
            Id = "p", Points = [[4, 20], [35, 20]], Radius = 3, Style = PathStyle.Cobble, Seed = 5,
            Blocks = [new PaveBlock(Blocks.Cobblestone, 0), new PaveBlock(Blocks.Gravel, 0), new PaveBlock(Blocks.Stone, 0)],
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
            Id = "p", Points = [[4, 20], [35, 20]], Radius = 2, Seed = 5, Blocks = [new PaveBlock(Blocks.Gravel, 0)],
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
            new PathProp { Id = "p", Points = [[4, 20], [35, 20]], Radius = 3, Seed = 5, Blocks = [new PaveBlock(Blocks.Gravel, 0)] },
            new FloraProp { Id = "f", Points = AreaOver(40), Spec = new FloraSpec(Coverage: 1.0), Seed = 7 },
        ]));

        var overRoad = Placed(world, [(20, 20), (18, 20), (24, 20)], 8, 10).ToList();
        await Assert.That(overRoad).IsEmpty();
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
    public async Task A_tree_may_lean_its_crown_over_a_drop_but_never_over_something_protected()
    {
        // Two different footprints answer two different questions. What a prop *rests* on needs ground; what it
        // merely occupies does not, or no tree could grow within a crown's reach of a shoreline. Protection is
        // the opposite: it covers the whole volume, because a canopy over a monument reads as badly as a trunk.
        var stand = new TreeProp { Id = "t", X = 7, Z = 7, Species = "oak", Seed = 5 };

        var (edge, edgeTop) = Plateau(14);
        await Assert.That(Decorator.Decorate(edge, Context(edgeTop, [stand])).Trees).IsEqualTo(1);

        var (fenced, fencedTop) = Plateau(14);
        var walled = Decorator.Decorate(fenced, Context(fencedTop, [stand],
            isProtected: (x, z) => x is < 6 or > 8 || z is < 6 or > 8));
        await Assert.That(walled.Trees).IsEqualTo(0);
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
            Blocks = [new PaveBlock(Blocks.Gravel, 0)],
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

    // ── determinism & the wire format ──────────────────────────────────────────────────────────────
    [Test]
    public async Task The_same_props_dress_the_same_world_the_same_way()
    {
        PlacedProp[] props =
        [
            new PathProp { Id = "p", Points = [[4, 20], [35, 20]], Radius = 2, Style = PathStyle.Worn, Seed = 5, Blocks = [new PaveBlock(Blocks.Gravel, 0)] },
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
                new PathProp { Id = "p", Points = [[1, 2], [3, 4]], Radius = 4, Style = PathStyle.Cobble, Seed = 5, Blocks = [new PaveBlock(4, 0), new PaveBlock(13, 0)] },
                new TreeProp { Id = "t", X = 5, Z = 6, Species = "birch", Height = 22, Stems = 2, Seed = 9 },
                new BoulderProp { Id = "b", X = 7, Z = 8, Form = BoulderForm.Cairn, Size = 4, Seed = 11 },
                new FloraProp { Id = "f", Points = [[0, 0], [8, 0], [8, 8]], Spec = new FloraSpec(Coverage: 0.9), Seed = 13 },
            ],
        };

        var back = DressingJson.Deserialize(DressingJson.Serialize(doc));

        await Assert.That(back.Props.Select(prop => prop.GetType().Name))
            .IsEquivalentTo(doc.Props.Select(prop => prop.GetType().Name));
        await Assert.That(((PathProp)back.Props[0]).Style).IsEqualTo(PathStyle.Cobble);
        await Assert.That(((TreeProp)back.Props[1]).Species).IsEqualTo("birch");
        await Assert.That(((BoulderProp)back.Props[2]).Form).IsEqualTo(BoulderForm.Cairn);
        await Assert.That(((FloraProp)back.Props[3]).Spec.Coverage).IsEqualTo(0.9);
    }

    [Test]
    public async Task A_blob_of_json_the_reader_cannot_parse_dresses_nothing_rather_than_failing_an_export()
    {
        await Assert.That(DressingJson.Deserialize("{ not json").Props).IsEmpty();
        await Assert.That(DressingJson.DeserializeProp("{\"kind\":\"unicorn\"}")).IsNull();
    }
}

using PgmStudio.Domain;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Vocabulary;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The dressing pass (docs/world-export/decoration.md): the third walk over a realized world, adding the
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
        Func<int, int, KeepOut?>? keptClear = null, string? symmetry = null, double centerX = 0, double centerZ = 0,
        Func<int, int, bool>? goalClearance = null)
        => new(top, props, keptClear ?? ((_, _) => null), new DressingSymmetry(symmetry, centerX, centerZ),
               IsGoalClearance: goalClearance);

    /// <summary><b>A stroke is turned away by ground somebody drew, and by nothing else.</b> The keep-out mask
    /// is about things that <em>stand</em> on ground; a stroke stands on nothing, replaces one course of
    /// finish, and in front of a door it IS the lane the approach is being kept clear for. Held to the whole
    /// mask a road stops short of every spawn and tapers away as the approach rect crosses it.</summary>
    [Test]
    public async Task A_stroke_paves_through_a_door_approach_and_stops_at_a_drawn_structure()
    {
        var (world, top) = Plateau();
        // The mask a spawn at the far end of the plateau builds: its own margin over z 30+, the lane in
        // front of its door over z 24..29, and a drawn thing — a wall, a stair, a crop bed — at x 30.
        static KeepOut? Mask(int x, int z) =>
            x is >= 29 and <= 31 ? KeepOut.Structure
            : z >= 30 ? KeepOut.Spawn
            : z >= 24 ? KeepOut.Approach
            : null;

        var tally = Decorator.Decorate(world, Context(top,
        [
            new StrokeProp
            {
                Id = "lane", Points = [[20, 4], [20, 34]], Radius = 2, Seed = 5, Route = true,
                Pave = new SolidMaterial(Blocks.Gravel),
            },
            new StrokeProp
            {
                Id = "cross", Points = [[4, 12], [36, 12]], Radius = 2, Seed = 5, Route = true,
                Pave = new SolidMaterial(Blocks.Gravel),
            },
        ], keptClear: Mask));

        await Assert.That(tally.Declines).IsEmpty();
        // Through the approach and on into the spawn's own margin: a road that stops in front of a door
        // leads nowhere, and neither reason is about a block standing on the ground.
        await Assert.That(world.GetBlock(20, 7, 26).Id).IsEqualTo(Blocks.Gravel);
        await Assert.That(world.GetBlock(20, 7, 32).Id).IsEqualTo(Blocks.Gravel);
        // And stopped dead at the drawn structure, which is the one thing `keepClear` exists to protect:
        // the crossing road paves either side of x 30 and not on it.
        await Assert.That(world.GetBlock(27, 7, 12).Id).IsEqualTo(Blocks.Gravel);
        await Assert.That(world.GetBlock(30, 7, 12).Id).IsNotEqualTo(Blocks.Gravel);
        await Assert.That(world.GetBlock(34, 7, 12).Id).IsEqualTo(Blocks.Gravel);
    }

    /// <summary>A goal's clearance over the square [16,24]² — the shape `DressingScope.GoalClearanceAt`
    /// derives from a goal at (20, 20), stated here as the literal it is so these tests read the pass rather
    /// than the derivation.</summary>
    private static bool ClearanceAt20(int x, int z) => x is >= 16 and <= 24 && z is >= 16 and <= 24;

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

    // ── every whole-prop decline is reported with its reason ───────────────────────────────────────
    [Test]
    public async Task A_house_stands_over_the_path_and_the_road_still_keeps_a_tree_off_the_route()
    {
        // The path is laid first and a road is meant to run to a porch or a door, so a house at the END of
        // the pavement wins the ground and the path simply ends at its wall (a house the road carries on past
        // is DR-CROSS, below). The band's claim still holds against the props above the buildings: a trunk in
        // the middle of the route is refused, with the colliding cell named.
        var (world, top) = Plateau();
        var tally = Decorator.Decorate(world, Context(top,
        [
            new StrokeProp
            {
                Id = "p", Points = [[4, 20], [35, 20]], Radius = 2, Seed = 5, Route = true,
                Pave = new SolidMaterial(Blocks.Gravel),
            },
            new HouseProp
            {
                Id = "h", Wings = [new AuthoredWing([[2, 16], [10, 24]])],
                Style = new HouseStyle { Doorway = new Doorway { Door = DoorMaterial.Air } },
            },
            new TreeProp { Id = "t", X = 30, Z = 20, Species = "oak", Height = 14, Seed = 5 },
        ]));

        await Assert.That(tally.Houses).IsEqualTo(1);
        await Assert.That(tally.Trees).IsEqualTo(0);
        var drop = tally.Declines.Single(d => d.SubjectIds.Contains("t"));
        await Assert.That(drop.Rule).IsEqualTo(DressingRules.GroundTaken);
        // Neither a refusal nor a remark: the world is built and this tree is not in it, which is the one
        // thing a caller reading the 2xx cannot learn any other way.
        await Assert.That(drop.Severity).IsEqualTo(Severity.Decline);
        await Assert.That(drop.Message).Contains("tree 't' rests on (");
        // The road got there first and keeps the cell, so that is what the decline names — the record says
        // who holds the ground, not merely that something does.
        await Assert.That(drop.Message).Contains("claimed by the route 'p'");
        // The road survives up to the wall and the house's floor owns the ground inside it.
        await Assert.That(world.GetBlock(25, 7, 20).Id).IsEqualTo(Blocks.Gravel);
        await Assert.That(world.GetBlock(6, 7, 20).Id).IsNotEqualTo(Blocks.Gravel);
    }

    [Test]
    public async Task A_house_the_road_runs_past_is_declined_and_paint_is_not_a_road()
    {
        // The same road and a house in the middle of it: what was one way through the board becomes two dead
        // ends facing a wall, so the whole building is declined (DR-CROSS). The stroke has to be a route — a
        // band laid to change a finish is ground rather than a way, and a house on paint is a house on grass.
        HouseProp Middle() => new()
        {
            Id = "h", Wings = [new AuthoredWing([[14, 16], [22, 24]])],
            Style = new HouseStyle { Doorway = new Doorway { Door = DoorMaterial.Air } },
        };
        StrokeProp Band(bool route) => new()
        {
            Id = "p", Points = [[4, 20], [35, 20]], Radius = 2, Seed = 5, Route = route,
            Pave = new SolidMaterial(Blocks.Gravel),
        };

        var (paved, pavedTop) = Plateau();
        var across = Decorator.Decorate(paved, Context(pavedTop, [Band(route: true), Middle()]));

        await Assert.That(across.Houses).IsEqualTo(0);
        var drop = across.Declines.Single(finding => finding.SubjectIds.Contains("h"));
        await Assert.That(drop.Rule).IsEqualTo(DressingRules.RouteCrossed);
        await Assert.That(drop.Severity).IsEqualTo(Severity.Decline);
        await Assert.That(drop.Message).Contains("stands across the route 'p'");

        var (painted, paintedTop) = Plateau();
        var overPaint = Decorator.Decorate(painted, Context(paintedTop, [Band(route: false), Middle()]));

        await Assert.That(overPaint.Houses).IsEqualTo(1);
        await Assert.That(overPaint.Declines).IsEmpty();
    }

    [Test]
    public async Task Paint_is_planted_over_and_only_a_route_is_kept_clear_of()
    {
        // The standoff exists to stop a canopy closing over a road. A stroke that is a grass tongue over a
        // crag is ground rather than a way through, and asking the same distance of it is what leaves a board
        // with nothing plantable: the same geometry, the same brush, one word apart.
        StrokeProp Band(bool route) => new()
        {
            Id = "p", Points = [[4, 20], [35, 20]], Radius = 2, Seed = 5, Route = route,
            Pave = new SolidMaterial(Blocks.Gravel),
        };

        var (painted, paintedTop) = Plateau();
        var overPaint = Decorator.Decorate(painted, Context(paintedTop,
            [Band(route: false), new TreeProp { Id = "t", X = 20, Z = 23, Species = "oak", Height = 14, Seed = 5 }]));
        await Assert.That(overPaint.Trees).IsEqualTo(1);
        await Assert.That(overPaint.Declines).IsEmpty();

        var (paved, pavedTop) = Plateau();
        var overRoute = Decorator.Decorate(paved, Context(pavedTop,
            [Band(route: true), new TreeProp { Id = "t", X = 20, Z = 23, Species = "oak", Height = 14, Seed = 5 }]));
        await Assert.That(overRoute.Trees).IsEqualTo(0);
        await Assert.That(overRoute.Declines.Single(d => d.SubjectIds.Contains("t")).Rule)
            .IsEqualTo(DressingRules.RoadStandoff);

        // Both lay the same ground: paint is not a weaker road, it is the same band without the claim.
        await Assert.That(overPaint.PathCells).IsEqualTo(overRoute.PathCells);
    }

    [Test]
    public async Task A_trunk_keeps_three_blocks_off_the_road_and_a_rock_keeps_two()
    {
        // The author's ruling: a road may pass a forest, but a trunk against the kerb reads as trees in the
        // road. The path here paves z 18–21 (radius 2 on the z=20 line), so a trunk at z=23 stands two blocks
        // off the pavement — inside a tree's three-block standoff — and one at z=25 keeps it. A rock's
        // standoff is the shorter two: one resting against the kerb is refused, one well clear seats.
        StrokeProp Road() => new()
        {
            Id = "p", Points = [[4, 20], [35, 20]], Radius = 2, Seed = 5, Route = true,
            Pave = new SolidMaterial(Blocks.Gravel),
        };

        var (near, nearTop) = Plateau();
        var refused = Decorator.Decorate(near, Context(nearTop,
            [Road(), new TreeProp { Id = "t", X = 20, Z = 23, Species = "oak", Height = 14, Seed = 5 }]));
        await Assert.That(refused.Trees).IsEqualTo(0);
        var drop = refused.Declines.Single(d => d.SubjectIds.Contains("t"));
        await Assert.That(drop.Rule).IsEqualTo(DressingRules.RoadStandoff);
        await Assert.That(drop.Message).Contains("nearer than 3 blocks to the road at (");

        var (clear, clearTop) = Plateau();
        var seated = Decorator.Decorate(clear, Context(clearTop,
            [Road(), new TreeProp { Id = "t", X = 20, Z = 25, Species = "oak", Height = 14, Seed = 5 }]));
        await Assert.That(seated.Trees).IsEqualTo(1);
        await Assert.That(seated.Declines).IsEmpty();

        var (rocky, rockyTop) = Plateau();
        var rocks = Decorator.Decorate(rocky, Context(rockyTop,
        [
            Road(),
            new BoulderProp { Id = "b-near", X = 10, Z = 22, Size = 1, Mossy = false, Seed = 3 },
            new BoulderProp { Id = "b-clear", X = 28, Z = 27, Size = 1, Mossy = false, Seed = 3 },
        ]));
        await Assert.That(rocks.Boulders).IsEqualTo(1);
        await Assert.That(rocks.Declines.Single().SubjectIds).IsEquivalentTo(new[] { "b-near" });
    }

    [Test]
    public async Task A_tree_over_the_void_is_reported_dropped_and_a_seated_one_reports_nothing()
    {
        // One tree on the plateau, one clicked past its edge: the seated one stands, the void one is a
        // reported decline rather than a silently smaller count — and a board with no drops answers null.
        var (world, top) = Plateau();
        var seated = Decorator.Decorate(world, Context(top,
            [new TreeProp { Id = "t-on", X = 14, Z = 20, Species = "oak", Height = 16, Seed = 5 }]));
        await Assert.That(seated.Trees).IsEqualTo(1);
        await Assert.That(seated.Declines).IsEmpty();

        var (world2, top2) = Plateau();
        var dropped = Decorator.Decorate(world2, Context(top2,
            [new TreeProp { Id = "t-off", X = 200, Z = 200, Species = "oak", Height = 16, Seed = 5 }]));
        await Assert.That(dropped.Trees).IsEqualTo(0);
        var drop = dropped.Declines.Single(d => d.SubjectIds.Contains("t-off"));
        await Assert.That(drop.Rule).IsEqualTo(DressingRules.NoGround);
        await Assert.That(drop.Severity).IsEqualTo(Severity.Decline);
        await Assert.That(drop.Message).Contains("tree 't-off' has no ground at (");
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

    /// <summary><b>A prop seats on its feet and is written wherever it meets air, so clearing the seat is not
    /// the same as fitting.</b> The ground a building holds reaches one block past its stamp; a crown reaches
    /// as far as the tree is wide. What separates a prop worth naming from one that merely brushed something
    /// is whether the clip cut a piece off its own footing and left it in the air.</summary>
    [Test]
    public async Task A_tree_that_loses_a_limb_to_a_wall_it_stands_clear_of_is_named()
    {
        // A wall taller than the tree, eight blocks east of the stem — far enough that the seat is clear and
        // almost every block still lands, and near enough that the crown reaches into it.
        var (world, top) = Wall(gap: 8);
        var report = Decorator.Decorate(world, Context(top,
            [new TreeProp { Id = "oak", X = 0, Z = 0, Form = TreeForm.Grown, Wood = "oak", Height = 20, Seed = 7 }]));

        var cut = report.Declines.SingleOrDefault(finding => finding.Rule == DressingRules.PropCut);
        await Assert.That(cut).IsNotNull();
        await Assert.That(cut!.Severity).IsEqualTo(Severity.Complaint);   // the tree is in the world, as it fell
        await Assert.That(cut.Message).Contains("oak");
        await Assert.That(report.Trees).IsEqualTo(1);                     // and it is not declined
    }

    /// <summary>The other half of the same rule: a rock flattened along a wall is still a rock. It loses far
    /// more of itself than the tree above does and says nothing, because a solid mass truncated at a face
    /// severs nothing — every block that lands is still joined to the ground.</summary>
    [Test]
    public async Task A_boulder_flattened_against_a_wall_says_nothing()
    {
        var (bare, bareTop) = Wall(gap: 40);
        Decorator.Decorate(bare, Context(bareTop, [Erratic()]));
        var whole = Rock(bare);

        var (world, top) = Wall(gap: 2);
        var report = Decorator.Decorate(world, Context(top, [Erratic()]));

        await Assert.That(Rock(world)).IsLessThan(whole * 4 / 5);         // the wall really did cut it
        await Assert.That(report.Declines.Any(finding => finding.Rule == DressingRules.PropCut)).IsFalse();

        static BoulderProp Erratic() => new() { Id = "rock", X = 0, Z = 0, Size = 7, Mossy = false, Seed = 7 };
        static int Rock(VoxelWorld world) => Cells(world).Count(block => block.Id == Blocks.Stone && block.Y >= 8);
    }

    /// <summary>And a small tree at the same clearance the big one is named at. A crown two or three blocks
    /// wide never reaches the wall, so the rule is about the prop's own reach and not about how close the
    /// seat rule lets it stand.</summary>
    [Test]
    public async Task A_small_tree_at_the_same_clearance_says_nothing()
    {
        var (world, top) = Wall(gap: 8);
        var report = Decorator.Decorate(world, Context(top,
            [new TreeProp { Id = "oak", X = 0, Z = 0, Form = TreeForm.Grown, Wood = "oak", Height = 8, Seed = 7 }]));

        await Assert.That(report.Trees).IsEqualTo(1);
        await Assert.That(report.Declines.Any(finding => finding.Rule == DressingRules.PropCut)).IsFalse();
    }

    /// <summary>A plate with a wall standing on it `gap` blocks east of the origin, taller than anything these
    /// tests place, so the obstruction is the wall rather than the top of it.</summary>
    private static (VoxelWorld World, Dictionary<(int X, int Z), int> Top) Wall(int gap)
    {
        var world = new VoxelWorld();
        var top = new Dictionary<(int X, int Z), int>();
        for (var z = -40; z < 40; z++)
        for (var x = -40; x < 40; x++)
        {
            for (var y = 0; y < 7; y++) world.SetBlock(x, y, z, Blocks.Stone);
            world.SetBlock(x, 7, z, Blocks.Grass);
            top[(x, z)] = 8;
        }
        for (var y = 8; y < 48; y++)
        for (var z = -14; z < 14; z++)
        for (var x = gap; x < gap + 6; x++)
            world.SetBlock(x, y, z, Blocks.IronBlock, 0);
        return (world, top);
    }

    /// <summary>Every block standing over the plate <see cref="Wall"/> builds, the wall's own excluded.</summary>
    private static IEnumerable<(int X, int Y, int Z, int Id)> Cells(VoxelWorld world)
    {
        for (var y = 8; y < 70; y++)
        for (var z = -40; z < 40; z++)
        for (var x = -40; x < 40; x++)
        {
            var block = world.GetBlock(x, y, z);
            if (block.Id is not (Blocks.Air or Blocks.IronBlock)) yield return (x, y, z, block.Id);
        }
    }

    [Test]
    public async Task A_boulder_stands_on_the_ground_and_is_bedded_into_it()
    {
        var (world, top) = Plateau();
        var tally = Decorator.Decorate(world, Context(top,
            [new BoulderProp { Id = "b", X = 20, Z = 20, Size = 5, Mossy = false, Seed = 3 }]));

        await Assert.That(tally.Boulders).IsEqualTo(1);
        var rock = Placed(world, top.Keys, 4, 20).Where(b => b.Id == Blocks.Stone && b.Y >= 8).ToList();
        // An erratic is a mass left standing on a surface, so its bulk is over the ground …
        await Assert.That(rock.Max(b => b.Y)).IsGreaterThanOrEqualTo(13);
        // … and only its foot is under, which is what stops a course of turf showing daylight beneath it.
        await Assert.That(world.GetBlock(20, 7, 20).Id).IsEqualTo(Blocks.Stone);
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
        var tally = Decorator.Decorate(world, Context(top, [new StrokeProp
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
            return Decorator.Decorate(world, Context(top, [new StrokeProp
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
        Decorator.Decorate(world, Context(top, [new StrokeProp
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

        Decorator.Decorate(world, Context(top, [new StrokeProp
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
            new StrokeProp { Id = "p", Points = [[4, 20], [35, 20]], Radius = 3, Seed = 5, Pave = new SolidMaterial(Blocks.Gravel) },
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
    public async Task A_stated_level_fills_a_basin_the_ground_has_no_surface_at()
    {
        // The case a derived line cannot answer: the ground is dug out, so the lowest surface the channel
        // crosses IS the basin floor and filling to it puts no water in the hole. A stated line fills to itself.
        var (world, top) = Plateau();
        for (var z = 14; z < 27; z++)
        for (var x = 14; x < 27; x++)
        {
            for (var y = 3; y <= 7; y++) world.SetBlock(x, y, z, Blocks.Air);
            world.SetBlock(x, 2, z, Blocks.Stone);
            top[(x, z)] = 3;
        }

        Decorator.Decorate(world, Context(top, [new WaterProp
        {
            Id = "w", Points = [[20, 16], [20, 24]], Radius = 6, Depth = 2, Shore = 0, Level = 6, Seed = 5,
        }]));

        // The basin holds water to the stated line and no further: y6 is water, y7 is the air above it.
        await Assert.That(world.GetBlock(20, 6, 20).Id).IsEqualTo(Blocks.StationaryWater);
        await Assert.That(world.GetBlock(20, 4, 20).Id).IsEqualTo(Blocks.StationaryWater);
        await Assert.That(world.GetBlock(20, 7, 20).Id).IsEqualTo(Blocks.Air);
        // And the basin floor is still the ground it was, not a shelf laid at line minus depth.
        await Assert.That(world.GetBlock(20, 2, 20).Id).IsEqualTo(Blocks.Stone);
    }

    [Test]
    public async Task A_stated_level_reaches_no_further_than_the_footprint()
    {
        // Water rises to the line inside the prop's own cells and nowhere else — the rim is the author's, and
        // the pass never floods outward looking for one.
        var (world, top) = Plateau();
        Decorator.Decorate(world, Context(top, [new WaterProp
        {
            Id = "w", Points = [[20, 16], [20, 24]], Radius = 3, Depth = 2, Shore = 0, Level = 9, Seed = 5,
        }]));

        await Assert.That(world.GetBlock(20, 9, 20).Id).IsEqualTo(Blocks.StationaryWater);
        await Assert.That(world.GetBlock(2, 9, 2).Id).IsEqualTo(Blocks.Air);
    }

    [Test]
    public async Task Water_fills_the_air_beside_a_thing_standing_in_it_and_never_cuts_under_it()
    {
        // The harbour case: a hull floats in a pool and its columns are kept clear, so the pass must not
        // carve the seabed out from under it — and must still put water in the air beside it, a harbour dry
        // under the ship floating in it being no harbour.
        var (world, top) = Plateau();
        for (var z = 14; z < 27; z++)
        for (var x = 14; x < 27; x++)
        {
            for (var y = 3; y <= 7; y++) world.SetBlock(x, y, z, Blocks.Air);
            world.SetBlock(x, 2, z, Blocks.Stone);
            top[(x, z)] = 3;
        }
        // A hull two blocks thick floating at the line, over four columns of the basin.
        for (var z = 19; z < 22; z++)
        for (var x = 19; x < 22; x++)
        for (var y = 5; y <= 6; y++)
            world.SetBlock(x, y, z, Blocks.Planks, 1);

        KeepOut? hull(int x, int z) =>
            x is >= 19 and <= 21 && z is >= 19 and <= 21 ? KeepOut.Structure : null;
        Decorator.Decorate(world, Context(top, [new WaterProp
        {
            Id = "harbour", Shape = WaterShape.Pool, Points = [[14, 14], [26, 14], [26, 26], [14, 26]],
            Radius = 3, Depth = 2, Shore = 0, Level = 6, Seed = 5,
        }], keptClear: hull));

        // The basin holds water to the line, the hull is untouched, and the course under it is water rather
        // than the dry hole a kept-clear column used to be left as.
        await Assert.That(world.GetBlock(16, 6, 20).Id).IsEqualTo(Blocks.StationaryWater);
        await Assert.That(world.GetBlock(20, 5, 20)).IsEqualTo((Blocks.Planks, 1));
        await Assert.That(world.GetBlock(20, 6, 20)).IsEqualTo((Blocks.Planks, 1));
        await Assert.That(world.GetBlock(20, 4, 20).Id).IsEqualTo(Blocks.StationaryWater);
        // And the ground it stands over is the ground it stood over: no bed was cut beneath it.
        await Assert.That(world.GetBlock(20, 2, 20).Id).IsEqualTo(Blocks.Stone);
    }

    [Test]
    public async Task A_pool_fills_a_ring_and_shelves_in_from_its_shore()
    {
        // A pool is the footprint a harbour needs: a filled outline rather than a stroked line, its bed one
        // block deep at the ring and full depth once the shelf is crossed.
        var (world, top) = Plateau(size: 60);
        Decorator.Decorate(world, Context(top, [new WaterProp
        {
            Id = "lake", Shape = WaterShape.Pool, Seed = 5, Shore = 0, Edge = 0,
            Points = [[10, 10], [50, 10], [50, 50], [10, 50]], Radius = 8, Depth = 5,
        }]));

        // Water across the whole ring, corners included — what a stroked channel cannot fill.
        foreach (var (x, z) in new[] { (12, 12), (48, 12), (12, 48), (48, 48), (30, 30) })
            await Assert.That(world.GetBlock(x, 7, z).Id).IsEqualTo(Blocks.StationaryWater);
        // And it shelves: the bed at the middle is cut deeper than the bed one block in from the shore.
        var atShore = Enumerable.Range(0, 8).Count(y => world.GetBlock(11, y, 30).Id == Blocks.StationaryWater);
        var atMiddle = Enumerable.Range(0, 8).Count(y => world.GetBlock(30, y, 30).Id == Blocks.StationaryWater);
        await Assert.That(atMiddle).IsGreaterThan(atShore);
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
        ], keptClear: (x, z) => Math.Abs(x - 20) < 8 && Math.Abs(z - 20) < 8 ? KeepOut.Structure : null));

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
        // Protection is decided on the same footprint ground and occupancy are: the cells a tree
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
            keptClear: (x, z) => x is < 9 or > 11 || z is < 9 or > 11 ? KeepOut.Spawn : null));
        await Assert.That(overhanging.Trees).IsEqualTo(1);

        // But a trunk asked to root on the protected column itself is still refused.
        var (rooted, rootedTop) = Plateau(20);
        var onProtection = Decorator.Decorate(rooted, Context(rootedTop, [stand],
            keptClear: (x, z) => x == 10 && z == 10 ? KeepOut.Spawn : null));
        await Assert.That(onProtection.Trees).IsEqualTo(0);
    }

    // ── nothing stands inside anything else ──────────────────────────────────────────────────
    [Test]
    public async Task A_tree_does_not_root_where_a_building_already_stands()
    {
        // The building is placed first, so its cells are already claimed by the time the tree is considered.
        // The pass used to skip only the individual wood/leaf cells that landed on a non-air block,
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
    public async Task A_house_on_a_hillside_carves_the_slope_out_of_its_rooms()
    {
        // A building seats on the lowest column of its footprint, so a relief mark running through it used
        // to stand inside the rooms: the stamper deliberately never cuts terrain, and nothing else did
        // either. The building wins the ground it was drawn on (the author's ruling) — the slope inside the
        // footprint is carved to air down to the floor, and the hill outside the walls keeps its height.
        // Four courses of mound, which is a slope this building may stand on: past its own height the site
        // is refused instead (`DR-SLOPE`), and a fixture that buried the house would be testing the carve
        // on a placement the pass no longer makes.
        var (world, top) = Plateau();
        for (var x = 16; x <= 30; x++)
        for (var z = 16; z <= 24; z++)
        {
            for (var y = 8; y <= 11; y++) world.SetBlock(x, y, z, Blocks.Stone);
            top[(x, z)] = 12;
        }

        var tally = Decorator.Decorate(world, Context(top,
            [new HouseProp { Id = "h", Wings = [new AuthoredWing([[14, 14], [26, 26]])], Style = new HouseStyle
            {
                Doorway = new Doorway
                {
                    Door = DoorMaterial.Air,
                },
            } }]));

        await Assert.That(tally.Houses).IsEqualTo(1);
        await Assert.That(tally.Declines).IsEmpty();
        // The mound is gone from the interior: the column that stood four courses over the floor is open air.
        for (var y = 9; y <= 11; y++)
            await Assert.That(world.GetBlock(20, y, 20).Id).IsEqualTo(Blocks.Air);
        // The floor still owns its course under the carve, and the hill outside the footprint is untouched.
        await Assert.That(world.GetBlock(20, 7, 20).Id).IsNotEqualTo(Blocks.Air);
        await Assert.That(world.GetBlock(28, 11, 18).Id).IsEqualTo(Blocks.Stone);
    }

    [Test]
    public async Task A_house_under_the_footprint_floor_is_refused_and_a_five_by_five_stands()
    {
        // Eight of fourteen corpus-run houses came out four blocks deep — a wall with a roof. The floor is
        // 5×5 on the plan's box (DR-SIZE); exactly at the floor stands.
        var (world, top) = Plateau();
        var shallow = Decorator.Decorate(world, Context(top,
            [new HouseProp { Id = "h", Wings = [new AuthoredWing([[10, 10], [19, 13]])], Style = new HouseStyle
            {
                Doorway = new Doorway { Door = DoorMaterial.Air },
            } }]));
        await Assert.That(shallow.Houses).IsEqualTo(0);
        await Assert.That(shallow.Declines.Single().Rule).IsEqualTo(DressingRules.FootprintFloor);

        var (world2, top2) = Plateau();
        var atFloor = Decorator.Decorate(world2, Context(top2,
            [new HouseProp { Id = "h", Wings = [new AuthoredWing([[10, 10], [14, 14]])], Style = new HouseStyle
            {
                Doorway = new Doorway { Door = DoorMaterial.Air },
            } }]));
        await Assert.That(atFloor.Houses).IsEqualTo(1);
    }

    [Test]
    public async Task A_house_on_ground_that_rises_past_its_own_walls_is_refused()
    {
        // The failure this rule closes, found on `pgm-studio-mapgen`'s `opus5-ravensmere`: a house sited on
        // a hillside seats on the LOWEST column of its footprint and the terrain over that floor is carved
        // out of it, so a site with more relief across it than the building is tall builds a house nobody
        // can see — its uphill wall is below the ground beside it. Sinking into a slope is the seating rule
        // working; disappearing into one is a site that was never level enough.
        var (hill, hillTop) = Plateau();
        for (var z = 0; z < 40; z++)
        for (var x = 0; x < 40; x++)
        {
            var lift = 2 * Math.Max(0, x - 14);        // a bank climbing east, two courses a block
            for (var y = 8; y < 8 + lift; y++) hill.SetBlock(x, y, z, Blocks.Stone);
            hillTop[(x, z)] = 8 + lift;
        }

        var buried = Decorator.Decorate(hill, Context(hillTop,
            [new HouseProp { Id = "h", Wings = [new AuthoredWing([[10, 16], [20, 24]])],
                             Style = new HouseStyle { Doorway = new Doorway { Door = DoorMaterial.Air } } }]));
        await Assert.That(buried.Houses).IsEqualTo(0);
        var drop = buried.Declines.Single();
        await Assert.That(drop.Rule).IsEqualTo(DressingRules.SiteNotLevel);
        await Assert.That(drop.Message).Contains("across its own footprint");

        // The same house on level ground stands: the rule is about the site, not about the building.
        var (flat, flatTop) = Plateau();
        var seated = Decorator.Decorate(flat, Context(flatTop,
            [new HouseProp { Id = "h", Wings = [new AuthoredWing([[10, 16], [20, 24]])],
                             Style = new HouseStyle { Doorway = new Doorway { Door = DoorMaterial.Air } } }]));
        await Assert.That(seated.Houses).IsEqualTo(1);
        await Assert.That(seated.Declines.Where(f => f.Rule == DressingRules.SiteNotLevel)).IsEmpty();
    }

    [Test]
    public async Task A_house_that_corks_its_leg_is_refused_and_a_coast_house_stands()
    {
        // The generation failure this rule closes: a house across the full width of a land leg, void on both
        // flanks — players would have to dig through the building to reach the other side. Beside a house
        // there must be five blocks of passable ground along at least one side (the author's number). A
        // house against the map's own edge is fine as long as the other side keeps the passage.
        var leg = new VoxelWorld();
        var legTop = new Dictionary<(int X, int Z), int>();
        for (var z = 0; z < 40; z++)
        for (var x = 10; x < 19; x++)   // a nine-wide leg running north-south, void either side
        {
            for (var y = 0; y < 7; y++) leg.SetBlock(x, y, z, Blocks.Stone);
            leg.SetBlock(x, 7, z, Blocks.Grass);
            legTop[(x, z)] = 8;
        }

        var corked = Decorator.Decorate(leg, Context(legTop,
            [new HouseProp { Id = "h", Wings = [new AuthoredWing([[10, 16], [18, 24]])], Style = new HouseStyle
            {
                Doorway = new Doorway
                {
                    Door = DoorMaterial.Air,
                },
            } }]));
        await Assert.That(corked.Houses).IsEqualTo(0);
        var drop = corked.Declines.Single();
        await Assert.That(drop.Message).Contains("no way past");
        await Assert.That(drop.Rule).IsEqualTo(DressingRules.PassAround);

        // The same house on the same leg, hugging the west edge: the east flank keeps a five-block passage.
        var (coast, coastTop) = Plateau();
        for (var z = 0; z < 40; z++)
        for (var x = 0; x < 10; x++) coastTop.Remove((x, z));   // void west of x=10

        var seated = Decorator.Decorate(coast, Context(coastTop,
            [new HouseProp { Id = "h", Wings = [new AuthoredWing([[10, 16], [18, 24]])], Style = new HouseStyle
            {
                Doorway = new Doorway
                {
                    Door = DoorMaterial.Air,
                },
            } }]));
        await Assert.That(seated.Houses).IsEqualTo(1);
        await Assert.That(seated.Declines).IsEmpty();
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
    public async Task Two_buildings_keep_a_block_of_clear_ground_between_their_eaves()
    {
        // A building holds what it stamps plus one block outward, and is *tested* on what it stamps — so two
        // that merely fail to overlap are refused and two with a block between them both stand. Before the
        // ring, the claim was the wall rectangle: a verge overhung ground the pass believed free.
        var (world, top) = Plateau();
        var open = new HouseStyle { Doorway = new Doorway { Door = DoorMaterial.Air } };
        HouseProp At(string id, int minX, int maxX) =>
            new() { Id = id, Wings = [new AuthoredWing([[minX, 10], [maxX, 18]])], Style = open };

        // Overhang 1, so the first stamps x9…19 and the second x20…30 — adjacent, with nothing between them.
        var flush = Decorator.Decorate(world, Context(top, [At("h1", 10, 18), At("h2", 21, 29)]));
        await Assert.That(flush.Houses).IsEqualTo(1);
        var drop = flush.Declines.Single(d => d.SubjectIds.Contains("h2"));
        await Assert.That(drop.Rule).IsEqualTo(DressingRules.GroundTaken);

        // One block further out and the eaves have a course of ground between them.
        var (clean, cleanTop) = Plateau();
        var spaced = Decorator.Decorate(clean, Context(cleanTop, [At("h1", 10, 18), At("h2", 22, 30)]));
        await Assert.That(spaced.Houses).IsEqualTo(2);
        await Assert.That(spaced.Declines).IsEmpty();
    }

    [Test]
    public async Task A_building_with_a_column_of_its_footprint_over_nothing_is_refused()
    {
        // A building seats on the lowest column its plan covers, so a footprint half on land used to seat on
        // that half and hang off the rest. The refusal names the first bare column, which is what makes it
        // checkable against the board.
        var (world, top) = Plateau(size: 20);              // ground is x,z 0…19
        var house = new HouseProp
        {
            Id = "h",
            Wings = [new AuthoredWing([[15, 5], [23, 13]])],   // runs off the east edge
            Style = new HouseStyle { Doorway = new Doorway { Door = DoorMaterial.Air } },
        };

        var tally = Decorator.Decorate(world, Context(top, [house]));

        await Assert.That(tally.Houses).IsEqualTo(0);
        var drop = tally.Declines.Single();
        await Assert.That(drop.Rule).IsEqualTo(DressingRules.NoGround);
        await Assert.That(drop.Message).Contains("(20, 5)");
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
        // — not on everything a tall crown happens to pass over. A trunk planted clear of a low building
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

    // ── fairness ────────────────────────────────────────────────────────────────────────────
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
        var tally = Decorator.Decorate(world, Context(top, [new StrokeProp
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
            keptClear: (x, z) => Math.Abs(x - 12) < 3 && Math.Abs(z - 9) < 3 ? KeepOut.Spawn : null,
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
            new StrokeProp { Id = "p", Points = [[4, 20], [35, 20]], Radius = 2, Style = PathStyle.Worn, Seed = 5, Pave = new SolidMaterial(Blocks.Gravel) },
            new TreeProp { Id = "t", X = 12, Z = 12, Seed = 5 },
            new BoulderProp { Id = "b", X = 28, Z = 28, Seed = 3 },
            new FloraProp { Id = "f", Points = AreaOver(40), Spec = new FloraSpec(), Seed = 7 },
        ];

        var first = Plateau();
        var again = Plateau();
        var one = Decorator.Decorate(first.World, Context(first.SurfaceTop, props));
        var two = Decorator.Decorate(again.World, Context(again.SurfaceTop, props));

        // The counts, then the claims by value — the report holds lists now, and a record comparison over
        // those is reference equality, which would pass for two runs that agreed about nothing.
        await Assert.That(one with { Claimed = null }).IsEqualTo(two with { Claimed = null });
        await Assert.That(one.Placements.Select(claim => (claim.Owner, claim.Pass, claim.Cells.Count)))
            .IsEquivalentTo(two.Placements.Select(claim => (claim.Owner, claim.Pass, claim.Cells.Count)));
        await Assert.That(one.Placements.SelectMany(claim => claim.Cells))
            .IsEquivalentTo(two.Placements.SelectMany(claim => claim.Cells));
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
                new StrokeProp { Id = "p", Points = [[1, 2], [3, 4]], Radius = 4, Style = PathStyle.Rough, Seed = 5,
                               Pave = new CellMaterial(5, 3, 100, 0, [new SolidMaterial(4), new SolidMaterial(13)]) },
                new TreeProp { Id = "t", X = 5, Z = 6, Species = "birch", Height = 22, Stems = 2, Seed = 9 },
                new BoulderProp { Id = "b", X = 7, Z = 8, Form = BoulderForm.Cairn, Size = 4, Seed = 11 },
                new FloraProp { Id = "f", Points = [[0, 0], [8, 0], [8, 8]], Spec = new FloraSpec(Coverage: 0.9), Seed = 13 },
            ],
        };

        var back = DressingJson.Deserialize(DressingJson.Serialize(doc));

        await Assert.That(back.Props.Select(prop => prop.GetType().Name))
            .IsEquivalentTo(doc.Props.Select(prop => prop.GetType().Name));
        await Assert.That(((StrokeProp)back.Props[0]).Style).IsEqualTo(PathStyle.Rough);
        await Assert.That(((StrokeProp)back.Props[0]).Pave).IsEqualTo(((StrokeProp)doc.Props[0]).Pave);
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
        var prop = (StrokeProp)DressingJson.DeserializeProp(
            "{\"kind\":\"path\",\"id\":\"p\",\"seed\":5,\"style\":\"cobble\","
            + "\"blocks\":[{\"id\":4,\"data\":0},{\"id\":13,\"data\":0}]}")!;

        await Assert.That(prop.Style).IsEqualTo(PathStyle.Solid);
        await Assert.That(prop.Pave).IsEqualTo((TerrainMaterial)new CellMaterial(34, 3, 100, 0,
            [new SolidMaterial(4), new SolidMaterial(13)]));

        // A path of any other style spent only its first block, so that is the solid it becomes.
        var plain = (StrokeProp)DressingJson.DeserializeProp(
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
        await Assert.That(new BoulderProp { Size = 999 }.Reach).IsEqualTo(10);
        await Assert.That(new BoulderProp { Size = 0 }.Reach).IsEqualTo(2);
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

    /// <summary>
    /// <b>Every drop the pass makes says the same thing about itself.</b> A decline is what tells a caller
    /// reading a 2xx that a piece of what they posted is not in the world — so a site that raised a complaint
    /// instead would leave that prop looking as though it had been placed, with only a remark beside it. The
    /// board here trips five different rules at once so the check is over the pass rather than over one of
    /// its branches.
    /// </summary>
    [Test]
    public async Task Every_drop_the_pass_makes_is_a_decline()
    {
        var (world, top) = Plateau(60);
        var dropped = Decorator.Decorate(world, Context(top,
        [
            // no ground under it at all
            new TreeProp { Id = "t-void", X = 400, Z = 400, Species = "oak", Height = 8, Seed = 1 },
            // a footprint under the floor
            new HouseProp { Id = "h-thin", Wings = [new AuthoredWing([[4, 4], [7, 6]])] },
            // two buildings over one cell: the second loses the ground
            new HouseProp { Id = "h-first", Wings = [new AuthoredWing([[20, 20], [32, 32]])] },
            new HouseProp { Id = "h-second", Wings = [new AuthoredWing([[26, 26], [38, 38]])] },
        ]));

        await Assert.That(dropped.Declines).IsNotEmpty();
        await Assert.That(dropped.Declines.Where(finding => finding.Severity != Severity.Decline)
            .Select(finding => $"{finding.Rule} ({finding.Message})")).IsEmpty();
    }

    // ── OB19 — a goal's clearance turns a placed prop away ─────────────────────────────────────────────

    /// <summary>
    /// <b>A tree in a goal's clearance is declined, not refused.</b> A goal is what the map is for, so
    /// nothing may stand on the ground it is read against — and a prop is removable, so the pass drops it and
    /// the map still exports. The finding keeps <c>OB19</c> rather than a <c>DR-*</c>: the rule an author
    /// looks up is the one that names the goal, not the one that names the ground.
    /// </summary>
    [Test]
    public async Task A_tree_inside_a_goals_clearance_is_declined_under_OB19()
    {
        var (world, top) = Plateau(60);
        var dropped = Decorator.Decorate(world, Context(top,
            [new TreeProp { Id = "t-goal", X = 20, Z = 20, Species = "oak", Height = 14, Seed = 5 }],
            goalClearance: ClearanceAt20));

        await Assert.That(dropped.Trees).IsEqualTo(0);
        var decline = dropped.Declines.Single();
        await Assert.That(decline.Rule).IsEqualTo(ObjectiveRules.PropInClearance);
        await Assert.That(decline.Severity).IsEqualTo(Severity.Decline);
        await Assert.That(decline.SubjectIds).IsEquivalentTo(new[] { "t-goal" });
        await Assert.That(decline.Message).Contains("inside a goal's clearance");
    }

    /// <summary>A building is judged on the whole floor it stamps, so a footprint whose corner reaches the
    /// clearance goes even though its anchors sit outside it.</summary>
    [Test]
    public async Task A_building_whose_footprint_reaches_the_clearance_is_declined()
    {
        var (world, top) = Plateau(60);
        var dropped = Decorator.Decorate(world, Context(top,
            [new HouseProp { Id = "h-goal", Wings = [new AuthoredWing([[23, 23], [29, 29]])] }],
            goalClearance: ClearanceAt20));

        await Assert.That(dropped.Houses).IsEqualTo(0);
        await Assert.That(dropped.Declines.Single().Rule).IsEqualTo(ObjectiveRules.PropInClearance);
    }

    /// <summary>
    /// <b>The mirror counts.</b> A prop authored nowhere near a goal still stands beside one on the other
    /// half of the board, and the whole prop drops for it — a rock standing on one side and missing from the
    /// other is worse than neither. This is the reading the pass gets for free by fanning before it sites,
    /// and the reason the check belongs here rather than over the authored points.
    /// </summary>
    [Test]
    public async Task A_prop_whose_mirror_lands_in_the_clearance_is_declined_whole()
    {
        var (world, top) = Plateau(60, from: -30);
        var dropped = Decorator.Decorate(world, Context(top,
            [new TreeProp { Id = "t-mirror", X = -20, Z = -20, Species = "oak", Height = 14, Seed = 5 }],
            symmetry: "rot_180", goalClearance: ClearanceAt20));

        await Assert.That(dropped.Trees).IsEqualTo(0);
        await Assert.That(dropped.Declines.Single().SubjectIds).IsEquivalentTo(new[] { "t-mirror" });
    }

    /// <summary>Ground cover crosses a goal's ground freely: grass and flowers under a floating monument hide
    /// nothing and change nothing a player can reach. Only the tall kind turns away, at the narrower mask
    /// `AllowsCover` holds, and the placement clearance never sees a flora field at all.</summary>
    [Test]
    public async Task Ground_cover_crosses_a_goals_clearance()
    {
        var (world, top) = Plateau(60);
        var covered = Decorator.Decorate(world, Context(top,
            [new FloraProp { Id = "f1", Seed = 1, Points = [[16, 16], [24, 16], [24, 24], [16, 24]] }],
            goalClearance: ClearanceAt20));

        await Assert.That(covered.Declines).IsEmpty();
        await Assert.That(covered.Plants).IsGreaterThan(0);
    }
}

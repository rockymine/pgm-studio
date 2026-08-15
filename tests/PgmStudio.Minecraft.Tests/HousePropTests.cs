using PgmStudio.Domain;
using PgmStudio.Minecraft.Dressing;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// A building placed as dressing rather than derived from a plan piece. What is worth asserting is the part
/// that differs from a room: the footprint is a dragged rectangle, so it has to survive being normalized and
/// turned; the placement is authored, so nothing may quietly refuse it; and it is cover, so every team has to
/// get the same building from the same side.
/// </summary>
public sealed class HousePropTests
{
    private static (VoxelWorld World, Dictionary<(int X, int Z), int> SurfaceTop) Plateau(int size = 48)
    {
        var world = new VoxelWorld();
        var top = new Dictionary<(int X, int Z), int>();
        for (var z = -size / 2; z < size / 2; z++)
            for (var x = -size / 2; x < size / 2; x++)
            {
                for (var y = 0; y < 7; y++) world.SetBlock(x, y, z, Blocks.Stone);
                world.SetBlock(x, 7, z, Blocks.Grass);
                top[(x, z)] = 8;
            }
        return (world, top);
    }

    private static DressingContext Context(
        Dictionary<(int X, int Z), int> top, IReadOnlyList<PlacedProp> props,
        Func<int, int, bool>? isProtected = null, string? symmetry = null)
        => new(top, props, isProtected ?? ((_, _) => false), new DressingSymmetry(symmetry, 0, 0));

    private static HouseProp House(int minX, int minZ, int maxX, int maxZ, RoomEdge? front = null) => new()
    {
        Id = "h1",
        Wings = [new AuthoredWing([[minX, minZ], [maxX, maxZ]])],
        Front = front,
        Style = new HouseStyle { Door = DoorMaterial.Air },
    };

    /// <summary>A building of several touching wings, each as its own two opposite corners.</summary>
    private static HouseProp House(
        string id, RoomEdge? front, HouseStyle? style, params (int MinX, int MinZ, int MaxX, int MaxZ)[] wings)
        => new()
        {
            Id = id,
            Wings = [.. wings.Select(wing => new AuthoredWing(
                [[wing.MinX, wing.MinZ], [wing.MaxX, wing.MaxZ]]))],
            Front = front,
            Style = style ?? new HouseStyle { Door = DoorMaterial.Air },
        };

    private static HouseProp House(
        string id, RoomEdge? front, params (int MinX, int MinZ, int MaxX, int MaxZ)[] wings)
        => House(id, front, style: null, wings);

    /// <summary>How tall a column stands over the ground, or 0 where nothing was built.</summary>
    private static int Height(VoxelWorld world, int x, int z)
    {
        var top = 0;
        for (var y = 8; y < 40; y++) if (world.GetBlock(x, y, z).Id != Blocks.Air) top = y;
        return top;
    }

    // ── the footprint ──────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_rectangle_reads_the_same_whichever_corner_was_dragged_from()
    {
        // Two corners, four ways round: an author dragging up-left and one dragging down-right placed the
        // same building, and a stamp that took the points in order would build one of them backwards.
        var forward = House(4, 6, 12, 14).Footprint();
        await Assert.That(forward).IsNotNull();
        await Assert.That((forward!.MinX, forward.MinZ, forward.Width, forward.Depth)).IsEqualTo((4, 6, 9, 9));
        await Assert.That(Plan(House(12, 14, 4, 6).Footprint())).IsEqualTo(Plan(forward));
        await Assert.That(Plan(House(12, 6, 4, 14).Footprint())).IsEqualTo(Plan(forward));
    }

    /// <summary>A footprint reduced to the tuple a value comparison can read, since <see cref="Footprint"/>
    /// itself carries no equality of its own.</summary>
    private static (int MinX, int MinZ, int Width, int Depth)? Plan(Footprint? footprint)
        => footprint is null ? null : (footprint.MinX, footprint.MinZ, footprint.Width, footprint.Depth);

    [Test]
    public async Task A_rectangle_too_small_to_hold_two_walls_and_an_inside_is_no_footprint_at_all()
    {
        await Assert.That(House(0, 0, 1, 8).Footprint()).IsNull();     // two blocks across
        await Assert.That(House(0, 0, 2, 2).Footprint()).IsNotNull();  // the smallest house there is
        await Assert.That(new HouseProp { Wings = [new AuthoredWing([[0, 0]])] }.Footprint()).IsNull();
        await Assert.That(new HouseProp { Wings = [] }.Footprint()).IsNull();
    }

    [Test]
    public async Task A_second_wing_too_small_to_hold_two_walls_and_an_inside_is_no_footprint_either()
    {
        // The rule is per wing, not just over the plan as a whole: a sliver a room can never compose out of is
        // no more a wing than it is a building on its own. Both wings abut the hall's whole north edge, so the
        // only thing separating the two cases is the sliver's own width.
        await Assert.That(House("h", null, (0, 0, 8, 8), (2, 9, 3, 16)).Footprint()).IsNull();
        await Assert.That(House("h", null, (0, 0, 8, 8), (2, 9, 6, 16)).Footprint()).IsNotNull();
    }

    /// <summary>
    /// <b>A plan the joint model refuses is refused here, and says which rule it broke.</b> Two rectangles an
    /// author dragged into each other, or half onto each other, or side by side with their ridges parallel are
    /// each a shape no building is made of — and a prop that merely declined to stamp would leave the author
    /// looking at bare ground with nothing said. The rule id is what carries across to a canvas or an agent.
    /// </summary>
    [Test]
    [Arguments("overlapping", 6, 5, 10, 12, "HJ1")]
    [Arguments("half on", 6, 7, 14, 12, "HJ2")]
    [Arguments("side by side, ridges parallel", 0, 7, 10, 11, "HJ3")]
    public async Task A_plan_whose_wings_make_no_building_is_refused_by_rule(
        string shape, int minX, int minZ, int maxX, int maxZ, string rule)
    {
        var prop = House("h", null, (0, 0, 10, 6), (minX, minZ, maxX, maxZ));

        await Assert.That((shape, prop.Footprint())).IsEqualTo((shape, (Footprint?)null));
        await Assert.That((shape, prop.Check().SingleOrDefault()?.Rule)).IsEqualTo((shape, rule));
        await Assert.That(prop.Check().Single().Message).IsNotEmpty();
    }

    /// <summary>A plan that <em>is</em> a building says so by having nothing to say — the same L every other
    /// test here builds on, and the one shape the three above are each a spoiled copy of.</summary>
    [Test]
    public async Task A_plan_whose_wings_make_a_building_carries_no_fault()
    {
        await Assert.That(House("h", null, (0, 0, 10, 6), (0, 7, 5, 13)).Check()).IsEmpty();
        await Assert.That(House(0, 0, 8, 8).Check()).IsEmpty();
    }

    [Test]
    public async Task A_rectangle_larger_than_a_small_house_is_no_footprint_either()
    {
        // The prop is for small houses, so it carries a ceiling as well as a floor: three times the 8x8 shell a
        // wool cage is stamped in, because scenery covering much more than a few of those competes with the
        // objectives for the ground. Area rather than a side length, so a long low building is as buildable as
        // a square one — height is the roof's business and is bounded over the shorter side.
        await Assert.That(House(0, 0, 11, 15).Footprint()).IsNotNull();   // 12x16 = 192, the largest there is
        await Assert.That(House(0, 0, 15, 11).Footprint()).IsNotNull();   // the same rectangle turned
        await Assert.That(House(0, 0, 13, 12).Footprint()).IsNotNull();   // 14x13 = 182, a different shape of it
        await Assert.That(House(0, 0, 11, 16).Footprint()).IsNull();      // 12x17 = 204
        await Assert.That(House(0, 0, 19, 29).Footprint()).IsNull();      // 20x30 = 600, a building, not scenery
    }

    [Test]
    public async Task The_cap_is_the_ground_the_wings_actually_cover_not_the_box_drawn_round_them()
    {
        // A long, shallow wing off the end of a deeper one spans a box far past the cap — the box is 25x12 =
        // 300 — but the two wings themselves cover far less of it: the notch between them is ground the
        // building never stands on, and a building of several wings is held to the same number as one, over
        // the ground it actually claims rather than the rectangle drawn round the whole plan.
        var oversizedBox = House("h", null, (0, 0, 5, 11), (6, 0, 24, 3));
        var plan = oversizedBox.Footprint();
        await Assert.That(plan).IsNotNull();
        await Assert.That(plan!.Cells().Count()).IsLessThanOrEqualTo(HouseProp.MaxFootprint);
        await Assert.That(plan.Cells().Count()).IsLessThan((24 - 0 + 1) * (11 - 0 + 1));
    }

    [Test]
    public async Task A_building_past_the_cap_raises_nothing_rather_than_raising_part_of_one()
    {
        var (world, top) = Plateau(size: 96);
        var tally = Decorator.Decorate(world, Context(top, [House(-20, -20, 10, 20)]));   // 31x41
        await Assert.That(tally.Houses).IsEqualTo(0);
        await Assert.That(Height(world, 0, 0)).IsEqualTo(0);
    }

    [Test]
    public async Task The_cap_is_the_props_own_and_never_the_stampers()
    {
        // A wool cage and a spawn cube go through the same stamper, and their footprints come from the plan
        // piece they sit on (WX1) — a map's own geometry, which a dressing limit has no business refusing.
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, 0, 0, 40, 40, 64, new HouseStyle());    // 1600 blocks, far past the prop cap
        await Assert.That(world.GetBlock(0, 65, 0).Id).IsNotEqualTo(Blocks.Air);
    }

    // ── the placement ──────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_building_stands_on_the_rectangle_that_was_dragged_and_nowhere_else()
    {
        var (world, top) = Plateau();
        var tally = Decorator.Decorate(world, Context(top, [House(2, 3, 10, 9)]));

        await Assert.That(tally.Houses).IsEqualTo(1);
        await Assert.That(Height(world, 2, 3)).IsGreaterThan(8);       // a corner of the rectangle
        await Assert.That(Height(world, 10, 9)).IsGreaterThan(8);      // the opposite one
        await Assert.That(Height(world, 12, 9)).IsEqualTo(0);          // two blocks past it, untouched
    }

    // ── a building of more than one wing (G177) ───────────────────────────────────────────────────────
    [Test]
    public async Task Two_touching_wings_of_one_prop_stand_as_one_house_rather_than_colliding()
    {
        // The overlap rule's other half: two authored rectangles that overlap are two buildings colliding
        // (MG7), but a wing that shares its edge cells with its own prop's other wing is not a second building
        // — it never reaches the taken set at all, since the whole plan is composed and checked once.
        var (world, top) = Plateau();
        var ell = House("h", null, (0, 0, 10, 6), (0, 7, 5, 13));
        var tally = Decorator.Decorate(world, Context(top, [ell]));

        await Assert.That(tally.Houses).IsEqualTo(1);
        await Assert.That(Height(world, 5, 3)).IsGreaterThan(8);     // inside the hall
        await Assert.That(Height(world, 3, 10)).IsGreaterThan(8);    // inside the cross wing
        await Assert.That(Height(world, 3, 6)).IsGreaterThan(8);     // the crook where the two wings meet
    }

    [Test]
    public async Task A_house_of_two_wings_placed_through_the_decorator_is_sealed_and_stands_on_six_posts()
    {
        // The stamp itself is proven closed and six-posted by HouseStamperTests; this is the round trip through
        // the authoring shape and the symmetry turn, so a regression in how the Decorator composes and turns
        // the plan is caught here rather than only at the stamper's own, narrower door.
        var (world, top) = Plateau();
        var ell = House("h", null, new HouseStyle { Door = DoorMaterial.StainedGlass }, (0, 0, 10, 6), (0, 7, 5, 13));
        Decorator.Decorate(world, Context(top, [ell]));

        var floorY = 7;   // Plateau's flat surface top (8) minus one — Ground seats the floor a course under it
        var posts = 0;
        for (var x = -1; x <= 11; x++)
            for (var z = -1; z <= 13; z++)
                if (world.GetBlock(x, floorY + 3, z).Id == Blocks.Log) posts++;
        await Assert.That(posts).IsEqualTo(6);

        // Started deep in the crook, the cell furthest from either wing's own straight run of wall.
        var start = (3, floorY + 1, 4);
        var seen = new HashSet<(int X, int Y, int Z)> { start };
        var queue = new Queue<(int X, int Y, int Z)>([start]);
        var escaped = false;
        while (queue.Count > 0 && !escaped)
        {
            var (x, y, z) = queue.Dequeue();
            if (x < -6 || x > 16 || z < -6 || z > 18 || y > 40) { escaped = true; break; }
            foreach (var (dx, dy, dz) in new[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) })
            {
                var next = (x + dx, y + dy, z + dz);
                if (world.GetBlock(next.Item1, next.Item2, next.Item3).Id != Blocks.Air) continue;
                if (seen.Add(next)) queue.Enqueue(next);
            }
        }
        await Assert.That(escaped).IsFalse();
    }

    [Test]
    public async Task An_authored_building_is_never_refused_by_the_protected_mask()
    {
        // The mask keeps a *scatter* off the cells the map is played through, because a scatter is generated
        // and has to be told where not to grow. Someone drew this rectangle here on purpose, and a refusal
        // would silently drop a placement they can see on the canvas.
        var (world, top) = Plateau();
        var tally = Decorator.Decorate(world, Context(top, [House(2, 3, 10, 9)], isProtected: (_, _) => true));

        await Assert.That(tally.Houses).IsEqualTo(1);
        await Assert.That(Height(world, 2, 3)).IsGreaterThan(8);
    }

    [Test]
    public async Task A_building_claims_its_own_ground_so_nothing_grows_through_the_walls()
    {
        // The other half of the mask question, and the half that does apply: a building's cells join the pass's
        // running claim exactly as a path's do, so the cover placed after it stops at the walls.
        var (world, top) = Plateau();
        var flora = new FloraProp
        {
            Id = "f1", Points = [[-20, -20], [20, -20], [20, 20], [-20, 20]],
            Spec = new FloraSpec(Coverage: 1.0, FlowerShare: 0, TallShare: 0),
        };
        Decorator.Decorate(world, Context(top, [House(2, 3, 10, 9), flora]));

        // The course a plant would stand in, inside the building's footprint, holds the building and not a fern.
        for (var x = 3; x <= 9; x++)
            for (var z = 4; z <= 8; z++)
                await Assert.That(BlockRoles.IsFlora(world.GetBlock(x, 8, z).Id)).IsFalse();
    }

    [Test]
    public async Task A_building_with_no_ground_under_it_raises_nothing()
    {
        // Not policy — physics. A footprint over the void has nothing to stand on, and a floor cannot be
        // seated at a course that does not exist.
        var world = new VoxelWorld();
        var tally = Decorator.Decorate(world, Context([], [House(2, 3, 10, 9)]));
        await Assert.That(tally.Houses).IsEqualTo(0);
    }

    [Test]
    public async Task A_building_settles_into_a_slope_rather_than_standing_over_its_low_side()
    {
        // Seated on the *lowest* column it covers, the rule every prop follows: the alternative leaves the
        // downhill wall hanging in the air on stilts.
        var (world, top) = Plateau();
        for (var x = 6; x <= 10; x++)
            for (var z = 3; z <= 9; z++)
            {
                world.SetBlock(x, 8, z, Blocks.Stone);
                world.SetBlock(x, 9, z, Blocks.Grass);
                top[(x, z)] = 10;                       // that half of the footprint stands two blocks higher
            }

        Decorator.Decorate(world, Context(top, [House(2, 3, 10, 9)]));
        // The floor is one course under the low ground, so the building sinks into the high side instead.
        await Assert.That(world.GetBlock(2, 7, 3).Id).IsNotEqualTo(Blocks.Air);
    }

    // ── the fan ────────────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_building_is_raised_at_every_image_of_its_orbit()
    {
        var (world, top) = Plateau();
        var tally = Decorator.Decorate(world, Context(top, [House(4, 4, 12, 10)], symmetry: "rot_180"));

        await Assert.That(tally.Houses).IsEqualTo(2);
        await Assert.That(Height(world, 6, 6)).IsGreaterThan(8);
        await Assert.That(Height(world, -6, -6)).IsGreaterThan(8);     // the other team's, about the origin
    }

    [Test]
    public async Task A_quarter_turn_swaps_a_buildings_width_and_depth()
    {
        // The whole reason the footprint is stored as two corners: a rectangle turned ninety degrees is a
        // rectangle whose width and depth have swapped, and taking the corners round the orbit says that
        // without the stamp having to be told it happened.
        var symmetry = new DressingSymmetry("rot_90", 0, 0);
        var turned = new HouseProp
        {
            Wings = [new AuthoredWing(symmetry.ImageRing(House(2, 2, 12, 6).Wings[0].Corners, 1))],
        }.Footprint();

        await Assert.That(turned).IsNotNull();
        await Assert.That(turned!.Width).IsEqualTo(5);           // was the depth
        await Assert.That(turned.Depth).IsEqualTo(11);           // was the width
    }

    /// <summary>
    /// <b>A wing's own statements turn with it, and its ridge turns too.</b> Every image of a building is the
    /// same building, so a wing that projects still projects and a wing roofed as a hip is still hipped — those
    /// are what the building is, not where it stands. The ridge is the one of them a quarter turn <em>changes</em>:
    /// stated along x it comes out along z, and dropped instead it re-reads from the turned proportions — which
    /// on a square wing ties, turning a T into two ranges side by side and losing the junction entirely.
    /// </summary>
    [Test]
    public async Task A_turned_wing_keeps_what_it_states_and_turns_its_ridge_with_it()
    {
        var symmetry = new DressingSymmetry("rot_90", 0, 0);
        var plan = new Footprint([
            new Wing(0, 6, 9, 10),
            new Wing(0, 0, 4, 4, new WingSpec(StoreysHigh: 2, Form: RoofForm.Hip, Ridge: RidgeAxis.AlongZ, Projects: true)),
        ]);

        var turned = Decorator.TurnedFootprint(plan, symmetry, 1).Wings[1];

        await Assert.That(turned.Ridge).IsEqualTo(RidgeAxis.AlongX);
        await Assert.That((turned.StoreysHigh, turned.Form, turned.Projects))
            .IsEqualTo((2, (RoofForm?)RoofForm.Hip, true));

        // Turned four times it is back where it started, ridge included — a quarter turn that only sometimes
        // swapped the axis would not close the orbit.
        var round = plan;
        for (var k = 0; k < 4; k++) round = Decorator.TurnedFootprint(round, symmetry, 1);
        await Assert.That(round.Wings[1].Ridge).IsEqualTo(RidgeAxis.AlongZ);

        // A wing that left its ridge to its proportions has none to carry, and re-reads it from the turn.
        var loose = Decorator.TurnedFootprint(new Footprint([new Wing(0, 0, 9, 4)]), symmetry, 1).Wings[0];
        await Assert.That(loose.Ridge).IsNull();
        await Assert.That(loose.RidgeAlongX).IsFalse();          // ten deep and five wide once turned
    }

    [Test]
    [Arguments("mirror_x", RoomEdge.NegX, RoomEdge.PosX)]
    [Arguments("mirror_z", RoomEdge.NegZ, RoomEdge.PosZ)]
    [Arguments("rot_180", RoomEdge.NegZ, RoomEdge.PosZ)]
    [Arguments("rot_90", RoomEdge.NegZ, RoomEdge.PosX)]
    public async Task A_doorway_turns_with_the_building_it_is_cut_in(string mode, RoomEdge front, RoomEdge image)
    {
        // Fanning the rectangle alone puts a copy on the far side of the map with its door still on the same
        // compass side — so a mirrored pair face the same way and one team walks out toward the other's half.
        var symmetry = new DressingSymmetry(mode, 0, 0);
        await Assert.That(symmetry.TurnEdge(front, 1)).IsEqualTo(image);
        await Assert.That(symmetry.TurnEdge(front, 0)).IsEqualTo(front);

        // And the turn composes: the k-th image's wall is the first image's turned k-1 times more, which is
        // what makes a quarter turn a quarter turn rather than a lookup table that happens to agree twice.
        var walked = front;
        for (var k = 1; k < symmetry.Order; k++)
        {
            walked = symmetry.TurnEdge(walked, 1);
            await Assert.That(symmetry.TurnEdge(front, k)).IsEqualTo(walked);
        }
    }

    [Test]
    public async Task A_chosen_wall_is_the_wall_the_doorway_is_actually_cut_in()
    {
        var (world, top) = Plateau();
        Decorator.Decorate(world, Context(top, [House(2, 3, 12, 11, RoomEdge.PosX)]));

        // An open door on the +x wall, and none on the −x wall opposite it.
        var openOnPosX = 0;
        var openOnNegX = 0;
        for (var z = 4; z <= 10; z++)
            for (var y = 9; y <= 11; y++)
            {
                if (world.GetBlock(12, y, z).Id == Blocks.Air) openOnPosX++;
                if (world.GetBlock(2, y, z).Id == Blocks.Air) openOnNegX++;
            }

        await Assert.That(openOnPosX).IsGreaterThan(0);
        await Assert.That(openOnNegX).IsEqualTo(0);
    }
}

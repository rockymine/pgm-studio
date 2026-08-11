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
        Points = [[minX, minZ], [maxX, maxZ]],
        Front = front,
        Style = new HouseStyle { Door = DoorMaterial.Air },
    };

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
        await Assert.That(forward).IsEqualTo((4, 6, 9, 9));
        await Assert.That(House(12, 14, 4, 6).Footprint()).IsEqualTo(forward);
        await Assert.That(House(12, 6, 4, 14).Footprint()).IsEqualTo(forward);
    }

    [Test]
    public async Task A_rectangle_too_small_to_hold_two_walls_and_an_inside_is_no_footprint_at_all()
    {
        await Assert.That(House(0, 0, 1, 8).Footprint()).IsNull();     // two blocks across
        await Assert.That(House(0, 0, 2, 2).Footprint()).IsNotNull();  // the smallest house there is
        await Assert.That(new HouseProp { Points = [[0, 0]] }.Footprint()).IsNull();
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
        var turned = new HouseProp { Points = symmetry.ImageRing(House(2, 2, 12, 6).Points, 1) }.Footprint();

        await Assert.That(turned).IsNotNull();
        await Assert.That(turned!.Value.Width).IsEqualTo(5);           // was the depth
        await Assert.That(turned.Value.Depth).IsEqualTo(11);           // was the width
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

using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Views;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The writing half of block geometry, checked against the reading half.
///
/// <para><see cref="BlockGeometry"/> composes a data nibble and <see cref="BlockShapes"/> takes one apart, and
/// nothing but a shared understanding of two bits and a flag keeps them agreeing. So the assertions here go
/// through the reader rather than against a number: a stair turned toward +x has to come back as a stair whose
/// raised half is on the right, or one of the two halves is wrong and it does not matter which.</para>
/// </summary>
public class BlockGeometryTests
{
    /// <summary>The rectangle a block's raised half draws as, from the reader — the second piece of a stair,
    /// which is the quarter that says which way it climbs.</summary>
    private static BlockPiece Raised(int data) => BlockShapes.Of(Blocks.OakStairs, data).Last();

    /// <summary>
    /// <b>A stair climbs toward the side it is turned to.</b> The reader draws the raised half on the right
    /// for east and south, so the two axes' positive directions both come back rightward and their negatives
    /// both come back left — which is the whole of what a facing means.
    /// </summary>
    [Test]
    [Arguments(RoomEdge.PosX, true)]
    [Arguments(RoomEdge.NegX, false)]
    [Arguments(RoomEdge.PosZ, true)]
    [Arguments(RoomEdge.NegZ, false)]
    public async Task A_stair_is_turned_toward_the_side_it_climbs(RoomEdge toward, bool rightward)
    {
        var raised = Raised(BlockGeometry.Stair(toward));

        await Assert.That((toward, raised.Left > 0)).IsEqualTo((toward, rightward));
        await Assert.That((toward, raised.Top)).IsEqualTo((toward, 0.0));   // upright: the raised half is on top
    }

    /// <summary>Hung upside down, the same stair keeps the side it climbs toward and moves its full half to the
    /// top of the cube — the step hanging below is what an arch's corner is made of.</summary>
    [Test]
    public async Task An_upside_down_stair_keeps_its_facing_and_hangs_its_step()
    {
        var upright = BlockGeometry.Stair(RoomEdge.PosX);
        var hung = BlockGeometry.Stair(RoomEdge.PosX, upsideDown: true);

        await Assert.That(hung & 3).IsEqualTo(upright & 3);                 // the same two facing bits
        await Assert.That(Raised(hung).Top).IsEqualTo(0.5);                 // the raised quarter is now below
        await Assert.That(Raised(hung).Left).IsEqualTo(Raised(upright).Left);
    }

    /// <summary>A slab fills the upper or the lower half of its cube and keeps the three low bits saying what
    /// it is made of — so a lintel and a sill of the same wood differ by one bit and nothing else.</summary>
    [Test]
    public async Task A_slab_takes_a_half_and_keeps_what_it_is_made_of()
    {
        const int spruce = 1;
        var sill = BlockGeometry.Slab(spruce, upper: false);
        var lintel = BlockGeometry.Slab(spruce, upper: true);

        await Assert.That((sill & 0x7, lintel & 0x7)).IsEqualTo((spruce, spruce));
        await Assert.That(BlockShapes.Of(Blocks.WoodenSlab, sill).Single().Top).IsEqualTo(0.5);
        await Assert.That(BlockShapes.Of(Blocks.WoodenSlab, lintel).Single().Top).IsEqualTo(0.0);

        // A variant wider than the three bits it has cannot reach the half flag and turn a sill into a lintel.
        await Assert.That(BlockGeometry.Slab(0xF, upper: false)).IsEqualTo(0x7);
    }

    /// <summary>
    /// <b>Two corner stairs turn back to back, whichever axis the run lies on.</b> That pair is the shape an
    /// arch's head and a lattice's upper course are both made of, and it was written out three times before it
    /// was written down once — each stair climbing toward its own end, so the quarter each is missing faces
    /// the other and the light is between them.
    /// </summary>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task The_two_corners_of_a_run_face_away_from_each_other(bool alongX)
    {
        var low = BlockGeometry.CornerStair(alongX, atLowEnd: true);
        var high = BlockGeometry.CornerStair(alongX, atLowEnd: false);

        await Assert.That((alongX, Raised(low).Left > 0)).IsEqualTo((alongX, false));
        await Assert.That((alongX, Raised(high).Left > 0)).IsEqualTo((alongX, true));
        await Assert.That((alongX, Raised(low).Top, Raised(high).Top)).IsEqualTo((alongX, 0.5, 0.5));
    }

    /// <summary>
    /// <b>One arch, and the span is the whole of what a door's and a window's differ by.</b> Both round the
    /// two top corners off the same way; over a doorway the middle is a beam carrying the wall, and over a
    /// window it is light, because there is no wall there to carry.
    /// </summary>
    [Test]
    public async Task An_arch_differs_over_a_door_and_a_window_only_in_what_spans_it()
    {
        var beam = new ArchSpan(Blocks.WoodenSlab, BlockGeometry.Slab(0, upper: true));

        for (var step = 0; step < 4; step++)
        {
            var over = Arch.Piece(alongX: true, step, width: 4, Blocks.OakStairs, beam);
            var under = Arch.Piece(alongX: true, step, width: 4, Blocks.OakStairs, ArchSpan.Open);
            var corner = step is 0 or 3;

            await Assert.That((step, over == under)).IsEqualTo((step, corner));
            if (corner) continue;
            await Assert.That((step, over.Id, under.Id)).IsEqualTo((step, Blocks.WoodenSlab, Blocks.Air));
        }

        // Two cells wide is corners and nothing else, so the two arches are the same block for block.
        for (var step = 0; step < 2; step++)
            await Assert.That(Arch.Piece(true, step, 2, Blocks.OakStairs, beam))
                .IsEqualTo(Arch.Piece(true, step, 2, Blocks.OakStairs, ArchSpan.Open));
    }

    /// <summary>A half-turn of the plan: both axes reversed.</summary>
    private static (int X, int Z) HalfTurn(int dx, int dz) => (-dx, -dz);

    /// <summary>A quarter-turn of the plan, the orbit a four-team board is fanned round.</summary>
    private static (int X, int Z) QuarterTurn(int dx, int dz) => (-dz, dx);

    /// <summary>
    /// <b>A vine clings to the side a turn puts it on.</b> Its data is a mask of sides rather than one
    /// facing, so a half-turn has to send south to north and west to east — and leave the two opposite
    /// <em>pairs</em> alone, which is the property a board authored on a <c>rot_180</c> orbit relies on to
    /// state one vine for both halves.
    /// </summary>
    [Test]
    [Arguments(1, 4)]     // south  -> north
    [Arguments(4, 1)]     // north  -> south
    [Arguments(2, 8)]     // west   -> east
    [Arguments(8, 2)]     // east   -> west
    [Arguments(5, 5)]     // north|south, invariant under a half-turn
    [Arguments(10, 10)]   // west|east, the same
    [Arguments(0, 0)]     // hanging from the block above: no side to turn
    public async Task A_vine_clings_to_the_side_a_half_turn_puts_it_on(int data, int expected)
    {
        await Assert.That((data, BlockGeometry.Turned(Blocks.Vine, data, HalfTurn))).IsEqualTo((data, expected));
    }

    /// <summary>
    /// <b>A fronted block looks the way the turn points it.</b> A ladder, a chest and a wall sign share one
    /// four-number table, so the assertion goes through <see cref="BlockGeometry.Fronting"/> rather than
    /// against a literal: a ladder looking north, turned a half-turn, is the number that means south.
    /// </summary>
    [Test]
    [Arguments(RoomEdge.NegZ, RoomEdge.PosZ)]
    [Arguments(RoomEdge.PosZ, RoomEdge.NegZ)]
    [Arguments(RoomEdge.NegX, RoomEdge.PosX)]
    [Arguments(RoomEdge.PosX, RoomEdge.NegX)]
    public async Task A_fronted_block_looks_the_way_a_half_turn_points_it(RoomEdge looks, RoomEdge turned)
    {
        foreach (var id in new[] { Blocks.Ladder, Blocks.WallSign, Blocks.Chest })
            await Assert.That((id, BlockGeometry.Turned(id, BlockGeometry.Fronting(looks), HalfTurn)))
                .IsEqualTo((id, BlockGeometry.Fronting(turned)));
    }

    /// <summary>
    /// <b>Four quarter-turns are no turn at all</b>, for every kind of data that carries a direction. A
    /// quarter-turn board fans a copied body through all four images, so a turn that is not a group action
    /// lands the fourth image somewhere the first one is not.
    /// </summary>
    [Test]
    public async Task Four_quarter_turns_return_a_directed_block_to_itself()
    {
        (int Id, int Data)[] directed =
        [
            (Blocks.Vine, 1), (Blocks.Vine, 2), (Blocks.Vine, 5), (Blocks.Vine, 11),
            (Blocks.Ladder, BlockGeometry.Fronting(RoomEdge.NegZ)),
            (Blocks.Chest, BlockGeometry.Fronting(RoomEdge.PosX)),
            (Blocks.OakStairs, BlockGeometry.Stair(RoomEdge.NegX)),
            (Blocks.OakStairs, BlockGeometry.Stair(RoomEdge.PosZ, upsideDown: true)),
            (Blocks.Log, 4), (Blocks.Log, 8), (Blocks.Log, 0), (Blocks.Log, 12),
        ];

        foreach (var (id, data) in directed)
        {
            var turned = data;
            for (var quarter = 0; quarter < 4; quarter++) turned = BlockGeometry.Turned(id, turned, QuarterTurn);
            await Assert.That((id, data, turned)).IsEqualTo((id, data, data));
        }
    }

    /// <summary>
    /// <b>A quarter-turn actually moves a face.</b> The half-turn cases above pass on a block whose data is
    /// simply carried through whenever the two directions happen to coincide, so the discriminating check is
    /// that one quarter-turn lands a south vine on the west face and a north ladder looking east.
    /// </summary>
    [Test]
    public async Task A_quarter_turn_moves_a_face_off_its_own_axis()
    {
        await Assert.That(BlockGeometry.Turned(Blocks.Vine, 1, QuarterTurn)).IsEqualTo(2);          // south -> west
        await Assert.That(BlockGeometry.Turned(Blocks.Vine, 5, QuarterTurn)).IsEqualTo(10);         // north|south -> west|east
        await Assert.That(BlockGeometry.Turned(Blocks.Ladder, BlockGeometry.Fronting(RoomEdge.NegZ), QuarterTurn))
            .IsEqualTo(BlockGeometry.Fronting(RoomEdge.PosX));                                      // north -> east
    }

    /// <summary>A fronted block's non-geometry bits ride through a turn: a dropper's triggered flag is not a
    /// direction, and a floor skull's rotation is in its tile entity rather than its nibble.</summary>
    [Test]
    public async Task A_fronted_block_carries_what_is_not_a_facing()
    {
        const int Dropper = 158, Triggered = 8, Skull = 144, OnFloor = 1;

        await Assert.That(BlockGeometry.Turned(Dropper, BlockGeometry.Fronting(RoomEdge.NegZ) | Triggered, HalfTurn))
            .IsEqualTo(BlockGeometry.Fronting(RoomEdge.PosZ) | Triggered);
        await Assert.That(BlockGeometry.Turned(Skull, OnFloor, QuarterTurn)).IsEqualTo(OnFloor);
    }
}

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
}

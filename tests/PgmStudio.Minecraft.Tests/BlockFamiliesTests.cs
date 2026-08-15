using PgmStudio.Minecraft.Palette;
namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The families themselves are a list of ids and a list is not worth asserting back. What is worth asserting is
/// where a family and the <see cref="BlockRoles"/> vocabulary have to <b>agree</b> — because until they were
/// composed from one source they agreed only by nobody having edited one of them, and every one of these
/// crossings was a place a divergence would have shown up as a wrong build rather than a failed test.
/// </summary>
public sealed class BlockFamiliesTests
{
    [Test]
    public async Task A_double_slab_is_never_a_slab()
    {
        // The confusion that has a visible consequence: a double slab ignores the half bit and renders as the
        // full cube, so a door head or a window band that accepted one would build a solid lintel.
        foreach (var id in BlockFamilies.DoubleSlabs)
        {
            await Assert.That(BlockFamilies.IsSlab(id)).IsFalse();
            await Assert.That(BlockFamilies.IsDoubleSlab(id)).IsTrue();
        }
        await Assert.That(BlockFamilies.SingleSlabs.Overlaps(BlockFamilies.DoubleSlabs)).IsFalse();
    }

    [Test]
    public async Task Everything_thinner_than_its_block_reads_as_not_filling_its_cube()
    {
        // The crossing that was two hand-written lists: BlockRoles' partial materials were exactly these three
        // families spelled out again as literals, and a stair added to one and not the other would have read as
        // a solid block to every pass asking about shape.
        foreach (var id in BlockFamilies.Stairs.Concat(BlockFamilies.SingleSlabs).Concat(BlockFamilies.Panes))
            await Assert.That(BlockRoles.IsFullCube(id)).IsFalse();
    }

    [Test]
    public async Task A_double_slab_fills_its_cube_where_a_single_one_does_not()
        => await Assert.That(BlockFamilies.DoubleSlabs.All(BlockRoles.IsFullCube)).IsTrue();

    [Test]
    public async Task Wood_and_leaves_are_the_tree_role_and_are_not_each_other()
    {
        foreach (var id in BlockFamilies.Logs.Concat(BlockFamilies.Leaves))
            await Assert.That(BlockRoles.Of(id)).IsEqualTo(BlockRole.Tree);

        await Assert.That(BlockFamilies.Logs.Overlaps(BlockFamilies.Leaves)).IsFalse();
        await Assert.That(BlockFamilies.Logs.Any(BlockFamilies.IsLeaf)).IsFalse();
    }

    [Test]
    public async Task A_pass_reading_a_build_looks_through_a_leaf_and_never_through_a_log()
    {
        // The one asymmetry the two families exist to keep: a corner post is a log, so a roof finder that saw
        // through logs would lose the measure telling a framed house from a shed.
        await Assert.That(BlockFamilies.Leaves.All(BlockRoles.SeenThrough)).IsTrue();
        await Assert.That(BlockFamilies.Logs.Any(BlockRoles.SeenThrough)).IsFalse();
    }

    [Test]
    public async Task Soil_is_terrain_and_never_a_built_surface()
    {
        // What the roof rule rests on: soil is what a building stands on, so none of it may also read as the
        // material signature of something built.
        await Assert.That(BlockFamilies.Soil.Any(BlockRoles.IsBuilt)).IsFalse();
        await Assert.That(BlockFamilies.Soil.All(BlockRoles.IsNaturalGround)).IsTrue();
    }

    [Test]
    public async Task Soil_and_natural_ground_answer_different_questions()
    {
        // The pair whose names used to be IsGround and IsNaturalGround. Soil is a closed list of six; natural
        // ground is everything left after built, liquid, log and grown are taken away — so stone is one and not
        // the other, which is exactly the difference a caller could not see at the call site.
        await Assert.That(BlockRoles.IsNaturalGround(Blocks.Stone)).IsTrue();
        await Assert.That(BlockFamilies.IsSoil(Blocks.Stone)).IsFalse();
    }
}

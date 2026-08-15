using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The one classification every analysis pass composes from. What is gated here is the part that used to be
/// carried eight times over and disagreed each time: which blocks stand on the ground, which of those a pass
/// reading a build may look through, and which fill their cube.
/// </summary>
public sealed class BlockRolesTests
{
    [Test]
    public async Task A_block_has_exactly_one_role()
    {
        // Every id in the legacy range answers, and the answer is a single role — the composites below are
        // built from these, so an id in two buckets would be double-counted by every one of them.
        for (var id = 0; id < 256; id++)
        {
            var role = BlockRoles.Of(id);
            var claims = new[]
            {
                role == BlockRole.Liquid == BlockRoles.IsLiquid(id),
                role == BlockRole.Tree == BlockRoles.IsTree(id),
                role == BlockRole.Flora == BlockRoles.IsFlora(id),
                role == BlockRole.Decoration == BlockRoles.IsDecoration(id),
            };
            await Assert.That(claims.All(agrees => agrees)).IsTrue();
        }
    }

    [Test]
    public async Task Air_and_liquid_are_not_things_standing_on_the_ground()
    {
        foreach (var id in new[] { 0, 8, 9, 10, 11 })
        {
            await Assert.That(BlockRoles.StandsOnGround(id)).IsFalse();
            await Assert.That(BlockRoles.SeenThrough(id)).IsFalse();
        }
    }

    [Test]
    public async Task A_log_is_a_tree_to_the_ground_and_a_post_to_a_build()
    {
        // The distinction the old per-render lists carried by accident and never stated: a pass reading the
        // ground steps past a trunk with the rest of its tree, and a pass reading the shape of a build must see
        // it, because the corner posts of a house are logs.
        foreach (var log in new[] { 17, 162 })
        {
            await Assert.That(BlockFamilies.IsLog(log)).IsTrue();
            await Assert.That(BlockRoles.StandsOnGround(log)).IsTrue();
            await Assert.That(BlockRoles.SeenThrough(log)).IsFalse();
        }
        foreach (var leaf in new[] { 18, 161 })
        {
            await Assert.That(BlockFamilies.IsLeaf(leaf)).IsTrue();
            await Assert.That(BlockRoles.StandsOnGround(leaf)).IsTrue();
            await Assert.That(BlockRoles.SeenThrough(leaf)).IsTrue();
        }
    }

    [Test]
    public async Task The_blocks_that_were_in_no_list_at_all_now_have_a_role()
    {
        // These fifteen were in none of the eight lists, so every pass that met one read it as terrain — which
        // is exactly why mob heads and daylight sensors were reported as ground.
        foreach (var id in new[] { 81, 113, 116, 117, 122, 140, 144, 145, 151, 176, 177, 178, 52, 171, 27 })
            await Assert.That(BlockRoles.StandsOnGround(id)).IsTrue();
    }

    [Test]
    public async Task Fullness_is_shape_and_not_role()
    {
        // A dropper is placed and fills its cube; a stone slab is a material and does not. Neither answer can be
        // read off the other, which is the whole reason fullness is its own question.
        await Assert.That(BlockRoles.Of(158)).IsEqualTo(BlockRole.Decoration);
        await Assert.That(BlockRoles.IsFullCube(158)).IsTrue();
        await Assert.That(BlockRoles.Of(44)).IsEqualTo(BlockRole.Material);
        await Assert.That(BlockRoles.IsFullCube(44)).IsFalse();
    }

    [Test]
    public async Task A_double_slab_is_whole_where_its_single_is_not()
    {
        foreach (var (whole, half) in new[] { (43, 44), (125, 126), (181, 182) })
        {
            await Assert.That(BlockRoles.IsFullCube(whole)).IsTrue();
            await Assert.That(BlockRoles.IsFullCube(half)).IsFalse();
        }
    }

    [Test]
    public async Task Nothing_that_grows_fills_its_cube()
    {
        for (var id = 0; id < 256; id++)
            if (BlockRoles.IsFlora(id))
                await Assert.That(BlockRoles.IsFullCube(id)).IsFalse();
    }

    [Test]
    public async Task Every_stair_and_pane_is_partial()
    {
        foreach (var id in new[] { 53, 67, 108, 109, 114, 128, 134, 135, 136, 156, 163, 164, 180, 102, 160 })
            await Assert.That(BlockRoles.IsFullCube(id)).IsFalse();
    }

    [Test]
    public async Task Every_block_a_tone_family_names_fills_its_cube()
    {
        // The ground vocabulary names only full blocks. Stated in TerrainPalette's prose and enforced there by
        // hand-picking ids; this is the check that the two never drift, in either direction.
        foreach (var family in TerrainPalette.Families)
            foreach (var block in family.Blocks)
                await Assert.That(BlockRoles.IsFullCube(block.Id)).IsTrue();
    }

    [Test]
    public async Task No_block_a_tone_family_names_stands_on_the_ground()
    {
        // Ground is what a family names; a thing standing on ground is what the other roles name. An id in both
        // would have the surface report claiming a column twice over.
        foreach (var family in TerrainPalette.Families)
            foreach (var block in family.Blocks)
                await Assert.That(BlockRoles.StandsOnGround(block.Id)).IsFalse();
    }
}

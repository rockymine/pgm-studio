using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The sky marker every goal carries: a small coloured shape floating clear of the build-height cap so it
/// stays legible and unreachable at once.
/// </summary>
public sealed class GoalMarkerStamperTests
{
    [Test]
    public async Task Cube_is_a_solid_3x3x3_block_in_the_goals_colour()
    {
        var w = new VoxelWorld();
        GoalMarkerStamper.Stamp(w, centerX: 10, centerZ: 20, floorY: 100, woolDamage: 14, GoalMarkerShape.Cube);

        var count = 0;
        for (var x = 9; x <= 11; x++)
        for (var y = 100; y <= 102; y++)
        for (var z = 19; z <= 21; z++)
            if (w.GetBlock(x, y, z).Id == Blocks.Wool) count++;
        await Assert.That(count).IsEqualTo(27);
        await Assert.That(w.GetBlock(10, 101, 20)).IsEqualTo((Blocks.Wool, 14));   // centre block, red

        // Nothing lands outside the 3×3×3 footprint.
        await Assert.That(w.GetBlock(12, 101, 20).Id).IsEqualTo(Blocks.Air);
        await Assert.That(w.GetBlock(10, 103, 20).Id).IsEqualTo(Blocks.Air);
        await Assert.That(w.GetBlock(10, 99, 20).Id).IsEqualTo(Blocks.Air);
    }

    [Test]
    public async Task Cross_is_a_seven_block_asterisk_through_the_centre()
    {
        var w = new VoxelWorld();
        GoalMarkerStamper.Stamp(w, centerX: 0, centerZ: 0, floorY: 50, woolDamage: 11, GoalMarkerShape.Cross);

        var count = 0;
        for (var x = -1; x <= 1; x++)
        for (var y = 50; y <= 52; y++)
        for (var z = -1; z <= 1; z++)
            if (w.GetBlock(x, y, z).Id == Blocks.Wool) count++;
        // A centre block plus one arm tip each way on all three axes — 7, not the full 27 of a cube.
        await Assert.That(count).IsEqualTo(7);

        await Assert.That(w.GetBlock(0, 51, 0)).IsEqualTo((Blocks.Wool, 11));    // centre
        await Assert.That(w.GetBlock(1, 51, 0)).IsEqualTo((Blocks.Wool, 11));    // +X arm
        await Assert.That(w.GetBlock(-1, 51, 0)).IsEqualTo((Blocks.Wool, 11));   // -X arm
        await Assert.That(w.GetBlock(0, 52, 0)).IsEqualTo((Blocks.Wool, 11));    // +Y arm
        await Assert.That(w.GetBlock(0, 50, 0)).IsEqualTo((Blocks.Wool, 11));    // -Y arm
        await Assert.That(w.GetBlock(0, 51, 1)).IsEqualTo((Blocks.Wool, 11));    // +Z arm
        await Assert.That(w.GetBlock(0, 51, -1)).IsEqualTo((Blocks.Wool, 11));   // -Z arm
        // A corner of the bounding cube is never touched by the cross.
        await Assert.That(w.GetBlock(1, 52, 1).Id).IsEqualTo(Blocks.Air);
    }

    [Test]
    public async Task Stamps_nothing_when_the_marker_would_not_fit_under_the_world_ceiling()
    {
        var w = new VoxelWorld();
        GoalMarkerStamper.Stamp(w, centerX: 0, centerZ: 0, floorY: VoxelWorld.MaxHeight - 1, woolDamage: 0);
        await Assert.That(w.GetBlock(0, VoxelWorld.MaxHeight - 1, 0).Id).IsEqualTo(Blocks.Air);
    }
}

using PgmStudio.Minecraft;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Cube-shell geometry: floor bedrock + 2×2 wool centre, roof bedrock + 4×4 hole, layer-6 slit, the
/// layer-4 coloured strip (wool vs stained clay), and the door variants (4 glass-pane doors vs 1 open-air).
/// Anchored at world origin corner (0,0), floor at y=64 → footprint x/z ∈ [-4, 3].
/// </summary>
public sealed class CubeStamperTests
{
    private const int Red = 14;

    [Test]
    public async Task Wool_cage_shell_places_floor_roof_slit_strip_and_glass_doors()
    {
        var w = new VoxelWorld();
        CubeStamper.Stamp(w, anchorX: 0, anchorZ: 0, floorY: 64, color: Red, CubeKind.WoolCage);

        // Floor (y=64): 2×2 wool centre at world {-1,0}×{-1,0}, bedrock elsewhere.
        await Assert.That(w.GetBlock(-1, 64, -1)).IsEqualTo((Blocks.Wool, Red));
        await Assert.That(w.GetBlock(0, 64, 0)).IsEqualTo((Blocks.Wool, Red));
        await Assert.That(w.GetBlock(-4, 64, -4)).IsEqualTo((Blocks.Bedrock, 0));

        // Roof (layer 8 → y=72): bedrock, 4×4 centre hole is air.
        await Assert.That(w.GetBlock(-4, 72, -4)).IsEqualTo((Blocks.Bedrock, 0));
        await Assert.That(w.GetBlock(-1, 72, -1)).IsEqualTo((Blocks.Air, 0));

        // Colour strip (layer 4 → y=68): wool for a cage.
        await Assert.That(w.GetBlock(-4, 68, -1)).IsEqualTo((Blocks.Wool, Red));
        // Light slit (layer 6 → y=70): perimeter is air.
        await Assert.That(w.GetBlock(-4, 70, -3)).IsEqualTo((Blocks.Air, 0));
        // Plain wall course (layer 2 → y=66): bedrock.
        await Assert.That(w.GetBlock(-4, 66, -3)).IsEqualTo((Blocks.Bedrock, 0));

        // Door: -Z wall (world z=-4), centre columns {-1,0}, layers 1-3 = stained-glass panes.
        await Assert.That(w.GetBlock(-1, 65, -4)).IsEqualTo((Blocks.StainedGlassPane, Red));
        await Assert.That(w.GetBlock(0, 67, -4)).IsEqualTo((Blocks.StainedGlassPane, Red));
        // +X wall door too (world x=3).
        await Assert.That(w.GetBlock(3, 65, -1)).IsEqualTo((Blocks.StainedGlassPane, Red));
    }

    [Test]
    public async Task Spawn_cube_uses_clay_strip_and_a_single_open_air_door()
    {
        var w = new VoxelWorld();
        CubeStamper.Stamp(w, anchorX: 0, anchorZ: 0, floorY: 64, color: Red, CubeKind.SpawnCube, Facing.NegZ);

        // Colour strip is stained clay.
        await Assert.That(w.GetBlock(-4, 68, -1)).IsEqualTo((Blocks.StainedClay, Red));
        // Floor centre still wool.
        await Assert.That(w.GetBlock(-1, 64, -1)).IsEqualTo((Blocks.Wool, Red));

        // Single 4-wide × 4-tall open-air door on the -Z wall (z=-4), columns {-2..1}, layers 1-4.
        await Assert.That(w.GetBlock(-2, 65, -4)).IsEqualTo((Blocks.Air, 0));
        await Assert.That(w.GetBlock(1, 68, -4)).IsEqualTo((Blocks.Air, 0));   // door cuts through the strip course
        // The opposite (+Z) wall has no door → strip stays clay there.
        await Assert.That(w.GetBlock(-1, 68, 3)).IsEqualTo((Blocks.StainedClay, Red));
    }
}

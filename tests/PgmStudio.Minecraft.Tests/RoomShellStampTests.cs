using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;
using PgmStudio.Geom;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Room-shell geometry over a resolved frame (docs/world-export/structures.md): floor bedrock + the wool
/// pad, roof bedrock + the capped centre hole, layer-6 slit, the layer-4 coloured strip (wool vs stained
/// clay), and doors cut on the frame's entries (glass panes vs the single open-air door). The baseline
/// frame is the shipped one — a 10×10 piece centred on (0,0) → shell x/z ∈ [-4, 3].
/// </summary>
public sealed class RoomShellStampTests
{
    private const int Red = 14;

    /// <summary>The room a style describes: its shell, and the pad the structure stampers lay over it.</summary>
    private static void Shell(VoxelWorld world, RoomFrame frame, int floorY, HouseStyle style)
    {
        HouseStamper.Stamp(world, frame, floorY, style, Red);
        PadStamp.Lay(world, frame.Pad, floorY, Red);
    }

    private static readonly (double MinX, double MinZ, double MaxX, double MaxZ) TopEntry = (-5, -5, 5, -5);

    private static RoomFrame Baseline(RoomEdge? spawnDoor = null) =>
        RoomFrames.Resolve(new BlockRect(-5, -5, 5, 5), new BlockRect(-4, -4, 4, 4), shellBound: true, 0, 0, spawnDoor is null ? [TopEntry] : [], spawnDoor is { } door ? [door] : [], out _)!;

    [Test]
    public async Task Wool_cage_shell_places_floor_roof_slit_strip_and_a_seam_door()
    {
        var w = new VoxelWorld();
        Shell(w, Baseline(), 64, HouseStyle.Wool);

        // Floor (y=64): 2×2 wool pad at world {-1,0}×{-1,0}, bedrock elsewhere.
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

        // Door on the entry seam only: -Z wall (z=-4), the WX7 4-wide door on the 6-across interior,
        // columns {-2..1}, layers 1-3 = stained-glass panes.
        await Assert.That(w.GetBlock(-2, 65, -4)).IsEqualTo((Blocks.StainedGlassPane, Red));
        await Assert.That(w.GetBlock(1, 67, -4)).IsEqualTo((Blocks.StainedGlassPane, Red));
        // No entry on the +X wall → no door there.
        await Assert.That(w.GetBlock(3, 65, -1)).IsEqualTo((Blocks.Bedrock, 0));
    }

    [Test]
    public async Task Spawn_cube_uses_clay_strip_and_a_single_open_air_door()
    {
        var w = new VoxelWorld();
        Shell(w, Baseline(RoomEdge.NegZ), 64, HouseStyle.Spawn);

        // Colour strip is stained clay.
        await Assert.That(w.GetBlock(-4, 68, -1)).IsEqualTo((Blocks.StainedClay, Red));
        // Floor pad still wool.
        await Assert.That(w.GetBlock(-1, 64, -1)).IsEqualTo((Blocks.Wool, Red));

        // Single 4-wide × 4-tall open-air door on the -Z wall (z=-4), columns {-2..1}, layers 1-4.
        await Assert.That(w.GetBlock(-2, 65, -4)).IsEqualTo((Blocks.Air, 0));
        await Assert.That(w.GetBlock(1, 68, -4)).IsEqualTo((Blocks.Air, 0));   // door cuts through the strip course
        // The opposite (+Z) wall has no door → strip stays clay there.
        await Assert.That(w.GetBlock(-1, 68, 3)).IsEqualTo((Blocks.StainedClay, Red));
    }

    [Test]
    public async Task Odd_shell_centres_a_3_wide_door_a_3x3_pad_and_a_3x3_roof_hole()
    {
        // A 9×9 piece with a cell-centre marker: shell [1,8) (span 7), interior 5 across.
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 9, 9), footprint: null, shellBound: true, 4.5, 4.5, [(0, 0, 9, 0)], [], out _)!;
        var w = new VoxelWorld();
        Shell(w, frame, 64, HouseStyle.Wool);

        // 3×3 pad centred on block 4 (cells 3..5).
        await Assert.That(w.GetBlock(3, 64, 3)).IsEqualTo((Blocks.Wool, Red));
        await Assert.That(w.GetBlock(5, 64, 5)).IsEqualTo((Blocks.Wool, Red));
        await Assert.That(w.GetBlock(2, 64, 2)).IsEqualTo((Blocks.Bedrock, 0));

        // 3-wide door on the odd wall (columns 3..5 at z=1).
        await Assert.That(w.GetBlock(3, 65, 1)).IsEqualTo((Blocks.StainedGlassPane, Red));
        await Assert.That(w.GetBlock(5, 65, 1)).IsEqualTo((Blocks.StainedGlassPane, Red));
        await Assert.That(w.GetBlock(2, 65, 1)).IsEqualTo((Blocks.Bedrock, 0));

        // 3×3 roof hole (cells 3..5 at layer 8).
        await Assert.That(w.GetBlock(4, 72, 4)).IsEqualTo((Blocks.Air, 0));
        await Assert.That(w.GetBlock(2, 72, 2)).IsEqualTo((Blocks.Bedrock, 0));
    }

    [Test]
    public async Task Minimum_shell_narrows_the_spawn_door_so_corners_stay_covered()
    {
        // The WX2 floor: an 8×8 piece → 6×6 shell, 4×4 interior → a 2-wide door.
        var frame = RoomFrames.Resolve(new BlockRect(0, 0, 8, 8), footprint: null, shellBound: true, 4, 4, [], [RoomEdge.NegZ], out _)!;
        var w = new VoxelWorld();
        Shell(w, frame, 64, HouseStyle.Spawn);

        // Door columns {3,4} at z=1 are open; the wall cells beside them are not.
        await Assert.That(w.GetBlock(3, 65, 1)).IsEqualTo((Blocks.Air, 0));
        await Assert.That(w.GetBlock(4, 65, 1)).IsEqualTo((Blocks.Air, 0));
        await Assert.That(w.GetBlock(2, 65, 1)).IsEqualTo((Blocks.Bedrock, 0));
        await Assert.That(w.GetBlock(5, 65, 1)).IsEqualTo((Blocks.Bedrock, 0));

        // 2×2 roof hole on the 6-span shell.
        await Assert.That(HouseStamper.RoofHoleSpan(6)).IsEqualTo(2);
        await Assert.That(HouseStamper.RoofHoleSpan(7)).IsEqualTo(3);
        await Assert.That(HouseStamper.RoofHoleSpan(8)).IsEqualTo(4);
        await Assert.That(HouseStamper.RoofHoleSpan(18)).IsEqualTo(4);
    }
}

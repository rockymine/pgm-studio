using PgmStudio.Domain;
using PgmStudio.Minecraft;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The DTM/DTC structure stamps. Every figure here is world-measured from the corpus, not read off the
/// XML: a hand-authored region is a loose box drawn around the structure and says nothing about its size.
/// </summary>
public sealed class ObjectiveStamperTests
{
    // A flat world whose surface sits at y=64 (surfaceTop is the topmost air cell, so solid ends at 63).
    private static Dictionary<(int X, int Z), int> FlatSurface(int y = 64)
    {
        var top = new Dictionary<(int X, int Z), int>();
        for (var x = -16; x <= 16; x++)
        for (var z = -16; z <= 16; z++)
            top[(x, z)] = y;
        return top;
    }

    private static VoxelWorld World() => new();

    // ── the box is shared by the stamp and the region (OB8) ─────────────────────────
    [Test]
    [Arguments(DestroyableStyle.Pillar1, 1, 1, 1)]
    [Arguments(DestroyableStyle.Pillar2, 1, 2, 1)]
    [Arguments(DestroyableStyle.Pillar3, 1, 3, 1)]
    [Arguments(DestroyableStyle.Cube3, 3, 3, 3)]
    [Arguments(DestroyableStyle.Cube4, 4, 4, 4)]
    public async Task The_box_matches_the_style_dimensions(DestroyableStyle style, int w, int h, int d)
    {
        var box = ObjectiveStamper.DestroyableBox(FlatSurface(), 0, 0, style);
        await Assert.That(box.Width).IsEqualTo(w);
        await Assert.That(box.Height).IsEqualTo(h);
        await Assert.That(box.Depth).IsEqualTo(d);
    }

    // A cuboid spans blocks [min, max), so its max attribute is one past the last block: a box holding
    // blocks 20..22 emits max=23, and getting this wrong inflates every measurement by a block per axis.
    [Test]
    public async Task A_cuboid_max_is_one_past_the_last_block()
    {
        var box = new BlockBox(20, 43, 146, 22, 45, 148);
        await Assert.That(box.CuboidMax).IsEqualTo((23, 46, 149));
        await Assert.That((box.Width, box.Height, box.Depth)).IsEqualTo((3, 3, 3));
    }

    // ── float (DT3) ─────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_destroyable_floats_above_the_surface()
    {
        var box = ObjectiveStamper.DestroyableBox(FlatSurface(64), 0, 0, DestroyableStyle.Pillar3);
        await Assert.That(box.MinY).IsEqualTo(64 + ObjectiveStamper.DefaultDestroyableFloat);
    }

    // The footprint drives the base, not the anchor column, so uneven terrain still gives a level structure.
    [Test]
    public async Task The_base_clears_the_highest_ground_the_footprint_spans()
    {
        var surface = FlatSurface(64);
        surface[(1, 1)] = 70;                       // a bump under one corner of a 3x3
        var box = ObjectiveStamper.DestroyableBox(surface, 0, 0, DestroyableStyle.Cube3, floatBlocks: 4);
        await Assert.That(box.MinY).IsEqualTo(74);
    }

    // ── the pillar family (DT1) ─────────────────────────────────────────────────────
    [Test]
    public async Task A_pillar_is_a_single_column_of_its_material()
    {
        var world = World();
        var box = ObjectiveStamper.DestroyableBox(FlatSurface(), 0, 0, DestroyableStyle.Pillar3);
        ObjectiveStamper.StampDestroyable(world, box, DestroyableStyle.Pillar3, Blocks.Obsidian);

        for (var y = box.MinY; y <= box.MaxY; y++)
            await Assert.That(world.GetBlock(0, y, 0).Id).IsEqualTo(Blocks.Obsidian);
        // one block wide in both directions, and nothing under it — a pillar floats alone
        await Assert.That(world.GetBlock(1, box.MinY, 0).Id).IsEqualTo(Blocks.Air);
        await Assert.That(world.GetBlock(0, box.MinY, 1).Id).IsEqualTo(Blocks.Air);
        await Assert.That(world.GetBlock(0, box.MinY - 1, 0).Id).IsEqualTo(Blocks.Air);
    }

    // ── the bedrock centre (DT2) ────────────────────────────────────────────────────
    [Test]
    public async Task A_cube_takes_a_concentric_bedrock_centre()
    {
        var world = World();
        var box = ObjectiveStamper.DestroyableBox(FlatSurface(), 0, 0, DestroyableStyle.Cube3);
        ObjectiveStamper.StampDestroyable(world, box, DestroyableStyle.Cube3, Blocks.EmeraldBlock, bedrockCentre: true);

        var (cx, cy, cz) = (box.MinX + 1, box.MinY + 1, box.MinZ + 1);
        await Assert.That(world.GetBlock(cx, cy, cz).Id).IsEqualTo(Blocks.Bedrock);

        // 27 cells, 26 of them the goal's material — the modal non-pillar block count. The bedrock is not
        // in `materials`, so it is neither counted in the goal's health nor breakable.
        var emerald = 0;
        for (var x = box.MinX; x <= box.MaxX; x++)
        for (var y = box.MinY; y <= box.MaxY; y++)
        for (var z = box.MinZ; z <= box.MaxZ; z++)
            if (world.GetBlock(x, y, z).Id == Blocks.EmeraldBlock) emerald++;
        await Assert.That(emerald).IsEqualTo(26);
    }

    [Test]
    public async Task A_cube_without_a_bedrock_centre_is_solid()
    {
        var world = World();
        var box = ObjectiveStamper.DestroyableBox(FlatSurface(), 0, 0, DestroyableStyle.Cube3);
        ObjectiveStamper.StampDestroyable(world, box, DestroyableStyle.Cube3, Blocks.GoldBlock);
        await Assert.That(world.GetBlock(box.MinX + 1, box.MinY + 1, box.MinZ + 1).Id).IsEqualTo(Blocks.GoldBlock);
    }

    // ── the ender stone column (DT4) ────────────────────────────────────────────────
    // 5 blocks a layer, corners open — the hollow cross is what separates it from a cube.
    [Test]
    public async Task A_column_plus_leaves_its_corners_open()
    {
        var world = World();
        var box = ObjectiveStamper.DestroyableBox(FlatSurface(), 0, 0, DestroyableStyle.ColumnPlus, columnHeight: 3);
        ObjectiveStamper.StampDestroyable(world, box, DestroyableStyle.ColumnPlus, Blocks.EndStone);

        var perLayer = 0;
        for (var x = box.MinX; x <= box.MaxX; x++)
        for (var z = box.MinZ; z <= box.MaxZ; z++)
            if (world.GetBlock(x, box.MinY, z).Id == Blocks.EndStone) perLayer++;
        await Assert.That(perLayer).IsEqualTo(5);

        await Assert.That(world.GetBlock(box.MinX, box.MinY, box.MinZ).Id).IsEqualTo(Blocks.Air);   // a corner
        await Assert.That(world.GetBlock(box.MaxX, box.MinY, box.MaxZ).Id).IsEqualTo(Blocks.Air);
        await Assert.That(world.GetBlock(box.MinX + 1, box.MinY, box.MinZ + 1).Id).IsEqualTo(Blocks.EndStone);
    }

    [Test]
    public async Task A_column_plus_repeats_its_layer_over_the_given_height()
    {
        var world = World();
        var box = ObjectiveStamper.DestroyableBox(FlatSurface(), 0, 0, DestroyableStyle.ColumnPlus, columnHeight: 3);
        ObjectiveStamper.StampDestroyable(world, box, DestroyableStyle.ColumnPlus, Blocks.EndStone);

        var total = 0;
        for (var x = box.MinX; x <= box.MaxX; x++)
        for (var y = box.MinY; y <= box.MaxY; y++)
        for (var z = box.MinZ; z <= box.MaxZ; z++)
            if (world.GetBlock(x, y, z).Id == Blocks.EndStone) total++;
        await Assert.That(total).IsEqualTo(15);   // dynamite's 3x3 column: 5 a layer, 3 tall
    }

    // ── the core (DC1) ──────────────────────────────────────────────────────────────
    [Test]
    public async Task A_core_is_a_shell_of_its_material_around_lava()
    {
        var world = World();
        var box = ObjectiveStamper.CoreBox(FlatSurface(), 0, 0);
        ObjectiveStamper.StampCore(world, box, Blocks.Obsidian);

        await Assert.That((box.Width, box.Height, box.Depth)).IsEqualTo((5, 5, 5));

        var lava = 0; var casing = 0;
        for (var x = box.MinX; x <= box.MaxX; x++)
        for (var y = box.MinY; y <= box.MaxY; y++)
        for (var z = box.MinZ; z <= box.MaxZ; z++)
        {
            var b = world.GetBlock(x, y, z).Id;
            if (b == Blocks.StationaryLava) lava++;
            else if (b == Blocks.Obsidian) casing++;
        }
        await Assert.That(lava).IsEqualTo(27);            // a 3x3x3 interior, the modal lava volume
        await Assert.That(casing).IsEqualTo(125 - 27);

        // the floor and the cap are both solid — the lava is fully enclosed
        await Assert.That(world.GetBlock(box.MinX + 2, box.MinY, box.MinZ + 2).Id).IsEqualTo(Blocks.Obsidian);
        await Assert.That(world.GetBlock(box.MinX + 2, box.MaxY, box.MinZ + 2).Id).IsEqualTo(Blocks.Obsidian);
    }

    [Test]
    public async Task An_open_top_core_exposes_its_lava_at_the_rim()
    {
        var world = World();
        var box = ObjectiveStamper.CoreBox(FlatSurface(), 0, 0);
        ObjectiveStamper.StampCore(world, box, Blocks.Obsidian, openTop: true);

        await Assert.That(world.GetBlock(box.MinX + 2, box.MaxY, box.MinZ + 2).Id).IsEqualTo(Blocks.StationaryLava);
        await Assert.That(world.GetBlock(box.MinX, box.MaxY, box.MinZ).Id).IsEqualTo(Blocks.Obsidian);   // rim intact
        await Assert.That(world.GetBlock(box.MinX + 2, box.MinY, box.MinZ + 2).Id).IsEqualTo(Blocks.Obsidian); // floor intact
    }

    [Test]
    public async Task A_core_floats_so_its_lava_can_fall()
    {
        var box = ObjectiveStamper.CoreBox(FlatSurface(64), 0, 0);
        await Assert.That(box.MinY).IsEqualTo(64 + ObjectiveStamper.DefaultCoreFloat);
    }

    // ── leak and float are one knob (DC2) ───────────────────────────────────────────
    [Test]
    [Arguments(5, 6, 0)]    // the corpus centre: the lava lands below the leak level on its own
    [Arguments(5, 2, 3)]    // a shallow float makes digging part of the capture
    [Arguments(3, 3, 0)]    // leak == float leaks the moment the casing is breached
    [Arguments(2, 7, 0)]    // never negative
    public async Task Dig_depth_is_leak_minus_float_never_negative(int leak, int floatBlocks, int expected)
        => await Assert.That(ObjectiveStamper.DigDepth(leak, floatBlocks)).IsEqualTo(expected);
}

using PgmStudio.Domain;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The room style — a course stack per part (docs/world-export/structures.md §6.4). Two things have to hold:
/// the shipped styles rebuild the shell that shipped, block for block, so no existing map moves; and an
/// extent moves freely, because that is what a stack indexed from the part's own base buys over the fixed
/// layer indices it replaces.
/// </summary>
public sealed class RoomStyleTests
{
    private const int Red = 14;

    /// <summary>The room this style describes: the shell as the flat-roofed house it is, and the pad the
    /// structure stampers lay over it. The shell no longer lays its own pad — a spawn point and a wool are
    /// their own stampers now — so what used to be one call is two, and these tests are about both.</summary>
    private static void Shell(VoxelWorld world, RoomFrame frame, int floorY, HouseStyle style)
    {
        HouseStamper.Stamp(world, frame, floorY, style, Red);
        PadStamp.Lay(world, frame.Pad, floorY, Red);
    }

    private static RoomFrame Baseline(RoomEdge? spawnDoor = null) =>
        RoomFrames.Resolve(-5, -5, 5, 5, 0, 0,
            spawnDoor is null ? [(-5, -5, 5, -5)] : [], spawnDoor, out _)!;

    // ── the shipped shell, unmoved ─────────────────────────────────────────────────────────────────
    [Test]
    public async Task The_shipped_styles_rebuild_the_shell_that_shipped()
    {
        // A golden over the whole volume rather than a handful of probes: the point of stage one is that a
        // course stack is a re-description of the old stamper, so every cell it writes must match what the
        // hard-coded layers wrote — the pad, the band, the slit, the hole and the doors included.
        foreach (var (style, door, doorHeight) in ((HouseStyle Style, int Door, int Height)[])
                 [(HouseStyle.Wool, Blocks.StainedGlassPane, 3), (HouseStyle.Spawn, Blocks.Air, 4)])
        {
            var spawnDoor = style == HouseStyle.Spawn ? RoomEdge.NegZ : (RoomEdge?)null;
            var frame = Baseline(spawnDoor);
            var world = new VoxelWorld();
            Shell(world, frame, 64, style);

            var band = style == HouseStyle.Wool ? Blocks.Wool : Blocks.StainedClay;
            for (var x = frame.MinX; x < frame.MaxX; x++)
            for (var z = frame.MinZ; z < frame.MaxZ; z++)
            for (var layer = 0; layer <= 8; layer++)
                await Assert.That(world.GetBlock(x, 64 + layer, z))
                    .IsEqualTo(Shipped(frame, x, z, layer, band, door, doorHeight));
        }
    }

    /// <summary>What the pre-style stamper wrote at a cell: floor bedrock + wool pad, walls bedrock with a
    /// coloured band at layer 4 and an open course at 6, roof bedrock with a centred hole, doors cut over the
    /// wall. Written out here so the golden compares against the old rules rather than against the new code
    /// restating itself.</summary>
    private static (int Id, int Data) Shipped(
        RoomFrame frame, int x, int z, int layer, int band, int door, int doorHeight)
    {
        var onPad = x >= frame.Pad.MinX && x < frame.Pad.MinX + frame.Pad.Size
                 && z >= frame.Pad.MinZ && z < frame.Pad.MinZ + frame.Pad.Size;
        var onRing = x == frame.MinX || x == frame.MaxX - 1 || z == frame.MinZ || z == frame.MaxZ - 1;

        if (layer >= 1 && layer <= doorHeight && InDoor(frame, x, z)) return (door, door == Blocks.Air ? 0 : Red);
        if (layer == 0) return onPad ? (Blocks.Wool, Red) : (Blocks.Bedrock, 0);
        if (layer == 8) return InHole(frame, x, z) ? (Blocks.Air, 0) : (Blocks.Bedrock, 0);
        if (!onRing) return (Blocks.Air, 0);
        if (layer == 6) return (Blocks.Air, 0);                       // the light slit
        return layer == 4 ? (band, Red) : (Blocks.Bedrock, 0);
    }

    private static bool InDoor(RoomFrame frame, int x, int z)
    {
        foreach (var door in frame.Doors)
        {
            var (dx, dz) = door.Edge switch
            {
                RoomEdge.NegZ => (-1, frame.MinZ),
                RoomEdge.PosZ => (-1, frame.MaxZ - 1),
                RoomEdge.NegX => (frame.MinX, -1),
                _ => (frame.MaxX - 1, -1),
            };
            var alongX = door.Edge is RoomEdge.NegZ or RoomEdge.PosZ;
            var along = alongX ? x : z;
            var fixedOk = alongX ? z == dz : x == dx;
            if (fixedOk && along >= door.Lo && along < door.Lo + door.Width) return true;
        }
        return false;
    }

    private static bool InHole(RoomFrame frame, int x, int z)
    {
        var (holeW, holeD) = (HouseStamper.RoofHoleSpan(frame.Width), HouseStamper.RoofHoleSpan(frame.Depth));
        var minX = frame.MinX + (frame.Width - holeW) / 2;
        var minZ = frame.MinZ + (frame.Depth - holeD) / 2;
        return x >= minX && x < minX + holeW && z >= minZ && z < minZ + holeD;
    }

    // ── the stack itself ───────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_stack_is_read_from_the_parts_own_base_and_its_last_course_repeats()
    {
        var part = new RoomPart(
            [new RoomCourse(new SolidMaterial(Blocks.Stone), 2), new RoomCourse(new SolidMaterial(Blocks.Dirt))], 6);

        await Assert.That(part.At(0).Material).IsEqualTo(new SolidMaterial(Blocks.Stone));
        await Assert.That(part.At(1)).IsEqualTo(((TerrainMaterial)new SolidMaterial(Blocks.Stone), 1));
        await Assert.That(part.At(2).Material).IsEqualTo(new SolidMaterial(Blocks.Dirt));
        // Past the stack: the last course carries on, so a taller part needs no re-authoring.
        await Assert.That(part.At(5).Material).IsEqualTo(new SolidMaterial(Blocks.Dirt));
    }

    [Test]
    public async Task A_taller_wall_grows_in_its_top_course_and_leaves_the_band_where_it_was()
    {
        // Height is the knob the fixed layer indices could not have: a band written at the fourth course is a
        // band at eye level, and it has to stay there when the room gets taller rather than slide with it.
        var frame = Baseline();
        var world = new VoxelWorld();
        var tall = HouseStyle.Wool with { Wall = HouseStyle.Wool.Wall with { Extent = 11 } };
        Shell(world, frame, 64, tall);

        await Assert.That(world.GetBlock(-4, 68, -1)).IsEqualTo((Blocks.Wool, Red));    // band still at layer 4
        await Assert.That(world.GetBlock(-4, 70, -3)).IsEqualTo((Blocks.Air, 0));       // slit still at layer 6
        await Assert.That(world.GetBlock(-4, 74, -3)).IsEqualTo((Blocks.Bedrock, 0));   // grown in bedrock
        await Assert.That(world.GetBlock(-4, 76, -3)).IsEqualTo((Blocks.Bedrock, 0));   // roof rode up with it
        await Assert.That(tall.TopLayer).IsEqualTo(12);
    }

    [Test]
    public async Task A_floor_grows_downward_so_the_course_players_stand_on_never_moves()
    {
        // The pad is the exported point (WX5). A thicker floor that lifted it would move the spawn.
        var world = new VoxelWorld();
        Shell(world, Baseline(), 64, HouseStyle.Wool with { Foundation = new Foundation { Plate = RoomPart.Of(new SolidMaterial(Blocks.Stone), 3) } });

        await Assert.That(world.GetBlock(-1, 64, -1)).IsEqualTo((Blocks.Wool, Red));     // pad, unmoved
        await Assert.That(world.GetBlock(-4, 64, -4)).IsEqualTo((Blocks.Stone, 0));
        await Assert.That(world.GetBlock(-4, 62, -4)).IsEqualTo((Blocks.Stone, 0));
        await Assert.That(world.GetBlock(-4, 61, -4)).IsEqualTo((Blocks.Air, 0));
    }

    // ── the roof ───────────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task An_overlapping_roof_reaches_the_piece_edge_and_no_further()
    {
        // The shell is its piece inset by one block (WX1), so an overhang of one lands exactly on the piece
        // boundary — which is why it needs no negotiation with anything outside the room. What used to be a
        // two-valued eave is that number: flush is none, overlap is one.
        var frame = Baseline();
        var world = new VoxelWorld();
        Shell(world, frame, 64, HouseStyle.Wool with { Overhang = 1 });

        await Assert.That(world.GetBlock(frame.MinX - 1, 72, frame.MinZ - 1)).IsEqualTo((Blocks.Bedrock, 0));
        await Assert.That(world.GetBlock(frame.MaxX, 72, frame.MaxZ)).IsEqualTo((Blocks.Bedrock, 0));
        await Assert.That(world.GetBlock(frame.MinX - 2, 72, frame.MinZ - 2)).IsEqualTo((Blocks.Air, 0));
        // The walls below it are untouched — an eave is roof, not a wider room.
        await Assert.That(world.GetBlock(frame.MinX - 1, 68, frame.MinZ - 1)).IsEqualTo((Blocks.Air, 0));
    }

    [Test]
    public async Task A_roof_is_one_course_however_thick_its_part_is_written()
    {
        // A lid is one course, the way a slope's tread is: a roof is a material now, not a stack, so there is
        // no thickness left to ask for and nothing stands where a second course would have been.
        var world = new VoxelWorld();
        Shell(world, Baseline(), 64, HouseStyle.Wool with { Roof = new SolidMaterial(Blocks.Bedrock) });

        await Assert.That(world.GetBlock(-4, 72, -4)).IsEqualTo((Blocks.Bedrock, 0));
        await Assert.That(world.GetBlock(-4, 73, -4)).IsEqualTo((Blocks.Air, 0));
        await Assert.That(world.GetBlock(-1, 72, -1)).IsEqualTo((Blocks.Air, 0));   // the hole is still open
    }

    [Test]
    public async Task A_style_may_close_the_roof()
    {
        var world = new VoxelWorld();
        Shell(world, Baseline(), 64, HouseStyle.Wool with { RoofHole = false });

        await Assert.That(world.GetBlock(-1, 72, -1)).IsEqualTo((Blocks.Bedrock, 0));
    }

    // ── what a style may not decide ────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_floor_style_cannot_repaint_the_pad_and_a_wall_style_cannot_close_the_doorway()
    {
        // Both are contract, not finish: the pad is the exported point (WX5) and the doorway is the entry
        // (WX6). A style that could take either would make the world disagree with the XML, or seal the room.
        var world = new VoxelWorld();
        Shell(world, Baseline(), 64, new HouseStyle
        {
            Foundation = new Foundation { Plate = RoomPart.Of(new SolidMaterial(Blocks.Obsidian)) },
            Wall = RoomPart.Of(new SolidMaterial(Blocks.Obsidian), 7),
            Door = DoorMaterial.Air,
        });

        await Assert.That(world.GetBlock(-1, 64, -1)).IsEqualTo((Blocks.Wool, Red));   // pad survives the floor
        await Assert.That(world.GetBlock(-4, 64, -4)).IsEqualTo((Blocks.Obsidian, 0));
        await Assert.That(world.GetBlock(-2, 65, -4)).IsEqualTo((Blocks.Air, 0));      // doorway cut through
    }

    [Test]
    public async Task An_air_course_leaves_a_gap_rather_than_erasing_what_is_there()
    {
        // The slit is a course the stack declines to fill, not a course of air blocks. The difference shows
        // wherever a shell meets something already stamped, and it is why a style can never delete a stamp.
        var world = new VoxelWorld();
        world.SetBlock(-4, 70, -3, Blocks.GoldBlock);
        Shell(world, Baseline(), 64, HouseStyle.Wool);

        await Assert.That(world.GetBlock(-4, 70, -3)).IsEqualTo((Blocks.GoldBlock, 0));
    }

    // ── the ring is a ring ─────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_wall_run_stripes_a_room_the_way_it_stripes_a_plateau()
    {
        // A room wall is a closed ring, so the perimeter arc a wall-run pattern reads is defined all the way
        // round it — which is what lets one material serve terrain edges and room walls alike.
        var world = new VoxelWorld();
        Shell(world, Baseline(), 64, HouseStyle.Wool with
        {
            Wall = RoomPart.Of(new WallRunMaterial(
                [new WallStripe(new SolidMaterial(Blocks.Stone), 1), new WallStripe(new SolidMaterial(Blocks.Dirt), 1)]), 7),
        });

        // Adjacent cells along the −z wall alternate, and the alternation carries round the corner.
        await Assert.That(world.GetBlock(-4, 66, -4).Id).IsNotEqualTo(world.GetBlock(-3, 66, -4).Id);
        await Assert.That(new[] { Blocks.Stone, Blocks.Dirt }).Contains(world.GetBlock(3, 66, 0).Id);
    }
}

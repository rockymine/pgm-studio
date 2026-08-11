using PgmStudio.Domain;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Storeys stacked inside a building (docs/world-export/structures.md §7.6). A storey is measured by the air a
/// player stands in, so the assertions are about that air and about what closes it: every storey has its three
/// blocks of clear, every slab but the top one is there, and every slab has a ladder standing through it. A
/// stack of sealed volumes is the failure this guards — a building with rooms nobody can reach.
/// </summary>
public sealed class HouseStoreyTests
{
    private const int FloorY = 64;

    private static HouseStyle Stacked(int count, int clear = 3, RoofForm form = RoofForm.Flat) => new()
    {
        Form = form,
        RoofHole = false,
        Storeys = [.. Enumerable.Range(0, count).Select(_ => new Storey { Clear = clear })],
    };

    private static VoxelWorld House(int width, int depth, HouseStyle style)
    {
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, 0, 0, width, depth, FloorY, style,
            doors: [new RoomDoor(RoomEdge.NegZ, width / 2, 1)]);
        return world;
    }

    // ── the arithmetic ─────────────────────────────────────────────────────────────────────────────
    /// <summary>A storey spends its headroom, plus one course on the slab over it when something stands there.
    /// Three storeys of three clear is 3+1 + 3+1 + 3 = 11 courses, not 9 and not 12.</summary>
    [Test]
    [Arguments(1, 3, 3)]
    [Arguments(2, 3, 7)]
    [Arguments(3, 3, 11)]
    [Arguments(2, 5, 11)]
    [Arguments(3, 4, 14)]
    public async Task A_storey_spends_its_headroom_plus_the_slab_over_it(int count, int clear, int courses)
        => await Assert.That(Stacked(count, clear).WallCourses).IsEqualTo(courses);

    /// <summary>A room has to be stood up in, so a clear under three is read as three however it was asked
    /// for. The wall height of a plain shell is <em>not</em> a clear and is taken as stated — a style saved
    /// before storeys existed is the building it always was.</summary>
    [Test]
    public async Task A_room_is_never_shorter_than_three_but_a_plain_wall_is_taken_as_stated()
    {
        await Assert.That(Stacked(1, clear: 1).WallCourses).IsEqualTo(3);
        await Assert.That(Stacked(2, clear: 2).WallCourses).IsEqualTo(7);

        var shed = new HouseStyle { Wall = RoomPart.Of(new SolidMaterial(Blocks.Planks), 2) };
        await Assert.That(shed.Storeys).IsEmpty();
        await Assert.That(shed.WallCourses).IsEqualTo(2);
    }

    /// <summary>Each storey's floor sits on the slab of the one below, so the bases are the running sum of the
    /// courses spent under them.</summary>
    [Test]
    public async Task Each_storey_stands_on_the_slab_below_it()
        => await Assert.That(Stacked(3, clear: 4).LevelBases).IsEquivalentTo([0, 5, 10]);

    // ── the stamped building ───────────────────────────────────────────────────────────────────────
    /// <summary>The slab is where the arithmetic says it is: solid across the interior at the top of each
    /// storey but the last, and air through the whole of the clear under it.</summary>
    [Test]
    public async Task A_slab_closes_every_storey_but_the_top_one()
    {
        var world = House(9, 9, Stacked(3));
        var (x, z) = (4, 6);                        // interior, clear of the ladder in the door wall's corner

        foreach (var slab in (int[])[FloorY + 4, FloorY + 8])
        {
            await Assert.That(world.GetBlock(x, slab, z).Id).IsNotEqualTo(Blocks.Air);
            for (var clear = 1; clear <= 3; clear++)
                await Assert.That(world.GetBlock(x, slab - clear, z).Id).IsEqualTo(Blocks.Air);
        }

        // The top storey's lid is the roof, not a slab: its three blocks of clear run to the eave.
        for (var clear = 1; clear <= 3; clear++)
            await Assert.That(world.GetBlock(x, FloorY + 8 + clear, z).Id).IsEqualTo(Blocks.Air);
    }

    /// <summary>Every storey can be stood up in — three blocks of air over its own floor, on every storey,
    /// measured through the middle of the room.</summary>
    [Test]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(5)]
    public async Task Every_storey_has_its_clear(int count)
    {
        var style = Stacked(count, clear: 4);
        var world = House(11, 11, style);
        var bases = style.LevelBases;

        for (var level = 0; level < count; level++)
            for (var clear = 1; clear <= 4; clear++)
                await Assert.That(world.GetBlock(5, FloorY + bases[level] + clear, 7).Id).IsEqualTo(Blocks.Air);
    }

    /// <summary>A ladder stands through every slab, hung on the door wall one cell in from an interior corner
    /// — the cells the chests and the wool monuments do not take — and facing away from the wall behind it.
    /// It reaches the slab so a player steps off onto the floor rather than into its underside.</summary>
    [Test]
    public async Task A_ladder_climbs_through_every_slab()
    {
        var world = House(9, 9, Stacked(3));
        var (x, z) = (2, 1);                        // one along the −z wall from its corner, one cell inside

        foreach (var slab in (int[])[FloorY + 4, FloorY + 8])
            for (var y = slab - 3; y <= slab; y++)
            {
                var block = world.GetBlock(x, y, z);
                await Assert.That(block.Id).IsEqualTo(Blocks.Ladder);
                await Assert.That(block.Data).IsEqualTo(3);         // hung on −z, facing +z
            }
    }

    /// <summary>The ladder moves to the far end of the door wall when the doorway would stand in it — a ladder
    /// in the doorway is a ladder in the way.</summary>
    [Test]
    public async Task A_wide_doorway_pushes_the_ladder_to_the_other_end()
    {
        var world = new VoxelWorld();
        HouseStamper.Stamp(world, 0, 0, 9, 9, FloorY, Stacked(2),
            doors: [new RoomDoor(RoomEdge.NegZ, 1, 4)]);

        await Assert.That(world.GetBlock(2, FloorY + 2, 1).Id).IsNotEqualTo(Blocks.Ladder);
        await Assert.That(world.GetBlock(6, FloorY + 2, 1).Id).IsEqualTo(Blocks.Ladder);
    }

    /// <summary>Windows are seated in each storey's own frame, so a sill of two is two blocks over
    /// <em>this</em> floor: the same openings at the same plan positions, one band per storey.</summary>
    [Test]
    public async Task Windows_repeat_in_every_storeys_own_frame()
    {
        var style = Stacked(3, clear: 5) with { Windows = WindowStyle.Glazed with { Sill = 2, Height = 2 } };
        var world = House(13, 11, style);
        var bases = style.LevelBases;

        // Every glass cell of the far wall, as an offset within its own storey. Offsets rather than heights is
        // the whole assertion: the same window in the same place, one storey up.
        var bands = bases
            .Select(at => (
                from x in Enumerable.Range(0, 13)
                from course in Enumerable.Range(1, 4)
                where world.GetBlock(x, FloorY + at + course, 10).Id == Blocks.GlassPane
                select (x, course)).ToList())
            .ToList();

        await Assert.That(bands[0]).IsNotEmpty();
        foreach (var band in bands.Skip(1)) await Assert.That(band).IsEquivalentTo(bands[0]);
    }

    /// <summary>A stack of storeys is still one closed shell: the slabs and the ladder holes cut through the
    /// inside of the building, never through its skin.
    ///
    /// <para>Every storey is seeded, not just the ground one. To a flood fill a ladder is a solid block, so a
    /// stack is a column of separate volumes here even though a player climbs freely between them — walking
    /// air out of the ground floor would say nothing about the two rooms above it.</para></summary>
    [Test]
    [Arguments(RoofForm.Flat)]
    [Arguments(RoofForm.Gable)]
    [Arguments(RoofForm.Hip)]
    public async Task A_stacked_house_is_still_sealed(RoofForm form)
    {
        var style = Stacked(3, form: form) with { Door = DoorMaterial.StainedGlass };
        var world = House(11, 9, style);

        var seeds = style.LevelBases.Select(at => (5, FloorY + at + 1, 4)).ToList();
        var seen = new HashSet<(int X, int Y, int Z)>(seeds);
        var queue = new Queue<(int X, int Y, int Z)>(seeds);
        while (queue.Count > 0)
        {
            var (x, y, z) = queue.Dequeue();
            if (x < -6 || x > 17 || z < -6 || z > 15 || y > FloorY + 40)
            {
                await Assert.That($"escaped at {x},{y},{z}").IsEqualTo("sealed");
                return;
            }
            foreach (var (dx, dy, dz) in new[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) })
            {
                var next = (x + dx, y + dy, z + dz);
                if (world.GetBlock(next.Item1, next.Item2, next.Item3).Id != Blocks.Air) continue;
                if (seen.Add(next)) queue.Enqueue(next);
            }
        }
    }
}

using PgmStudio.Domain;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Where windows go and what is written into them. Seating is the half that has to be right — a window is cut
/// out of a wall that already stands, so a badly placed one takes a corner post out of the building or opens
/// into the doorway beside it — and the dressing is the half that is easy to get subtly wrong, because a stair
/// turned the wrong way is a solid patch of wall rather than a light.
/// </summary>
public sealed class HouseWindowsTests
{
    private static List<WindowSeat> Seats(
        WindowStyle style, int width = 15, int depth = 11, int wallExtent = 7,
        IReadOnlyList<RoomDoor>? doors = null)
        => HouseWindows.Seats(style, 0, 0, width - 1, depth - 1, wallExtent, doors);

    [Test]
    public async Task No_window_is_cut_through_a_corner()
    {
        // A corner is what holds the building up, and it is the one cell a wall run must never offer.
        foreach (var seat in Seats(WindowStyle.Glazed))
        {
            var (lo, hi) = seat.Edge is RoomEdge.NegZ or RoomEdge.PosZ ? (0, 14) : (0, 10);
            await Assert.That(seat.Lo).IsGreaterThan(lo);
            await Assert.That(seat.Lo + seat.Width - 1).IsLessThan(hi);
        }
    }

    [Test]
    public async Task Windows_are_spread_evenly_and_centred_on_the_wall_they_are_cut_in()
    {
        var seats = Seats(WindowStyle.Glazed).Where(seat => seat.Edge == RoomEdge.NegZ).ToList();
        await Assert.That(seats.Count).IsGreaterThan(1);

        // Centred: what is left over is split between the two ends rather than piling up at one of them.
        var frontGap = seats[0].Lo - 1;
        var backGap = 13 - (seats[^1].Lo + seats[^1].Width - 1);
        await Assert.That(Math.Abs(frontGap - backGap)).IsLessThanOrEqualTo(1);

        // Evenly: one stride, not a run that starts wherever the last one ended.
        var strides = seats.Zip(seats.Skip(1), (a, b) => b.Lo - a.Lo).Distinct().ToList();
        await Assert.That(strides.Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_window_that_would_meet_a_doorway_is_dropped_rather_than_shifted()
    {
        var doors = new[] { new RoomDoor(RoomEdge.NegZ, 6, 2) };
        var withDoor = Seats(WindowStyle.Glazed, doors: doors).Where(s => s.Edge == RoomEdge.NegZ).ToList();
        var without = Seats(WindowStyle.Glazed).Where(s => s.Edge == RoomEdge.NegZ).ToList();

        // Shifting the one that clashes would break the spacing of every window after it to save one; the gap
        // where a door is reads as intended, so the survivors keep exactly the seats they always had.
        await Assert.That(withDoor.Count).IsLessThan(without.Count);
        foreach (var seat in withDoor) await Assert.That(without.Contains(seat)).IsTrue();
        // And none of the survivors touches the door or the block of wall either side of it.
        foreach (var seat in withDoor)
            await Assert.That(seat.Lo + seat.Width <= 5 || seat.Lo >= 9).IsTrue();
    }

    [Test]
    public async Task An_opening_that_will_not_fit_under_the_wall_top_is_not_cut_at_all()
    {
        // Better no windows than a band of holes running out through the eave.
        await Assert.That(Seats(WindowStyle.Band with { Sill = 6 }, wallExtent: 7)).IsEmpty();
        await Assert.That(Seats(WindowStyle.Band with { Sill = 5 }, wallExtent: 7)).IsNotEmpty();
    }

    [Test]
    public async Task Each_form_builds_at_the_size_its_geometry_needs()
    {
        // A lattice is 2×2 because the four missing quarters are the whole trick, and a band is three courses
        // because a sill and a lintel with nothing between them is not a window.
        await Assert.That((WindowStyle.Lattice with { Width = 6, Height = 4 }).Normalized()).IsEqualTo((2, 2));
        await Assert.That((WindowStyle.Band with { Width = 4, Height = 9 }).Normalized()).IsEqualTo((4, 3));
        await Assert.That((WindowStyle.Glazed with { Width = 3, Height = 4 }).Normalized()).IsEqualTo((3, 4));
    }

    [Test]
    public async Task A_stair_lattice_turns_its_four_stairs_away_from_the_light_between_them()
    {
        var world = new VoxelWorld();
        HouseWindows.Cut(world, new WindowSeat(RoomEdge.NegZ, 4, 2, 2, 2), WindowStyle.Lattice, 64, 0, 0, 14, 10);

        // The raised half of each stair sits on the outside of the group, so the quarter each is missing points
        // at the centre and the four missing quarters meet there.
        await Assert.That(world.GetBlock(4, 66, 0)).IsEqualTo((Blocks.OakStairs, Blocks.StairWest));
        await Assert.That(world.GetBlock(5, 66, 0)).IsEqualTo((Blocks.OakStairs, Blocks.StairEast));
        await Assert.That(world.GetBlock(4, 67, 0))
            .IsEqualTo((Blocks.OakStairs, Blocks.StairWest | Blocks.StairUpsideDown));
        await Assert.That(world.GetBlock(5, 67, 0))
            .IsEqualTo((Blocks.OakStairs, Blocks.StairEast | Blocks.StairUpsideDown));
    }

    [Test]
    public async Task A_stair_lattice_turns_along_the_wall_it_is_cut_in()
    {
        // The same window in a wall running the other way climbs north and south rather than east and west: the
        // step faces along the wall, not out through it.
        var world = new VoxelWorld();
        HouseWindows.Cut(world, new WindowSeat(RoomEdge.NegX, 4, 2, 2, 2), WindowStyle.Lattice, 64, 0, 0, 14, 10);

        await Assert.That(world.GetBlock(0, 66, 4)).IsEqualTo((Blocks.OakStairs, Blocks.StairNorth));
        await Assert.That(world.GetBlock(0, 66, 5)).IsEqualTo((Blocks.OakStairs, Blocks.StairSouth));
    }

    [Test]
    public async Task A_slab_band_is_a_sill_a_lintel_and_open_air_between_them()
    {
        var world = new VoxelWorld();
        HouseWindows.Cut(world, new WindowSeat(RoomEdge.NegZ, 4, 2, 2, 3), WindowStyle.Band, 64, 0, 0, 14, 10);

        for (var x = 4; x <= 5; x++)
        {
            await Assert.That(world.GetBlock(x, 66, 0)).IsEqualTo((Blocks.WoodenSlab, 0));
            await Assert.That(world.GetBlock(x, 67, 0).Id).IsEqualTo(Blocks.Air);
            await Assert.That(world.GetBlock(x, 68, 0)).IsEqualTo((Blocks.WoodenSlab, Blocks.SlabUpperHalf));
        }
    }

    [Test]
    public async Task A_pane_window_is_glazed_the_whole_way_across()
    {
        var world = new VoxelWorld();
        HouseWindows.Cut(world, new WindowSeat(RoomEdge.PosZ, 4, 2, 2, 2), WindowStyle.Glazed, 64, 0, 0, 14, 10);

        for (var x = 4; x <= 5; x++)
            for (var y = 66; y <= 67; y++)
                await Assert.That(world.GetBlock(x, y, 10).Id).IsEqualTo(Blocks.GlassPane);
    }

    [Test]
    public async Task A_style_that_asks_for_no_windows_seats_none()
    {
        await Assert.That(Seats(new WindowStyle())).IsEmpty();
    }
}

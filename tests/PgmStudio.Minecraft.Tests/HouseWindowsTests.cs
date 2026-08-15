using PgmStudio.Domain;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Where windows go and what is written into them. Seating is the half that has to be right — a window is cut
/// out of a wall that already stands, so a badly placed one takes a corner post out of the building or opens
/// into the doorway beside it — and the dressing is the half that is easy to get subtly wrong, because a stair
/// turned the wrong way is a solid patch of wall rather than a light.
/// </summary>
public sealed class HouseWindowsTests
{
    private const int FloorY = 64;

    /// <summary>The runs of wall a plain rectangle stands in — four, one per side.</summary>
    private static IReadOnlyList<WallSegment> Walls(int width = 15, int depth = 11)
        => new BuildingPlan(0, 0, width - 1, depth - 1).Segments;

    /// <summary>One wall of a rectangle, named the way a rectangle lets it be named.</summary>
    private static WallSegment Wall(RoomEdge facing, int width = 15, int depth = 11)
        => Walls(width, depth).First(wall => wall.Facing == facing);

    private static List<WindowSeat> Seats(
        WindowStyle style, int width = 15, int depth = 11, int wallExtent = 7,
        IReadOnlyList<WallOpening>? doors = null)
        => HouseWindows.Seats(style, Walls(width, depth), wallExtent, doors);

    [Test]
    public async Task No_window_is_cut_through_a_corner()
    {
        // A corner is what holds the building up, and it is the one cell a wall run must never offer.
        foreach (var seat in Seats(WindowStyle.Glazed))
        {
            var (lo, hi) = seat.Wall.Facing is RoomEdge.NegZ or RoomEdge.PosZ ? (0, 14) : (0, 10);
            await Assert.That(seat.Lo).IsGreaterThan(lo);
            await Assert.That(seat.Lo + seat.Width - 1).IsLessThan(hi);
        }
    }

    [Test]
    public async Task Windows_are_spread_evenly_and_centred_on_the_wall_they_are_cut_in()
    {
        var seats = Seats(WindowStyle.Glazed).Where(seat => seat.Wall.Facing == RoomEdge.NegZ).ToList();
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
        var doors = new[] { new WallOpening(Wall(RoomEdge.NegZ), 6, 2) };
        var front = (WindowSeat seat) => seat.Wall.Facing == RoomEdge.NegZ;
        var withDoor = Seats(WindowStyle.Glazed, doors: doors).Where(front).ToList();
        var without = Seats(WindowStyle.Glazed).Where(front).ToList();

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
        HouseWindows.Cut(world, new WindowSeat(Wall(RoomEdge.NegZ, 15, 11), 4, 2, 2, 2), WindowStyle.Lattice, 64);

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
        HouseWindows.Cut(world, new WindowSeat(Wall(RoomEdge.NegX, 15, 11), 4, 2, 2, 2), WindowStyle.Lattice, 64);

        await Assert.That(world.GetBlock(0, 66, 4)).IsEqualTo((Blocks.OakStairs, Blocks.StairNorth));
        await Assert.That(world.GetBlock(0, 66, 5)).IsEqualTo((Blocks.OakStairs, Blocks.StairSouth));
    }

    [Test]
    public async Task A_slab_band_is_a_sill_a_lintel_and_open_air_between_them()
    {
        var world = new VoxelWorld();
        HouseWindows.Cut(world, new WindowSeat(Wall(RoomEdge.NegZ, 15, 11), 4, 2, 2, 3), WindowStyle.Band, 64);

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
        HouseWindows.Cut(world, new WindowSeat(Wall(RoomEdge.PosZ, 15, 11), 4, 2, 2, 2), WindowStyle.Glazed, 64);

        for (var x = 4; x <= 5; x++)
            for (var y = 66; y <= 67; y++)
                await Assert.That(world.GetBlock(x, y, 10).Id).IsEqualTo(Blocks.GlassPane);
    }

    [Test]
    public async Task A_style_that_asks_for_no_windows_seats_none()
    {
        await Assert.That(Seats(new WindowStyle())).IsEmpty();
    }

    // ── a window that belongs to a band ─────────────────────────────────────────────────────────────

    /// <summary>A wall that bands in fours: two cells of host, two of something else, repeating.</summary>
    private static bool Banded(WallSegment wall, int along) => ((along % 4) + 4) % 4 is 0 or 1;

    [Test]
    public async Task A_window_with_a_host_sits_wholly_inside_one_panel_of_it()
    {
        // The failure this exists for: seats spread by spacing alone land half in one band and half in the
        // next, and an opening cut across that seam reads as damage rather than as a window.
        var style = new WindowStyle
        {
            Form = WindowForm.Pane, Width = 2, Height = 2, Sill = 2, Spacing = 1,
            HostBlock = 5, HostData = 1,
        };
        var seats = HouseWindows.Seats(style, Walls(21, 21), 7, null, Banded);

        await Assert.That(seats).IsNotEmpty();
        foreach (var seat in seats)
            for (var step = 0; step < seat.Width; step++)
                await Assert.That(Banded(seat.Wall, seat.Lo + step)).IsTrue();
    }

    [Test]
    public async Task The_first_panel_of_a_wall_is_not_skipped()
    {
        // A sentinel "nothing placed yet" coordinate far enough below the run to clear any spacing is also far
        // enough to overflow the subtraction that tests it, which silently refuses the first window on every
        // wall — the whole building came out blank.
        var style = new WindowStyle
        {
            Form = WindowForm.Pane, Width = 2, Height = 2, Sill = 2, Spacing = 1,
            HostBlock = 5, HostData = 1,
        };
        var seats = HouseWindows.Seats(style, Walls(9, 9), 7, null, Banded);
        await Assert.That(seats.Count(seat => seat.Wall.Facing == RoomEdge.NegZ)).IsGreaterThan(0);
    }

    [Test]
    public async Task A_host_a_wall_never_resolves_to_seats_nothing_rather_than_seating_anywhere()
    {
        // Naming a band the wall does not have is a style that asked for something it cannot have; it gets no
        // windows, not windows in the wrong place.
        var style = new WindowStyle
        {
            Form = WindowForm.Pane, Width = 2, Height = 2, Sill = 2, HostBlock = 5, HostData = 1,
        };
        await Assert.That(HouseWindows.Seats(style, Walls(21, 21), 7, null, (_, _) => false)).IsEmpty();
    }

    [Test]
    public async Task A_style_naming_no_host_is_seated_by_spacing_as_before()
    {
        // The host is an addition, not a change: a style that names none is spread exactly as it always was,
        // and the predicate is not even consulted.
        var style = new WindowStyle { Form = WindowForm.Pane, Width = 2, Height = 2, Sill = 2, Spacing = 3 };
        await Assert.That(HouseWindows.Seats(style, Walls(15, 11), 7, null, (_, _) => false))
            .IsEquivalentTo(Seats(style));
    }

    // ── the beam over a doorway ─────────────────────────────────────────────────────────────────────

    private const int BirchStairs = 135, BirchSlab = 2;

    private static DoorHeadStyle Arch => new()
    {
        Form = DoorHeadForm.Arched, Block = BirchStairs,
        Fill = DoorHeadFill.UpperSlab, FillBlock = Blocks.WoodenSlab, FillData = BirchSlab,
    };

    [Test]
    public async Task An_arched_head_turns_its_two_stairs_toward_the_opening_it_rounds()
    {
        // The upper half of a stair lattice doing the same trick for a different hole: each corner keeps its
        // raised half outward, so the quarter it is missing faces in and the two of them round the top off.
        await Assert.That(Arch.Piece(alongX: true, step: 0, width: 2))
            .IsEqualTo((BirchStairs, Blocks.StairWest | Blocks.StairUpsideDown));
        await Assert.That(Arch.Piece(alongX: true, step: 1, width: 2))
            .IsEqualTo((BirchStairs, Blocks.StairEast | Blocks.StairUpsideDown));

        // On a wall running the other way the pair turns with it, or both corners face along the wall's face.
        await Assert.That(Arch.Piece(alongX: false, step: 0, width: 2))
            .IsEqualTo((BirchStairs, Blocks.StairNorth | Blocks.StairUpsideDown));
    }

    [Test]
    public async Task A_wider_opening_is_spanned_between_its_corners()
    {
        // Five wide: a stair at each end and the middle carried across, since two corners do not reach.
        for (var step = 1; step <= 3; step++)
            await Assert.That(Arch.Piece(alongX: true, step, width: 5))
                .IsEqualTo((Blocks.WoodenSlab, BirchSlab | Blocks.SlabUpperHalf));

        await Assert.That(Arch.Piece(alongX: true, step: 4, width: 5))
            .IsEqualTo((BirchStairs, Blocks.StairEast | Blocks.StairUpsideDown));

        // A solid fill is the same beam with weight — the whole cube rather than its upper half.
        var beam = Arch with { Fill = DoorHeadFill.Solid, FillBlock = Blocks.Planks, FillData = 2 };
        await Assert.That(beam.Piece(alongX: true, step: 2, width: 5)).IsEqualTo((Blocks.Planks, 2));
    }

    [Test]
    [Arguments(2, 3, true)]
    [Arguments(5, 4, true)]
    [Arguments(1, 3, false)]     // one cell cannot hold two corners
    [Arguments(2, 2, false)]     // the head would take the last of the clear
    public async Task A_head_is_only_laid_where_the_opening_can_spare_the_course(int width, int height, bool fits)
    {
        await Assert.That(Arch.Fits(width, height)).IsEqualTo(fits);
        await Assert.That(new DoorHeadStyle().Fits(width, height)).IsFalse();   // naming no form lays none
    }

    [Test]
    public async Task No_window_sits_against_a_corner_post()
    {
        // Clearing the corner cell is not enough: an opening starting in the very next cell still meets the
        // post, and a window against the post reads as a hole knocked through the frame. The post wants a
        // block of wall beside it before anything is taken out.
        foreach (var (width, depth) in new[] { (9, 7), (15, 11), (11, 11) })
        {
            var seats = Seats(WindowStyle.Glazed, width, depth);
            await Assert.That(seats).IsNotEmpty();
            foreach (var seat in seats)
            {
                var span = seat.Wall.Facing is RoomEdge.NegZ or RoomEdge.PosZ ? width : depth;
                await Assert.That(seat.Lo).IsGreaterThanOrEqualTo(2);
                await Assert.That(seat.Lo + seat.Width - 1).IsLessThanOrEqualTo(span - 3);
            }
        }
    }

    [Test]
    public async Task A_wall_with_no_room_for_the_margin_takes_no_window()
    {
        // Five across leaves one cell once both margins are kept, so a two-wide window does not fit — and the
        // wall is left whole rather than the margin being quietly given up to seat one.
        await Assert.That(Seats(WindowStyle.Glazed, width: 5, depth: 5)).IsEmpty();
    }

    // ── the host block ─────────────────────────────────────────────────────────────────────────────
    /// <summary>A host names a <b>block</b>, not a band. A wall that is one material at the sill course
    /// resolves to a single panel the length of the whole run, and that has to take a row — one window centred
    /// in it is one window on a twenty-one-block hall, which is what a long building is least able to afford.</summary>
    [Test]
    [Arguments(13, 2)]
    [Arguments(17, 3)]
    [Arguments(21, 4)]
    [Arguments(25, 5)]
    public async Task A_wall_that_is_all_host_takes_a_row_rather_than_one_window(int width, int expected)
    {
        var style = new WindowStyle
        {
            Form = WindowForm.Pane, Block = Blocks.GlassPane, HostBlock = Blocks.Planks, HostData = 1,
            Width = 2, Height = 2, Sill = 2, Spacing = 2,
        };
        var seats = HouseWindows.Seats(style, Walls(width, 9), 6, null, (_, _) => true)
            .Where(seat => seat.Wall.Facing == RoomEdge.NegZ)
            .OrderBy(seat => seat.Lo)
            .ToList();

        await Assert.That(seats.Count).IsEqualTo(expected);

        // Evenly: one stride, and symmetric about the middle of the run.
        if (seats.Count > 1)
        {
            var strides = seats.Zip(seats.Skip(1), (a, b) => b.Lo - a.Lo).Distinct().ToList();
            await Assert.That(strides.Count).IsEqualTo(1);
            var head = seats[0].Lo - 2;
            var tail = width - 3 - (seats[^1].Lo + seats[^1].Width - 1);
            await Assert.That(Math.Abs(head - tail)).IsLessThanOrEqualTo(1);
        }
    }

    /// <summary>And a banded wall keeps what it had: a two-cell band holds one two-wide window and no more, so
    /// spreading each panel changes nothing where the panels are already window-sized. That is the case the
    /// host rule was written for, and the row above must not have cost it.</summary>
    [Test]
    public async Task A_banded_wall_still_takes_one_window_to_the_band()
    {
        var style = new WindowStyle
        {
            Form = WindowForm.Pane, Block = Blocks.GlassPane, HostBlock = Blocks.Planks, HostData = 1,
            Width = 2, Height = 2, Sill = 2, Spacing = 1,
        };
        // Two cells of something else, then two of the host, round and round.
        var seats = HouseWindows.Seats(style, Walls(21, 9), 6, null, (_, along) => along % 4 >= 2)
            .Where(seat => seat.Wall.Facing == RoomEdge.NegZ)
            .ToList();

        await Assert.That(seats).IsNotEmpty();
        foreach (var seat in seats)
        {
            // Every window sits wholly inside one band of the host.
            await Assert.That(seat.Lo % 4).IsEqualTo(2);
            await Assert.That(seat.Width).IsEqualTo(2);
        }
    }

    // ── the arched opening ─────────────────────────────────────────────────────────────────────────
    /// <summary>An arch is its two top corners and nothing else: an upside-down stair at each end of the
    /// opening's <em>top</em> course, every other cell of it left as light. The upside-down flag is what makes
    /// it an arch rather than a pair of steps — the full half is the top of the cube and the quarter each is
    /// missing faces into the opening.</summary>
    [Test]
    [Arguments(2, 2)]
    [Arguments(2, 3)]
    [Arguments(3, 3)]
    public async Task An_arched_window_carries_stairs_only_in_the_top_corners(int width, int height)
    {
        var style = new WindowStyle
        {
            Form = WindowForm.Arched, Block = Blocks.OakStairs,
            Width = width, Height = height, Sill = 2, Spacing = 3,
        };
        var (builtWidth, builtHeight) = style.Normalized();
        var world = new VoxelWorld();
        var seat = new WindowSeat(Wall(RoomEdge.NegZ, 13, 9), 3, builtWidth, 2, builtHeight);
        HouseWindows.Cut(world, seat, style, FloorY);

        for (var step = 0; step < builtWidth; step++)
            for (var course = 0; course < builtHeight; course++)
            {
                var (id, data) = world.GetBlock(seat.Lo + step, FloorY + 2 + course, 0);
                var corner = course == builtHeight - 1 && (step == 0 || step == builtWidth - 1);
                if (corner)
                {
                    await Assert.That(id).IsEqualTo(Blocks.OakStairs);
                    await Assert.That(data & Blocks.StairUpsideDown).IsEqualTo(Blocks.StairUpsideDown);
                }
                else
                {
                    await Assert.That(id).IsEqualTo(Blocks.Air);
                }
            }
    }

    /// <summary>Two corners are the whole of an arch and one cell cannot hold both, and a head that took the
    /// only course would be an arch over nothing — so the form states its own least size whatever it is
    /// asked for.</summary>
    [Test]
    [Arguments(1, 1, 2, 2)]
    [Arguments(1, 4, 2, 4)]
    [Arguments(5, 1, 5, 2)]
    public async Task An_arch_is_never_narrower_than_two_or_shorter_than_two(
        int askedWidth, int askedHeight, int builtWidth, int builtHeight)
    {
        var built = new WindowStyle
        {
            Form = WindowForm.Arched, Width = askedWidth, Height = askedHeight,
        }.Normalized();

        await Assert.That(built.Width).IsEqualTo(builtWidth);
        await Assert.That(built.Height).IsEqualTo(builtHeight);
    }
}

using PgmStudio.Domain;

namespace PgmStudio.Minecraft;

/// <summary>How a window's opening is dressed.</summary>
public enum WindowForm
{
    /// <summary>No windows. A shell that wants light through its roof rather than its walls, and what every
    /// style is until it asks for otherwise.</summary>
    None,

    /// <summary>Four stairs turned back-to-back in a 2×2 hole, each with its raised half toward the outside of
    /// the group, so the quarter each of them is missing meets in the middle and the window is <b>open</b> — a
    /// square light with a diamond of clear air through it, and no glass anywhere in it.</summary>
    StairLattice,

    /// <summary>A band two or more blocks wide and three tall: a course of slabs as the sill, a course of
    /// upside-down slabs as the lintel, and the course between them open. The two half-blocks leave half a
    /// cube of clear air above the sill and below the lintel, so the opening reads taller than the one course
    /// actually cut.</summary>
    SlabBanded,

    /// <summary>Panes filling the opening — the ordinary window, glazed rather than open.</summary>
    Pane,

    /// <summary>The hole and nothing in it. Distinct from <see cref="None"/>, which cuts no window at all: this
    /// one is cut and left, which is what a warm-weather house does and what a wall wants where the light
    /// matters more than the weather. Its size is entirely the author's, since no form is imposing one.</summary>
    Open,

    /// <summary>An opening whose <b>top course is rounded off</b>: an upside-down stair in each of its two top
    /// corners, raised half outward so the quarter each is missing faces into the opening, and the courses under
    /// them left clear. It is <see cref="DoorHeadForm.Arched"/> doing its trick for a window instead of a
    /// doorway — the same two stairs taking the squareness out of the same square hole.
    ///
    /// <para>Two wide at the least, because the whole of it is two corners and one cell cannot hold both, and
    /// two tall at the least, because a head that took the only course would leave an arch over nothing. Wider
    /// than two it spans the middle between the corners the way a door head does; taller than two the courses
    /// below the head are the light.</para></summary>
    Arched,
}

/// <summary>
/// The windows a house wears: which form, what they are made of, how big, how high off the floor, and how far
/// apart. Size is the form's as much as the author's — a stair lattice is 2×2 because the four missing quarters
/// are the whole trick, and a slab band is three courses because a sill and a lintel with nothing between them
/// is not a window — so <see cref="Normalized"/> is what the stamper reads rather than the raw numbers.
/// </summary>
public sealed record WindowStyle
{
    public WindowForm Form { get; init; } = WindowForm.None;

    /// <summary>The block the opening is dressed with. A block id rather than a material because the
    /// metadata is <b>geometry</b> here — which way a stair climbs, which half a slab fills — and a material
    /// resolves its data from where the cell sits, which would turn every stair in the wall the same way.</summary>
    public int Block { get; init; } = Blocks.GlassPane;

    /// <summary>The block a window may be cut <em>into</em>, or -1 to cut wherever one fits.
    ///
    /// <para><b>A window belongs to a material, not only to a wall.</b> Where the wall is one thing the two
    /// questions are the same and spacing alone seats a window well. Where it bands — a run of acacia against
    /// a run of planks — they come apart: a seat chosen by spacing lands half in one band and half in the
    /// other, and an opening cut across that seam reads as damage rather than as a window. Naming the block
    /// the wall has to resolve to lets the panel decide, and the wall answers by being asked rather than by
    /// the seater knowing which patterns exist.</para>
    ///
    /// <para>Matched against what the wall <em>resolves</em> to at the cell, so it works for any pattern that
    /// puts the block there — a stripe, a checker, a noise stop — without naming the pattern.</para></summary>
    public int HostBlock { get; init; } = -1;

    public int HostData { get; init; }

    /// <summary>The block's own variant nibble — which wood, which stone, which dye. The geometry bits are the
    /// stamper's and are added to this.</summary>
    public int Data { get; init; }

    /// <summary>The course above the floor the opening starts at, so a sill of two puts the light at standing
    /// eye level whatever the wall's height becomes.</summary>
    public int Sill { get; init; } = 2;

    public int Width { get; init; } = 2;

    public int Height { get; init; } = 2;

    /// <summary>Clear blocks of wall between one window and the next.</summary>
    public int Spacing { get; init; } = 3;

    /// <summary>The size this form actually builds at, whatever was asked for.</summary>
    public (int Width, int Height) Normalized() => Form switch
    {
        WindowForm.StairLattice => (2, 2),
        WindowForm.SlabBanded => (Math.Max(2, Width), 3),
        // Two corners are the whole of an arch and one cell cannot hold both; and a head that took the only
        // course would be an arch over nothing, so there is always a course of light under it.
        WindowForm.Arched => (Math.Max(2, Width), Math.Max(2, Height)),
        _ => (Math.Max(1, Width), Math.Max(1, Height)),
    };

    /// <summary>A 2×2 stair lattice in oak.</summary>
    public static WindowStyle Lattice { get; } = new()
    {
        Form = WindowForm.StairLattice, Block = Blocks.OakStairs, Sill = 2, Spacing = 3,
    };

    /// <summary>A three-course slab band, two wide, in oak.</summary>
    public static WindowStyle Band { get; } = new()
    {
        Form = WindowForm.SlabBanded, Block = Blocks.WoodenSlab, Width = 2, Sill = 2, Spacing = 3,
    };

    /// <summary>A glazed 2×2 pane window.</summary>
    public static WindowStyle Glazed { get; } = new()
    {
        Form = WindowForm.Pane, Block = Blocks.GlassPane, Width = 2, Height = 2, Sill = 2, Spacing = 3,
    };
}

/// <summary>How the top course of a doorway is dressed.</summary>
public enum DoorHeadForm
{
    /// <summary>Nothing — the doorway is a plain rectangle, which is what every opening was.</summary>
    None,

    /// <summary>An upside-down stair in each corner of the head, raised half outward, so the quarter each is
    /// missing faces into the opening and the two of them round its top corners off. It is the upper half of a
    /// stair lattice doing the same trick for a different hole: a beam that carries the wall over the door and
    /// takes the squareness out of it.</summary>
    Arched,
}

/// <summary>What spans the middle of an arched head on a doorway wider than its two corners.</summary>
public enum DoorHeadFill
{
    /// <summary>An upside-down slab: a half block at the top of its cube, so the middle of the beam sits at the
    /// same height as the raised halves either side of it and the head reads as one line.</summary>
    UpperSlab,

    /// <summary>The whole cube. A beam rather than a moulding, for a head that wants weight.</summary>
    Solid,
}

/// <summary>
/// The beam over a doorway: an <see cref="DoorHeadForm.Arched"/> head puts an upside-down stair in each corner
/// of the opening's top course and spans whatever is between them.
///
/// <para><b>It is dressing on the opening, not a change to it.</b> The doorway is cut as it always was and the
/// head is written into its top course, so a three-course door keeps two clear courses to walk through — which
/// is why a head is only laid on an opening at least three tall. Below that there is nothing to spare.</para>
///
/// <para>Blocks rather than materials, for the reason a window's are: the data on a stair is which way it
/// climbs and on a slab which half it fills, and a material resolving that from the cell would turn both
/// corners the same way and lay a solid lintel instead of an arch.</para>
/// </summary>
public sealed record DoorHeadStyle
{
    public DoorHeadForm Form { get; init; } = DoorHeadForm.None;

    /// <summary>The stair at each corner of the head. Which wood it is, is which block it is — a stair's data
    /// value is entirely geometry, so it carries no variant of its own.</summary>
    public int Block { get; init; } = Blocks.OakStairs;

    /// <summary>What spans the middle where the opening is wider than two.</summary>
    public DoorHeadFill Fill { get; init; } = DoorHeadFill.UpperSlab;

    public int FillBlock { get; init; } = Blocks.WoodenSlab;

    public int FillData { get; init; }

    /// <summary>The narrowest opening a head is worth laying in: two corners and nothing else is already the
    /// whole of it, and one cell cannot hold two corners.</summary>
    public const int LeastWidth = 2;

    /// <summary>The shortest opening one may be laid in — under three courses the head would take the last of
    /// the clear and leave a doorway nobody walks through.</summary>
    public const int LeastHeight = 3;

    /// <summary>Whether this head is laid on an opening of the given size at all.</summary>
    public bool Fits(int width, int height)
        => Form != DoorHeadForm.None && width >= LeastWidth && height >= LeastHeight;

    /// <summary>The block one cell of the head takes: a stair turned outward at each end, and the fill
    /// between. <paramref name="step"/> counts from the opening's low end.</summary>
    public (int Id, int Data) Piece(bool alongX, int step, int width)
    {
        if (step > 0 && step < width - 1)
            return Fill == DoorHeadFill.Solid
                ? (FillBlock, FillData & 0xF)
                : (FillBlock, (FillData & 0x7) | Blocks.SlabUpperHalf);

        // A stair's whole data value is geometry — two bits of facing and the upside-down flag — so there is no
        // variant nibble to carry and <see cref="Data"/> is the fill's alone. Which wood a stair is, is which
        // block it is.
        var toward = alongX
            ? step == 0 ? Blocks.StairWest : Blocks.StairEast
            : step == 0 ? Blocks.StairNorth : Blocks.StairSouth;
        return (Block, toward | Blocks.StairUpsideDown);
    }
}

/// <summary>One window's place in a wall: the <see cref="Wall"/> it is cut through, the low along-axis block
/// coordinate along that run (x for a wall facing ±z, z for one facing ±x), and its size in blocks.
/// <see cref="Sill"/> counts courses up from the floor, so it is the same number a style asked for. The seat
/// carries the run rather than a facing, so it knows the line its wall stands on and needs no box to be cut
/// against.</summary>
public readonly record struct WindowSeat(WallSegment Wall, int Lo, int Width, int Sill, int Height);

/// <summary>
/// Where a house's windows go and what is written into them.
///
/// <para><b>Seating comes before dressing, and is the part that has to be right.</b> A window is cut out of a
/// wall that already stands, so a badly placed one takes a corner post out of the building or opens into the
/// doorway beside it. Each wall is seated on the run between its two corners, the windows are spread evenly and
/// centred on that run — so a wall reads as symmetric rather than as windows starting at one end and stopping
/// when they run out — and any seat that would meet a doorway is dropped rather than shifted. Shifting it would
/// break the spacing of every window after it to save one; the gap where a door is reads as intended.</para>
/// </summary>
public static class HouseWindows
{
    /// <summary>Every window seat on a building's walls, run by run in the order the plan lists them. Empty
    /// when the style asks for none, when the wall is too short to hold one clear of its corners, or when the
    /// opening would not fit between the floor and the wall's last course.
    ///
    /// <para>A run rather than a facing is what a window is seated in, so a building that turns a corner seats
    /// each of its walls on its own length: an L's six walls are six runs, and the short one beside the turn
    /// takes what it can hold rather than what the whole side could.</para></summary>
    /// <param name="hosts">Whether the wall at one cell of a run is a block the window may be cut into, or
    /// null where the style names no host and any cell will do. Passed as a question rather than as the wall
    /// itself: the seater decides <em>where</em> a window goes and has no business knowing what a wall is made
    /// of, and the caller that does know can answer by resolving it.</param>
    public static List<WindowSeat> Seats(
        WindowStyle style, IReadOnlyList<WallSegment> walls,
        int wallExtent, IReadOnlyList<WallOpening>? doors, Func<WallSegment, int, bool>? hosts = null)
    {
        var seats = new List<WindowSeat>();
        if (style.Form == WindowForm.None) return seats;

        var (width, height) = style.Normalized();
        var sill = Math.Max(1, style.Sill);
        if (sill + height - 1 > wallExtent) return seats;      // no wall left above the sill to open

        foreach (var wall in walls)
        {
            // Two blocks in from each end rather than one: clearing the corner cell still leaves an opening
            // hard against the corner post, and a window meeting the post reads as a hole knocked through the
            // frame. The post wants a block of wall beside it before anything is taken out.
            var (seatLo, seatHi) = wall.Seat;
            var placed = style.HostBlock >= 0 && hosts is not null
                ? Panels(seatLo, seatHi, width, Math.Max(0, style.Spacing), along => hosts(wall, along))
                : Spread(seatLo, seatHi, width, Math.Max(0, style.Spacing));
            foreach (var lo in placed)
                if (!MeetsDoor(doors, wall, lo, width))
                    seats.Add(new WindowSeat(wall, lo, width, sill, height));
        }
        return seats;
    }

    /// <summary>The low coordinates of the windows a wall with a named host takes: each unbroken panel of the
    /// host block <b>spread and centred exactly as a whole wall is</b>, skipping a panel whose first window
    /// would stand closer than <paramref name="spacing"/> to the one already placed.
    ///
    /// <para>Where <see cref="Spread"/> divides the run and lets the material fall where it may, this lets the
    /// material divide the run first and then divides each piece. It is the difference between a window that
    /// happens to land on planks and a window that is <em>in</em> the planks — and on a wall whose bands are
    /// four cells and whose spacing is five, the first almost never happens.</para>
    ///
    /// <para><b>The panel is spread rather than given a single centred window, and a uniform wall is why.</b>
    /// A host names a block, not a band: a wall that is one material at the sill course resolves to a single
    /// panel the length of the whole run, and one window centred in that is one window on a twenty-one-block
    /// hall. Spreading each panel gives the banded wall exactly what it had — a two-cell band holds one
    /// two-wide window and no more — and gives the uniform wall the row it was always asking for.</para></summary>
    private static IEnumerable<int> Panels(int runLo, int runHi, int width, int spacing, Func<int, bool> hosts)
    {
        // Tracked as "is there one yet" rather than as a sentinel coordinate: a sentinel far enough below the
        // run to always clear the spacing is also far enough to overflow the subtraction that tests it, which
        // silently refuses the first window on every wall.
        var placed = false;
        var lastEnd = 0;
        for (var at = runLo; at <= runHi; at++)
        {
            if (!hosts(at)) continue;
            var end = at;
            while (end + 1 <= runHi && hosts(end + 1)) end++;

            foreach (var lo in Spread(at, end, width, spacing))
            {
                // Only the seam between two panels can be too tight: within one, the spread already put a
                // clear <paramref name="spacing"/> between neighbours.
                if (placed && lo - lastEnd <= spacing) continue;
                yield return lo;
                (placed, lastEnd) = (true, lo + width - 1);
            }
            at = end;
        }
    }

    /// <summary>The low coordinates of as many <paramref name="width"/>-wide windows as fit between
    /// <paramref name="runLo"/> and <paramref name="runHi"/> inclusive with <paramref name="spacing"/> clear
    /// blocks between them, centred on the run so what is left over is split between the two ends.</summary>
    private static IEnumerable<int> Spread(int runLo, int runHi, int width, int spacing)
    {
        var run = runHi - runLo + 1;
        if (run < width) yield break;
        var stride = width + spacing;
        var count = (run + spacing) / stride;
        var start = runLo + (run - (count * width + (count - 1) * spacing)) / 2;
        for (var index = 0; index < count; index++) yield return start + index * stride;
    }

    /// <summary>Whether a seat would meet a doorway on the same wall — the door's own run plus a block of wall
    /// either side of it, so a window never lands hard against a door jamb. A doorway in another run of wall is
    /// no obstacle even where the two look the same way, which is what carrying the run rather than the facing
    /// buys.</summary>
    private static bool MeetsDoor(IReadOnlyList<WallOpening>? doors, WallSegment wall, int lo, int width)
    {
        if (doors is null) return false;
        foreach (var door in doors)
        {
            if (door.Wall != wall) continue;
            if (lo <= door.Lo + door.Width && door.Lo <= lo + width) return true;
        }
        return false;
    }

    /// <summary>Cut one window and dress it. The cells are written rather than skipped, air included: a window
    /// is an opening taken out of a wall the same pass just built, which is the doorway's rule and not a
    /// material's.</summary>
    public static void Cut(VoxelWorld world, WindowSeat seat, WindowStyle style, int floorY)
    {
        var alongX = seat.Wall.AlongX;
        for (var step = 0; step < seat.Width; step++)
            for (var course = 0; course < seat.Height; course++)
            {
                var (x, z) = seat.Wall.Cell(seat.Lo + step);
                var (id, data) = Piece(style, seat, alongX, step, course);
                world.SetBlock(x, floorY + seat.Sill + course, z, id, data);
            }
    }

    /// <summary>The block one cell of a window takes, by form: which stair turned which way, which half of a
    /// slab, a pane, or the air between them.</summary>
    private static (int Id, int Data) Piece(
        WindowStyle style, WindowSeat seat, bool alongX, int step, int course)
    {
        var variant = style.Data & 0x7;
        switch (style.Form)
        {
            case WindowForm.StairLattice:
            {
                // Each stair keeps its raised half on the outside of the 2×2, so the quarter it is missing
                // points at the group's centre and the four missing quarters meet there as the light.
                var toward = alongX
                    ? step == 0 ? Blocks.StairWest : Blocks.StairEast
                    : step == 0 ? Blocks.StairNorth : Blocks.StairSouth;
                // The upper pair hang their step below them, which is what turns the light from a slot into a
                // diamond: upright stairs alone would leave the gap open all the way to the lintel.
                return (style.Block, course == 0 ? toward : toward | Blocks.StairUpsideDown);
            }
            case WindowForm.SlabBanded:
                return course switch
                {
                    0 => (style.Block, variant),                              // the sill
                    2 => (style.Block, variant | Blocks.SlabUpperHalf),       // the lintel
                    _ => (Blocks.Air, 0),
                };
            case WindowForm.Arched:
            {
                // Only the top course carries anything, and only at its two ends: the arch is the corners
                // rounded off and everything under them is the light. A wider opening leaves the middle of the
                // head open rather than spanning it — a window is not carrying a wall over a doorway, so there
                // is nothing there for a beam to do.
                if (course != seat.Height - 1 || (step > 0 && step < seat.Width - 1)) return (Blocks.Air, 0);
                var facing = alongX
                    ? step == 0 ? Blocks.StairWest : Blocks.StairEast
                    : step == 0 ? Blocks.StairNorth : Blocks.StairSouth;
                // A stair's whole data value is geometry — two bits of facing and the upside-down flag — so
                // there is no variant nibble to carry, and which wood it is, is which block it is.
                return (style.Block, facing | Blocks.StairUpsideDown);
            }
            case WindowForm.Pane:
                return (style.Block, style.Data & 0xF);
            default:
                return (Blocks.Air, 0);      // Open, and the air a slab band leaves between its two halves
        }
    }
}

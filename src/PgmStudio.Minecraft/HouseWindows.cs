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

/// <summary>One window's place in a wall: the <see cref="Edge"/> it is cut through, the low along-axis block
/// coordinate (x for a Z edge, z for an X edge), and its size in blocks. <see cref="Sill"/> counts courses up
/// from the floor, so it is the same number a style asked for.</summary>
public readonly record struct WindowSeat(RoomEdge Edge, int Lo, int Width, int Sill, int Height);

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
    /// <summary>Every window seat on the four walls of a footprint, in edge order. Empty when the style asks
    /// for none, when the wall is too short to hold one clear of its corners, or when the opening would not fit
    /// between the floor and the wall's last course.</summary>
    public static List<WindowSeat> Seats(
        WindowStyle style, int minX, int minZ, int maxX, int maxZ,
        int wallExtent, IReadOnlyList<RoomDoor>? doors)
    {
        var seats = new List<WindowSeat>();
        if (style.Form == WindowForm.None) return seats;

        var (width, height) = style.Normalized();
        var sill = Math.Max(1, style.Sill);
        if (sill + height - 1 > wallExtent) return seats;      // no wall left above the sill to open

        foreach (var edge in new[] { RoomEdge.NegZ, RoomEdge.PosZ, RoomEdge.NegX, RoomEdge.PosX })
        {
            var alongMin = edge is RoomEdge.NegZ or RoomEdge.PosZ ? minX : minZ;
            var alongMax = edge is RoomEdge.NegZ or RoomEdge.PosZ ? maxX : maxZ;
            foreach (var lo in Spread(alongMin + 1, alongMax - 1, width, Math.Max(0, style.Spacing)))
                if (!MeetsDoor(doors, edge, lo, width))
                    seats.Add(new WindowSeat(edge, lo, width, sill, height));
        }
        return seats;
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
    /// either side of it, so a window never lands hard against a door jamb.</summary>
    private static bool MeetsDoor(IReadOnlyList<RoomDoor>? doors, RoomEdge edge, int lo, int width)
    {
        if (doors is null) return false;
        foreach (var door in doors)
        {
            if (door.Edge != edge) continue;
            if (lo <= door.Lo + door.Width && door.Lo <= lo + width) return true;
        }
        return false;
    }

    /// <summary>Cut one window and dress it. The cells are written rather than skipped, air included: a window
    /// is an opening taken out of a wall the same pass just built, which is the doorway's rule and not a
    /// material's.</summary>
    public static void Cut(
        VoxelWorld world, WindowSeat seat, WindowStyle style, int floorY,
        int minX, int minZ, int maxX, int maxZ)
    {
        var alongX = seat.Edge is RoomEdge.NegZ or RoomEdge.PosZ;
        var fixedAt = seat.Edge switch
        {
            RoomEdge.NegZ => minZ,
            RoomEdge.PosZ => maxZ,
            RoomEdge.NegX => minX,
            _ => maxX,
        };

        for (var step = 0; step < seat.Width; step++)
            for (var course = 0; course < seat.Height; course++)
            {
                var along = seat.Lo + step;
                var (x, z) = alongX ? (along, fixedAt) : (fixedAt, along);
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
            case WindowForm.Pane:
                return (style.Block, style.Data & 0xF);
            default:
                return (Blocks.Air, 0);
        }
    }
}

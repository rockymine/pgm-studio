namespace PgmStudio.Domain;

/// <summary>The outward direction a room wall faces: the wall on the low-Z side faces <see cref="NegZ"/>.
/// Also the direction a spawn cube's door opens toward.</summary>
public enum RoomEdge { NegZ, PosZ, NegX, PosX }

/// <summary>A door opening cut into a room shell wall: the <see cref="Edge"/> it sits on, the low
/// along-axis block coordinate <see cref="Lo"/> (x for a Z edge, z for an X edge), and its
/// <see cref="Width"/> in blocks.</summary>
public readonly record struct RoomDoor(RoomEdge Edge, int Lo, int Width);

/// <summary>The spawn/wool floor pad: a <see cref="Size"/>×<see cref="Size"/> square of wool at
/// (<see cref="MinX"/>, <see cref="MinZ"/>). <see cref="Shifted"/> flags a pad moved off its marker to keep
/// the wall clearance — the exported spawn/wool point follows the pad, so the shift is author-visible.</summary>
public readonly record struct RoomPad(int MinX, int MinZ, int Size, bool Shifted)
{
    /// <summary>The pad's centre — the point the export emits as the spawn/wool location. A whole block
    /// coordinate for a 2×2 pad (the marker's grid line), a block centre (.5) for a 1×1 or 3×3.</summary>
    public double CenterX => MinX + Size / 2.0;

    /// <inheritdoc cref="CenterX"/>
    public double CenterZ => MinZ + Size / 2.0;
}

/// <summary>A monument seat inside a spawn room: the interior floor cell and the <see cref="Wall"/> the
/// pedestal hugs (which side its label sign hangs toward the room centre from).</summary>
public readonly record struct MonumentSlot(int X, int Z, RoomEdge Wall);

/// <summary>
/// The resolved geometry of one stamped room, in absolute world blocks, all rects min-inclusive /
/// max-exclusive: the shell footprint (the walls stand on its perimeter), the interior floor inside them,
/// the spawn/wool <see cref="Pad"/>, and the <see cref="Doors"/>. Derived once per room by
/// <see cref="RoomFrames.Resolve"/> and consumed by the stampers, the structure preview, and the exported
/// XML alike, so none of them can disagree about where the room is.
/// </summary>
public sealed record RoomFrame(
    int MinX, int MinZ, int MaxX, int MaxZ,
    RoomPad Pad,
    IReadOnlyList<RoomDoor> Doors)
{
    public int Width => MaxX - MinX;
    public int Depth => MaxZ - MinZ;

    /// <summary>The interior floor — the footprint inside the one-block walls.</summary>
    public int InteriorMinX => MinX + 1;
    public int InteriorMinZ => MinZ + 1;
    public int InteriorMaxX => MaxX - 1;
    public int InteriorMaxZ => MaxZ - 1;
}

/// <summary>
/// The room-frame rules (docs/world-export/structures.md, WX1–WX7): how a wool-room or spawn piece, its
/// marker, and its entry interfaces resolve into the shell the export stamps. Pure block geometry with no
/// world access, so the plan validator refuses with the same rules the stampers build by.
/// </summary>
public static class RoomFrames
{
    /// <summary>The smallest legal shell span (WX2) — a 4×4 interior, which still seats four corner
    /// monuments, the chest stacks and a pad.</summary>
    public const int MinShellSpan = 6;

    /// <summary>The smallest legal room/spawn piece span in blocks (WX2): the shell minimum plus the
    /// one-block clean ring on each side.</summary>
    public const int MinPieceSpan = MinShellSpan + 2;

    /// <summary>Clear floor kept between the pad and every wall (WX4).</summary>
    public const int PadWallClearance = 1;

    /// <summary>Whether a piece is too small to stamp a room (WX2) — spans measured in blocks, never cells.</summary>
    public static bool PieceTooSmall(int pieceWidth, int pieceDepth) =>
        pieceWidth < MinPieceSpan || pieceDepth < MinPieceSpan;

    /// <summary>Whether a marker's block-lattice parity differs between axes (WX3). The pad is always
    /// square, so a grid-line x with a block-centre z has no pad and refuses at validation.</summary>
    public static bool MixedParity(double markerX, double markerZ) =>
        IsGridLine(markerX) != IsGridLine(markerZ);

    /// <summary>The door width for a wall whose interior runs <paramref name="interiorAcross"/> blocks along
    /// it (WX7): an odd wall centres a 3-wide door; an even wall takes the common 4 once the interior is 6
    /// across, narrowing to 2 at the 4-across minimum. Always ≤ interior − 2, so the door-wall corner cells
    /// are never exposed from outside.</summary>
    public static int DoorWidth(int interiorAcross) =>
        interiorAcross % 2 == 1 ? 3 : interiorAcross >= 6 ? 4 : 2;

    /// <summary>
    /// Resolve a room's frame from its piece rect, its marker, and its entry interfaces (WX1–WX7).
    /// <paramref name="entries"/> are degenerate rects on the piece boundary (a seam or build-zone
    /// interface segment; zero-thickness on the seam axis); pass a <paramref name="spawnDoorEdge"/> instead
    /// for a spawn room's single yaw-derived door. Null with a <paramref name="refusal"/> message when a WX
    /// rule refuses — the same wording the validator reports.
    /// </summary>
    public static RoomFrame? Resolve(
        int pieceMinX, int pieceMinZ, int pieceMaxX, int pieceMaxZ,
        double markerX, double markerZ,
        IReadOnlyList<(double MinX, double MinZ, double MaxX, double MaxZ)> entries,
        RoomEdge? spawnDoorEdge,
        out string? refusal)
    {
        refusal = null;
        var pieceWidth = pieceMaxX - pieceMinX;
        var pieceDepth = pieceMaxZ - pieceMinZ;
        if (PieceTooSmall(pieceWidth, pieceDepth))
        {
            refusal = $"piece {pieceWidth}×{pieceDepth} is too small to stamp a room: the shell would be "
                + $"{pieceWidth - 2}×{pieceDepth - 2}, minimum {MinShellSpan}×{MinShellSpan} "
                + $"(piece ≥ {MinPieceSpan}×{MinPieceSpan} blocks) (WX2)";
            return null;
        }
        if (MixedParity(markerX, markerZ))
        {
            refusal = "marker parity differs between axes; the pad is always square — place the marker on a "
                + "cell corner, or on a cell centre in both axes (WX3)";
            return null;
        }

        // WX1 — the shell footprint is the piece inset by the one-block clean ring.
        int minX = pieceMinX + 1, minZ = pieceMinZ + 1, maxX = pieceMaxX - 1, maxZ = pieceMaxZ - 1;

        // The pad's allowed region is the interior inset by the wall clearance (WX4).
        var pad = PlacePad(markerX, markerZ,
            minX + 1 + PadWallClearance, minZ + 1 + PadWallClearance,
            maxX - 1 - PadWallClearance, maxZ - 1 - PadWallClearance);
        if (pad is null)
        {
            refusal = "no room for the spawn/wool pad inside the interior (WX4)";
            return null;
        }

        List<RoomDoor> doors;
        if (spawnDoorEdge is { } doorEdge)
        {
            var interiorAcross = doorEdge is RoomEdge.NegZ or RoomEdge.PosZ ? maxX - minX - 2 : maxZ - minZ - 2;
            var width = DoorWidth(interiorAcross);
            var lo = doorEdge is RoomEdge.NegZ or RoomEdge.PosZ
                ? minX + (maxX - minX - width) / 2
                : minZ + (maxZ - minZ - width) / 2;
            doors = [new RoomDoor(doorEdge, lo, width)];
        }
        else
        {
            doors = [];
            foreach (var entry in entries)
            {
                if (ClassifyEntry(entry, pieceMinX, pieceMinZ, pieceMaxX, pieceMaxZ) is not { } placed) continue;
                var (edge, intervalLo, intervalHi) = placed;
                var alongZ = edge is RoomEdge.NegX or RoomEdge.PosX;
                var interiorAcross = alongZ ? maxZ - minZ - 2 : maxX - minX - 2;
                var width = DoorWidth(interiorAcross);
                // Centre the door on the entry interval, clamped onto the wall run between the ring corners.
                var (runLo, runHi) = alongZ ? (minZ + 1, maxZ - 1) : (minX + 1, maxX - 1);
                var ideal = (int)Math.Round((intervalLo + intervalHi) / 2.0 - width / 2.0, MidpointRounding.AwayFromZero);
                var lo = Math.Min(Math.Max(ideal, runLo), runHi - width);
                doors.Add(new RoomDoor(edge, lo, width));
            }
            if (doors.Count == 0)
            {
                refusal = "wool room is unreachable: no land seam and no abutting build zone to enter by (WX6)";
                return null;
            }
        }

        return new RoomFrame(minX, minZ, maxX, maxZ, pad.Value, doors);
    }

    /// <summary>The interior corner cells (chest stacks in a wool cage), door-wall corners first.</summary>
    public static IReadOnlyList<(int X, int Z)> InteriorCorners(RoomFrame frame) =>
    [
        (frame.InteriorMinX, frame.InteriorMinZ), (frame.InteriorMaxX - 1, frame.InteriorMinZ),
        (frame.InteriorMinX, frame.InteriorMaxZ - 1), (frame.InteriorMaxX - 1, frame.InteriorMaxZ - 1),
    ];

    /// <summary>
    /// The ordered monument seats of a spawn room whose door is <paramref name="door"/>: the door-wall
    /// corners, then the back-wall corners, then the back wall filling inward, then the door wall — skipping
    /// the cells directly inside the door opening. The list's length is the room's monument capacity; the
    /// caller takes the first N.
    /// </summary>
    public static IReadOnlyList<MonumentSlot> MonumentSlots(RoomFrame frame, RoomDoor door)
    {
        // Work in a door-local reading: `along` runs along the door wall, `near` is the door wall's interior
        // row/column and `far` the opposite wall's. Mapping back out depends only on the door edge.
        var alongZ = door.Edge is RoomEdge.NegX or RoomEdge.PosX;
        var (alongLo, alongHi) = alongZ ? (frame.InteriorMinZ, frame.InteriorMaxZ) : (frame.InteriorMinX, frame.InteriorMaxX);
        var (near, far, nearWall, farWall) = door.Edge switch
        {
            RoomEdge.NegZ => (frame.InteriorMinZ, frame.InteriorMaxZ - 1, RoomEdge.NegZ, RoomEdge.PosZ),
            RoomEdge.PosZ => (frame.InteriorMaxZ - 1, frame.InteriorMinZ, RoomEdge.PosZ, RoomEdge.NegZ),
            RoomEdge.NegX => (frame.InteriorMinX, frame.InteriorMaxX - 1, RoomEdge.NegX, RoomEdge.PosX),
            _ => (frame.InteriorMaxX - 1, frame.InteriorMinX, RoomEdge.PosX, RoomEdge.NegX),
        };
        MonumentSlot Seat(int along, int crossAxis, RoomEdge wall) =>
            alongZ ? new MonumentSlot(crossAxis, along, wall) : new MonumentSlot(along, crossAxis, wall);
        bool InDoorSpan(int along) => along >= door.Lo && along < door.Lo + door.Width;

        var slots = new List<MonumentSlot>
        {
            Seat(alongLo, near, nearWall), Seat(alongHi - 1, near, nearWall),
            Seat(alongLo, far, farWall), Seat(alongHi - 1, far, farWall),
        };
        for (var along = alongLo + 1; along < alongHi - 1; along++) slots.Add(Seat(along, far, farWall));
        for (var along = alongLo + 1; along < alongHi - 1; along++)
            if (!InDoorSpan(along)) slots.Add(Seat(along, near, nearWall));
        return slots;
    }

    /// <summary>Whether <paramref name="coordinate"/> sits on a block grid line (integer) as opposed to a
    /// block centre (.5) — the parity that picks the pad class (WX3).</summary>
    public static bool IsGridLine(double coordinate) => coordinate == Math.Floor(coordinate);

    // WX3/WX4 — the square pad: parity picks 2 (straddling a grid line) or 3 (centred on a block, degrading
    // to 1 jointly when either axis lacks the clearance), then clamp into the interior keeping one block of
    // clear floor to every wall, flagging any shift.
    private static RoomPad? PlacePad(
        double markerX, double markerZ, int allowedMinX, int allowedMinZ, int allowedMaxX, int allowedMaxZ)
    {
        int size;
        if (IsGridLine(markerX)) size = 2;
        else
        {
            var fitsLarge = 3 <= allowedMaxX - allowedMinX && 3 <= allowedMaxZ - allowedMinZ;
            size = fitsLarge ? 3 : 1;
        }
        if (size > allowedMaxX - allowedMinX || size > allowedMaxZ - allowedMinZ) return null;

        int IdealMin(double marker) => IsGridLine(marker)
            ? (int)marker - size / 2
            : (int)Math.Floor(marker) - (size - 1) / 2;
        var idealX = IdealMin(markerX);
        var idealZ = IdealMin(markerZ);
        var placedX = Math.Min(Math.Max(idealX, allowedMinX), allowedMaxX - size);
        var placedZ = Math.Min(Math.Max(idealZ, allowedMinZ), allowedMaxZ - size);
        return new RoomPad(placedX, placedZ, size, placedX != idealX || placedZ != idealZ);
    }

    // An entry rect (degenerate on the seam axis) classified against the piece boundary: which edge it lies
    // on and its along-axis interval. Null when it doesn't sit on this piece's boundary.
    private static (RoomEdge Edge, double Lo, double Hi)? ClassifyEntry(
        (double MinX, double MinZ, double MaxX, double MaxZ) entry,
        int pieceMinX, int pieceMinZ, int pieceMaxX, int pieceMaxZ)
    {
        const double tolerance = 0.01;
        bool On(double a, double b) => Math.Abs(a - b) < tolerance;
        if (On(entry.MinX, entry.MaxX))   // a vertical seam line at x
        {
            if (On(entry.MinX, pieceMinX)) return (RoomEdge.NegX, entry.MinZ, entry.MaxZ);
            if (On(entry.MinX, pieceMaxX)) return (RoomEdge.PosX, entry.MinZ, entry.MaxZ);
            return null;
        }
        if (On(entry.MinZ, entry.MaxZ))   // a horizontal seam line at z
        {
            if (On(entry.MinZ, pieceMinZ)) return (RoomEdge.NegZ, entry.MinX, entry.MaxX);
            if (On(entry.MinZ, pieceMaxZ)) return (RoomEdge.PosZ, entry.MinX, entry.MaxX);
            return null;
        }
        return null;
    }
}

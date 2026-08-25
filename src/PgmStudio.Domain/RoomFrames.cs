
using PgmStudio.Vocabulary;

namespace PgmStudio.Domain;

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

/// <summary>The room-frame rule ids a refusal cites, from the WX checklist in
/// <c>docs/world-export/structures.md</c>. Stable names for what a refusal is about, the way <c>HS*</c> names
/// a house-style rule and <c>PL*</c> a plan one — and kept apart from any task-tracking id, since a rule an
/// author or another tool reads back off a refusal has to keep meaning the same thing long after the task
/// that added it has left the board.</summary>
public static class RoomFrameRules
{
    /// <summary>The shell's footprint is the piece rect inset one block on every side. The ring of clean
    /// floor around a room is part of what a piece promises, so a 10×10 piece carries an 8×8 shell and a
    /// 10×20 piece an 8×18 one; the shell takes the rect's own orientation, and the fanned rect orients the
    /// orbit images.</summary>
    /// <remarks>Size the piece for the shell it should carry: a shell is always two blocks narrower than its
    /// piece in each axis. Nothing else moves the footprint — <c>WX8</c>'s negotiation is the one thing that
    /// pulls an edge back off it.</remarks>
    [Rule(RuleConcern.Plan, RuleConcern.Structure)]
    public const string ShellFootprint = "WX1";

    /// <summary>The piece cannot hold a shell of the least legal span once the clean ring is taken off it.</summary>
    /// <remarks>Enlarge the piece. A room is its piece inset by the clean ring, and what is left has to hold a shell of the least legal span in both axes.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Structure)]
    public const string PieceTooSmall = "WX2";

    /// <summary>The marker's block-lattice parity differs between axes, and the pad is always square.</summary>
    /// <remarks>Move the marker half a block on one axis. The pad is square, so both axes must round the same way off the block lattice.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Structure, RuleConcern.Spawn)]
    public const string MarkerParity = "WX3";

    /// <summary>The pad keeps at least one block of clear floor to every wall. A marker sitting too close
    /// has its pad shifted inward by the minimum that restores the clearance — the exported point moves with
    /// it — and a 3×3 is chosen only where it still fits after that shift. An interior with no room for the
    /// pad even shifted is refused.</summary>
    /// <remarks>Enlarge the piece or shrink the pad. The wall clearance is kept first and the pad has to fit
    /// in what remains inside it; where a pad merely moved, the plan lint says so, because the exported
    /// spawn or wool point follows the pad rather than the marker.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Structure, RuleConcern.Spawn)]
    public const string PadClearance = "WX4";

    /// <summary>The exported spawn or wool location is the pad's centre, after any <c>WX4</c> shift. The
    /// world is the ground truth the map document has to agree with, so the point follows the pad rather
    /// than the marker that asked for it, and it snaps to the half-block lattice rather than to an
    /// integer.</summary>
    /// <remarks>Move the marker to move the point. Where a pad was shifted to keep its wall clearance the
    /// exported point moves with it, which the structure preview draws and the plan lint notes — so a point
    /// that is not where the marker was put has a shift behind it.</remarks>
    [Rule(RuleConcern.Structure, RuleConcern.Spawn, RuleConcern.Objective, RuleConcern.World)]
    public const string PadIsPoint = "WX5";

    /// <summary>A wool room with no seam and no abutting build zone has nothing to enter it by.</summary>
    /// <remarks>Give the wool room a border with a neighbouring piece, or place a build zone against it. A room nothing abuts has no door that can be cut.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Structure, RuleConcern.Objective)]
    public const string RoomUnreachable = "WX6";

    /// <summary>A door's width follows the wall it is cut into. An odd interior wall centres a 3-wide door;
    /// an even wall takes 4 once the interior is at least 6 across and narrows to 2 at the 4-across minimum.
    /// The invariant under the numbers is that a door is at least one block narrower than the interior on
    /// each side, so the corner cells a spawn cube seats monuments in are never opened to the
    /// outside.</summary>
    /// <remarks>Widen the interior to widen the door — the width is derived from the wall, never authored.
    /// A room that wants a 4-wide door needs an even wall and 6 blocks of interior across it.</remarks>
    [Rule(RuleConcern.Structure)]
    public const string DoorWidth = "WX7";

    /// <summary>An iron cube stands outside the room shell, inside the piece, with one block of clear air to
    /// the wall. Fitting is a negotiation in a fixed order: the shell pulls one edge back from its
    /// <c>WX1</c> footprint by the minimum that clears the cube, then the cube itself
    /// degrades by marker parity. The room wins — a shrink is legal only while the shell holds
    /// <c>WX2</c>'s minimum and the spawn marker stays inside the interior — so a marker no
    /// yield can seat resolves unplaceable.</summary>
    /// <remarks>Enlarge the spawn piece, or move the iron marker further from the shell. The cube needs its
    /// own footprint plus one block of clear air in the ring between the shell and the piece edge, and the
    /// shell will not shrink past the least legal span to make room for it.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Structure, RuleConcern.Spawn)]
    public const string IronFit = "WX8";

    /// <summary>Every structure marker resolves to placeable or not, and an unplaceable one is not an
    /// error the export throws on: it stamps nothing, the room takes its full <c>WX1</c>
    /// footprint, and the marker stays on the board where the author put it. Validation flags it and the
    /// structure preview draws only what will be placed, so the iso view never shows a cube the export
    /// declines.</summary>
    /// <remarks>Nothing is dropped from the document for you. Read the plan lint for the marker the finding
    /// names and either give it the room <c>WX8</c> asks for or take it off the plan — a marker
    /// left unplaceable is a placement the author can still see on the canvas and nothing in the
    /// world.</remarks>
    [Rule(RuleConcern.Structure, RuleConcern.World)]
    public const string MarkerPlaceability = "WX9";

    /// <summary>A bound room style builds a shell taller than the build ceiling — <see cref="BuildCeiling"/>'s
    /// clearance over the ground it stands on. A room's shell is authored geometry and is subject to no cap of
    /// its own, so a tall storey stack swallows the goal marker that hangs
    /// <see cref="BuildCeiling.MarkerOver"/> blocks over that ceiling, and the map's own sky sign ends up
    /// inside the building it points at. Measured on the <b>smallest</b> shell a room may be, since every
    /// sloped roof only climbs further on a bigger footprint: a style refused here has no footprint it could
    /// have been stamped on.</summary>
    /// <remarks>Take courses out of the shell — a storey off the stack, a shallower roof pitch, or a lower clear — until it stands under the build ceiling. The cap is the same one players build under, and a marker hanging above it is what makes a goal readable across the map.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Style, RuleConcern.Structure, RuleConcern.Objective)]
    public const string ShellOverCeiling = "WX10";

    /// <summary>A stamped structure stands over ground the cell beside it does not have. Its foundation fills
    /// the column under its whole footprint, so where the neighbouring cell is void or well below the floor
    /// what the building meets the world with is a sheer face of bedrock — a wall nobody drew, at a height
    /// nobody chose, which a player cannot climb and no other read reports.</summary>
    /// <remarks>Bring the ground up to the building, or move the building onto ground that carries it: the drop is measured from the floor it stands on to the surface of the cell beside it, and a step of one is a doorstep rather than a wall. A building deliberately sited on a ledge is the case to ignore — this is a complaint, and the world builds either way.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Structure, RuleConcern.World, RuleConcern.Terrain)]
    public const string StructureOnAPlinth = "WX11";
}

/// <summary>One iron marker's resolution beside a spawn room (WX8/WX9): the cube footprint min corner and
/// <see cref="Size"/> (4 or 2 on a grid-line marker, 3 on a block centre, any of the three on a marker whose
/// axes disagree), or — when no legal strip exists even after the room yields and the cube degrades — an
/// unplaceable marker (<see cref="Placeable"/> false): nothing stamps, the marker stays on the board, and
/// validation flags it.</summary>
public readonly record struct IronResolution(double MarkerX, double MarkerZ, int MinX, int MinZ, int Size, bool Placeable);

/// <summary>A room resolution: the <see cref="Frame"/> (whose shell may have yielded to iron) plus the
/// piece's <see cref="Iron"/> placements, one per marker in input order.</summary>
public sealed record ResolvedRoom(RoomFrame Frame, IReadOnlyList<IronResolution> Iron);

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

    /// <inheritdoc cref="ResolveRoom"/>
    /// <remarks>The frame-only convenience: no iron markers, returns just the frame.</remarks>
    public static RoomFrame? Resolve(
        int pieceMinX, int pieceMinZ, int pieceMaxX, int pieceMaxZ,
        double markerX, double markerZ,
        IReadOnlyList<(double MinX, double MinZ, double MaxX, double MaxZ)> entries,
        RoomEdge? spawnDoorEdge,
        out Finding? refusal)
        => ResolveRoom(pieceMinX, pieceMinZ, pieceMaxX, pieceMaxZ, markerX, markerZ,
            entries, spawnDoorEdge, [], out refusal)?.Frame;

    /// <summary>
    /// Resolve a room from its piece rect, its marker, its entry interfaces, and the piece's iron markers
    /// (WX1–WX9). <paramref name="entries"/> are degenerate rects on the piece boundary (a seam or
    /// build-zone interface segment; zero-thickness on the seam axis); pass a
    /// <paramref name="spawnDoorEdge"/> instead for a spawn room's single yaw-derived door.
    /// <paramref name="ironMarkers"/> resolve to cubes outside the shell (the shell yields, the cube
    /// degrades — WX8) or to unplaceable markers (WX9). Null with a <paramref name="refusal"/> naming the
    /// <see cref="RoomFrameRules"/> id that refused — the same finding the validator reports.
    ///
    /// <para>This answers a room <em>or</em> a refusal rather than a <see cref="Findings"/> list, and that is
    /// the difference between a resolve and a gate: a gate reads a document and collects everything wrong with
    /// it, while a resolve is producing a value and stops at the first thing that makes producing it
    /// impossible. There is no second WX fault to report once the piece is too small to hold a shell.</para>
    /// </summary>
    public static ResolvedRoom? ResolveRoom(
        int pieceMinX, int pieceMinZ, int pieceMaxX, int pieceMaxZ,
        double markerX, double markerZ,
        IReadOnlyList<(double MinX, double MinZ, double MaxX, double MaxZ)> entries,
        RoomEdge? spawnDoorEdge,
        IReadOnlyList<(double X, double Z)> ironMarkers,
        out Finding? refusal)
    {
        refusal = null;
        var pieceWidth = pieceMaxX - pieceMinX;
        var pieceDepth = pieceMaxZ - pieceMinZ;
        if (PieceTooSmall(pieceWidth, pieceDepth))
        {
            refusal = new Finding(RoomFrameRules.PieceTooSmall,
                $"piece {pieceWidth}×{pieceDepth} is too small to stamp a room: the shell would be "
                + $"{pieceWidth - 2}×{pieceDepth - 2}, minimum {MinShellSpan}×{MinShellSpan} "
                + $"(piece ≥ {MinPieceSpan}×{MinPieceSpan} blocks)");
            return null;
        }
        if (MixedParity(markerX, markerZ))
        {
            refusal = new Finding(RoomFrameRules.MarkerParity,
                "marker parity differs between axes; the pad is always square — place the marker on a "
                + "cell corner, or on a cell centre in both axes");
            return null;
        }

        // WX1 — the shell footprint is the piece inset by the one-block clean ring.
        int minX = pieceMinX + 1, minZ = pieceMinZ + 1, maxX = pieceMaxX - 1, maxZ = pieceMaxZ - 1;

        // WX8 — each iron marker in turn: the cube sits outside the shell with one block of clear air,
        // never fused. The room has priority: the shell pulls one edge back as far as WX2 and the room
        // marker allow, the cube degrades by parity, and an unfittable marker resolves unplaceable (WX9).
        var iron = new List<IronResolution>();
        foreach (var (ironX, ironZ) in ironMarkers)
            iron.Add(PlaceIron(ironX, ironZ, pieceMinX, pieceMinZ, pieceMaxX, pieceMaxZ,
                markerX, markerZ, ref minX, ref minZ, ref maxX, ref maxZ));

        // The pad's allowed region is the interior inset by the wall clearance (WX4).
        var pad = PlacePad(markerX, markerZ,
            minX + 1 + PadWallClearance, minZ + 1 + PadWallClearance,
            maxX - 1 - PadWallClearance, maxZ - 1 - PadWallClearance);
        if (pad is null)
        {
            refusal = new Finding(RoomFrameRules.PadClearance,
                "no room for the spawn/wool pad inside the interior");
            return null;
        }

        List<RoomDoor> doors;
        if (spawnDoorEdge is { } doorEdge)
        {
            var alongX = doorEdge.AlongX();
            var interiorAcross = alongX ? maxX - minX - 2 : maxZ - minZ - 2;
            var width = DoorWidth(interiorAcross);
            var lo = alongX
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
                var alongX = edge.AlongX();
                var interiorAcross = alongX ? maxX - minX - 2 : maxZ - minZ - 2;
                var width = DoorWidth(interiorAcross);
                // Centre the door on the entry interval, clamped onto the wall run between the ring corners.
                var (runLo, runHi) = alongX ? (minX + 1, maxX - 1) : (minZ + 1, maxZ - 1);
                var ideal = (int)Math.Round((intervalLo + intervalHi) / 2.0 - width / 2.0, MidpointRounding.AwayFromZero);
                var lo = Math.Min(Math.Max(ideal, runLo), runHi - width);
                doors.Add(new RoomDoor(edge, lo, width));
            }
            if (doors.Count == 0)
            {
                refusal = new Finding(RoomFrameRules.RoomUnreachable,
                    "wool room is unreachable: no land seam and no abutting build zone to enter by");
                return null;
            }
        }

        return new ResolvedRoom(new RoomFrame(minX, minZ, maxX, maxZ, pad.Value, doors), iron);
    }

    /// <summary>Air blocks kept between an iron cube and the room shell (WX8) — the same one-block
    /// clearance the pad keeps to the walls.</summary>
    public const int IronGap = 1;

    // Resolve one iron marker against the current shell, shrinking the shell in place when a legal yield
    // exists. Size ladder by parity: a grid-line marker centres 4 then 2, a block-centre marker centres 3,
    // and a marker that is a grid line on one axis and a block centre on the other centres no square at all,
    // so it takes the whole ladder and settles half a block off centre on the odd axis. A shrink candidate
    // pulls exactly one shell edge back to clear the cube plus gap, and is legal while the shell holds WX2
    // and the room marker stays inside the interior; the largest retained area wins, ties broken toward
    // moving the edge farthest from the room marker — a marker-relative choice, so orbit images shrink
    // mirror-consistently.
    private static IronResolution PlaceIron(
        double ironX, double ironZ, int pieceMinX, int pieceMinZ, int pieceMaxX, int pieceMaxZ,
        double markerX, double markerZ, ref int minX, ref int minZ, ref int maxX, ref int maxZ)
    {
        int[] sizes = IsGridLine(ironX) == IsGridLine(ironZ)
            ? IsGridLine(ironX) ? [4, 2] : [3]
            : [4, 3, 2];
        foreach (var size in sizes)
        {
            // The cube's low corner: the marker less half its span, put back on the block lattice. Rounding
            // away from zero is what keeps a half-block landing symmetric — an orbit image of the cube covers
            // the images of its cells rather than a row one block off.
            int Lo(double marker) => (int)Math.Round(marker - size / 2.0, MidpointRounding.AwayFromZero);
            int cubeMinX = Lo(ironX), cubeMinZ = Lo(ironZ);
            int cubeMaxX = cubeMinX + size, cubeMaxZ = cubeMinZ + size;
            if (cubeMinX < pieceMinX || cubeMinZ < pieceMinZ || cubeMaxX > pieceMaxX || cubeMaxZ > pieceMaxZ)
                continue;   // off the piece at this size — try the smaller cube

            bool Separated(int shellMinX, int shellMinZ, int shellMaxX, int shellMaxZ) =>
                shellMaxX <= cubeMinX - IronGap || shellMinX >= cubeMaxX + IronGap
                || shellMaxZ <= cubeMinZ - IronGap || shellMinZ >= cubeMaxZ + IronGap;
            if (Separated(minX, minZ, maxX, maxZ))
                return new IronResolution(ironX, ironZ, cubeMinX, cubeMinZ, size, Placeable: true);

            var candidates = new List<(int MinX, int MinZ, int MaxX, int MaxZ, double EdgeDistance)>
            {
                // one shell edge pulled back per candidate; EdgeDistance = room marker to the moved edge,
                // on that edge's own axis
                (minX, minZ, cubeMinX - IronGap, maxZ, Math.Abs(markerX - (cubeMinX - IronGap))),
                (cubeMaxX + IronGap, minZ, maxX, maxZ, Math.Abs(markerX - (cubeMaxX + IronGap))),
                (minX, minZ, maxX, cubeMinZ - IronGap, Math.Abs(markerZ - (cubeMinZ - IronGap))),
                (minX, cubeMaxZ + IronGap, maxX, maxZ, Math.Abs(markerZ - (cubeMaxZ + IronGap))),
            };
            // Legal while WX2 holds and the room marker stays inside the interior — the pad may still
            // clamp with a WX4 shift, exactly as it can against an un-shrunk wall.
            var legal = candidates
                .Where(c => c.MaxX - c.MinX >= MinShellSpan && c.MaxZ - c.MinZ >= MinShellSpan
                    && markerX >= c.MinX + 1 && markerX <= c.MaxX - 1
                    && markerZ >= c.MinZ + 1 && markerZ <= c.MaxZ - 1)
                .OrderByDescending(c => (c.MaxX - c.MinX) * (c.MaxZ - c.MinZ))
                .ThenByDescending(c => c.EdgeDistance)
                .ToList();
            if (legal.Count == 0) continue;   // the room cannot yield this much — try the smaller cube

            (minX, minZ, maxX, maxZ) = (legal[0].MinX, legal[0].MinZ, legal[0].MaxX, legal[0].MaxZ);
            return new IronResolution(ironX, ironZ, cubeMinX, cubeMinZ, size, Placeable: true);
        }
        return new IronResolution(ironX, ironZ, 0, 0, 0, Placeable: false);
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
        var alongX = door.Edge.AlongX();
        var (alongLo, alongHi) = alongX ? (frame.InteriorMinX, frame.InteriorMaxX) : (frame.InteriorMinZ, frame.InteriorMaxZ);
        // The two interior rows the door's own axis runs between, then which of them the door stands on.
        var (lowRow, highRow) = alongX
            ? (frame.InteriorMinZ, frame.InteriorMaxZ - 1)
            : (frame.InteriorMinX, frame.InteriorMaxX - 1);
        var (near, far) = door.Edge.Positive() ? (highRow, lowRow) : (lowRow, highRow);
        var (nearWall, farWall) = (door.Edge, door.Edge.Opposite());
        MonumentSlot Seat(int along, int crossAxis, RoomEdge wall) =>
            alongX ? new MonumentSlot(along, crossAxis, wall) : new MonumentSlot(crossAxis, along, wall);
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

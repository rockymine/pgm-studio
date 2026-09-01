using PgmStudio.Geom;

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

    /// <summary>The footprint cannot hold a room of the least legal span. A wall is what the interior is
    /// inset by and what the pad keeps its clearance to, so the span a shell needs is four blocks more on
    /// each axis than the span a pad and its chest corners need on open ground.</summary>
    /// <remarks>Enlarge the footprint, or take the shell off it. A room is a pad with a ring of floor round it; a shell adds its two courses of wall and the clearance the pad keeps to them.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Structure)]
    public const string FootprintTooSmall = "WX2";

    /// <summary>The footprint reaches outside the piece it stands on. A piece is one rectangle at one
    /// surface, so a footprint inside it is on ground by construction and crosses no interface; one that
    /// reaches past it is over whatever the neighbour happens to be, or over the void.</summary>
    /// <remarks>Draw the footprint back inside its piece, or enlarge the piece under it. The piece is the ground the room stands on and the region that protects it; the footprint is the building raised on that ground.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Structure)]
    public const string FootprintOffPiece = "WX12";

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

    /// <summary>An iron cube is <see cref="RoomFrames.IronSpan"/> blocks square and stands outside the room
    /// shell, inside the piece, holding <see cref="RoomFrames.IronGap"/> blocks of clear air to the wall — the standing room a
    /// player has to get round it. It fits where its marker puts it or it does not: the room keeps the
    /// footprint <c>WX1</c> gave it and never yields an edge, and the cube is one size whatever the marker's
    /// parity. A marker with no room for its cube resolves unplaceable (<c>WX9</c>).</summary>
    /// <remarks>Move the iron marker further from the shell, or enlarge the spawn piece — the cube needs its own
    /// footprint plus its clear air in the ring between the shell and the piece edge. Shrinking the room's own
    /// footprint is the other way to make the ring wider, and is the author's to state rather than the
    /// resolver's to take.</remarks>
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

/// <summary>A room resolution: the <see cref="Frame"/> plus the piece's <see cref="Iron"/> placements, one
/// per marker in input order.</summary>
public sealed record ResolvedRoom(RoomFrame Frame, IReadOnlyList<IronResolution> Iron);

/// <summary>
/// The resolved geometry of one stamped room, in absolute world blocks, all rects min-inclusive /
/// max-exclusive: the shell footprint (the walls stand on its perimeter), the interior floor inside them,
/// the spawn/wool <see cref="Pad"/>, and the <see cref="Doors"/>. Derived once per room by
/// <see cref="RoomFrames.Resolve"/> and consumed by the stampers, the structure preview, and the exported
/// XML alike, so none of them can disagree about where the room is.
///
/// <para><see cref="Wall"/> is how thick the shell's walls are, and 0 where no building stands — either
/// because none is bound or because the footprint is too small to carry one. The pad, the chests and the
/// monuments are what a room is and sit on the footprint whichever it is, so every stamper reads this rather
/// than asking again whether a style was bound.</para>
/// </summary>
public sealed record RoomFrame(
    int MinX, int MinZ, int MaxX, int MaxZ,
    RoomPad Pad,
    IReadOnlyList<RoomDoor> Doors,
    int Wall = 1)
{
    public int Width => MaxX - MinX;
    public int Depth => MaxZ - MinZ;

    /// <summary>The interior floor — the footprint inside the walls, which is the whole footprint where no
    /// shell stands over it (<see cref="Wall"/> 0). It is the row the chests and the monuments seat in.</summary>
    public int InteriorMinX => MinX + Wall;
    public int InteriorMinZ => MinZ + Wall;
    public int InteriorMaxX => MaxX - Wall;
    public int InteriorMaxZ => MaxZ - Wall;
}

/// <summary>
/// The room-frame rules (docs/world-export/structures.md, WX1–WX7): how a wool-room or spawn piece, its
/// marker, and its entry interfaces resolve into the shell the export stamps. Pure block geometry with no
/// world access, so the plan validator refuses with the same rules the stampers build by.
/// </summary>
public static class RoomFrames
{
    /// <summary>The pad a room is built around — where a player arrives, and what every other span is
    /// measured out from.</summary>
    public const int PadSpan = 2;

    /// <summary>The smallest room there is (WX2): a <see cref="PadSpan"/>-square pad and the block of clear
    /// floor it keeps on every side, which is also the ring its four chest corners seat in. It is what a room
    /// needs whether or not a building stands over it — the contents are the same either way and the walls
    /// are the whole difference. Spans are in blocks, never cells.</summary>
    public const int MinRoomSpan = PadSpan + 2 * PadWallClearance;

    /// <summary>Clear floor kept between the pad and every wall (WX4).</summary>
    public const int PadWallClearance = 1;

    /// <summary>What a wall costs a footprint on each axis: the one course it stands in, on both sides.
    /// <see cref="MinRoomSpan"/> already carries the pad and the clearance it keeps, so a shell adds only the
    /// courses — which is the whole difference between the two minimums (WX2).</summary>
    public const int WallCost = 2;

    /// <summary>The smallest footprint a room may take (WX2): <see cref="MinRoomSpan"/> on open ground, and
    /// that plus what a wall costs where a shell stands over it — <b>6×6</b>, a 4×4 interior that still seats
    /// four corner monuments, the chest stacks and a pad.</summary>
    public static int MinSpan(bool walled) => MinRoomSpan + (walled ? WallCost : 0);

    /// <summary>Whether a footprint is too small to hold a room (WX2).</summary>
    public static bool FootprintTooSmall(int width, int depth, bool walled) =>
        width < MinSpan(walled) || depth < MinSpan(walled);

    /// <summary>The clean floor a piece keeps between its edge and the room it carries, on every side but
    /// the one the door opens through (WX1).</summary>
    public const int DefaultGap = 1;

    /// <summary>The clean floor kept in front of the door (WX1): the iron cube plus the standing room it
    /// holds to the wall, so a spawn opens with somewhere for its iron to stand rather than with a shell that
    /// has to shrink to make room.</summary>
    public const int DefaultDoorGap = IronSpan + IronGap;

    /// <summary>The footprint a piece carries where none is stated (WX1): the piece inset by
    /// <see cref="DefaultGap"/> on every side, and by up to <see cref="DefaultDoorGap"/> on the side the door
    /// opens through. A 20×20 spawn piece facing −z opens as an 18×14 room with five blocks of ground in
    /// front of its door — a cube and the standing room it keeps, without the room giving up an edge for
    /// it.
    ///
    /// <para><b>The door's gap yields to the marker.</b> A marker is where a player arrives and the pad is
    /// derived from it, so a default that pushed the room off its own marker would move the spawn point
    /// (<c>WX4</c> would clamp the pad) on a board nobody had touched. The gap therefore takes only what
    /// leaves the marker seated where it already sat, down to the clean ring every side keeps. A piece too
    /// small to give the door its ground still gives the room its ring, and a footprint under the minimum is
    /// <c>WX2</c>'s to report about the room rather than about a default.</para></summary>
    public static BlockRect DefaultFootprint(
        BlockRect piece, RoomEdge? doorEdge, double markerX, double markerZ, bool walled)
    {
        BlockRect With(int doorGap)
        {
            int Gap(RoomEdge side) => doorEdge == side ? doorGap : DefaultGap;
            return new BlockRect(
                piece.MinX + Gap(RoomEdge.NegX), piece.MinZ + Gap(RoomEdge.NegZ),
                piece.MaxX - Gap(RoomEdge.PosX), piece.MaxZ - Gap(RoomEdge.PosZ));
        }
        for (var gap = DefaultDoorGap; gap > DefaultGap; gap--)
        {
            var candidate = With(gap);
            if (!FootprintTooSmall(candidate.Width, candidate.Depth, walled)
                && Seats(candidate, markerX, markerZ, walled)) return candidate;
        }
        return With(DefaultGap);
    }

    /// <summary>Whether a footprint holds the pad its marker asks for without clamping it (WX4) — what the
    /// door's gap is held to, so a default never moves a spawn point.</summary>
    private static bool Seats(BlockRect footprint, double markerX, double markerZ, bool walled)
    {
        var inset = (walled ? 1 : 0) * (1 + PadWallClearance);
        var pad = PlacePad(markerX, markerZ, footprint.MinX + inset, footprint.MinZ + inset,
            footprint.MaxX - inset, footprint.MaxZ - inset);
        return pad is { Shifted: false };
    }

    /// <summary>Whether a marker's block-lattice parity differs between axes (WX3). The pad is always
    /// square, so a grid-line x with a block-centre z has no pad and refuses at validation.</summary>
    public static bool MixedParity(double markerX, double markerZ) =>
        IsGridLine(markerX) != IsGridLine(markerZ);

    /// <summary>The nearest offset whose two axes share a parity (WX3). A marker centred in a room that is
    /// odd across one axis only lands mixed, and the pad is always square, so the block-centre axis moves
    /// half a block onto the grid line below it. Offsets are piece-relative and never negative, which is what
    /// the floor holds.</summary>
    public static (double X, double Z) SameParity(double markerX, double markerZ)
    {
        if (!MixedParity(markerX, markerZ)) return (markerX, markerZ);
        return IsGridLine(markerX)
            ? (markerX, Math.Max(0, markerZ - 0.5))
            : (Math.Max(0, markerX - 0.5), markerZ);
    }

    /// <summary>The door width for a wall whose interior runs <paramref name="interiorAcross"/> blocks along
    /// it (WX7): an odd wall centres a 3-wide door; an even wall takes the common 4 once the interior is 6
    /// across, narrowing to 2 at the 4-across minimum. Always ≤ interior − 2, so the door-wall corner cells
    /// are never exposed from outside.</summary>
    public static int DoorWidth(int interiorAcross) =>
        interiorAcross % 2 == 1 ? 3 : interiorAcross >= 6 ? 4 : 2;

    /// <inheritdoc cref="ResolveRoom"/>
    /// <remarks>The frame-only convenience: no iron markers, returns just the frame.</remarks>
    public static RoomFrame? Resolve(
        BlockRect piece, BlockRect? footprint, bool shellBound,
        double markerX, double markerZ,
        IReadOnlyList<(double MinX, double MinZ, double MaxX, double MaxZ)> entries,
        RoomEdge? spawnDoorEdge,
        out Finding? refusal)
        => ResolveRoom(piece, footprint, shellBound, markerX, markerZ,
            entries, spawnDoorEdge, [], out refusal)?.Frame;

    /// <summary>
    /// Resolve a room from its piece rect, its marker, its entry interfaces, and the piece's iron markers
    /// (WX1–WX9). <paramref name="entries"/> are degenerate rects on the piece boundary (a seam or
    /// build-zone interface segment; zero-thickness on the seam axis); pass a
    /// <paramref name="spawnDoorEdge"/> instead for a spawn room's single yaw-derived door.
    /// <paramref name="ironMarkers"/> resolve to cubes standing clear of the shell in the ring around it
    /// (WX8), or to unplaceable markers (WX9). Null with a <paramref name="refusal"/> naming the
    /// <see cref="RoomFrameRules"/> id that refused — the same finding the validator reports.
    ///
    /// <para>This answers a room <em>or</em> a refusal rather than a <see cref="Findings"/> list, and that is
    /// the difference between a resolve and a gate: a gate reads a document and collects everything wrong with
    /// it, while a resolve is producing a value and stops at the first thing that makes producing it
    /// impossible. There is no second WX fault to report once the footprint is too small to hold a room.</para>
    /// </summary>
    /// <param name="piece">The ground the room stands on: what bounds every marker, and what the footprint
    /// must lie inside.</param>
    /// <param name="footprint">The room itself, or null for <see cref="DefaultFootprint"/> — the piece inset
    /// a block, and further in front of the door so the iron has ground to stand on (WX1).</param>
    /// <param name="shellBound">Whether a room style is bound, so a shell stands on the footprint's perimeter
    /// where one fits. A wall is what the interior is inset by, so a room on open ground has none and takes
    /// the whole footprint; a bound shell that will not fit leaves the same open room, and the resolved
    /// frame's <see cref="RoomFrame.Wall"/> is what says which happened.</param>
    /// <param name="markerX">The spawn or wool point's x, in absolute blocks — where the pad centres (WX3–WX5).</param>
    /// <param name="markerZ">The same point's z.</param>
    /// <param name="entries">Degenerate rects on the piece boundary, one door cut per distinct edge (WX6).</param>
    /// <param name="spawnDoorEdge">A spawn room's single yaw-derived door, in place of <paramref name="entries"/>.</param>
    /// <param name="ironMarkers">The piece's iron markers, resolved in input order (WX8/WX9).</param>
    /// <param name="refusal">The <see cref="RoomFrameRules"/> finding that refused, where the result is null.</param>
    public static ResolvedRoom? ResolveRoom(
        BlockRect piece, BlockRect? footprint, bool shellBound,
        double markerX, double markerZ,
        IReadOnlyList<(double MinX, double MinZ, double MaxX, double MaxZ)> entries,
        RoomEdge? spawnDoorEdge,
        IReadOnlyList<(double X, double Z)> ironMarkers,
        out Finding? refusal)
    {
        refusal = null;
        int pieceMinX = piece.MinX, pieceMinZ = piece.MinZ, pieceMaxX = piece.MaxX, pieceMaxZ = piece.MaxZ;
        var room = footprint ?? DefaultFootprint(piece, spawnDoorEdge, markerX, markerZ, shellBound);
        int minX = room.MinX, minZ = room.MinZ, maxX = room.MaxX, maxZ = room.MaxZ;
        // A shell stands where one is bound and the footprint can carry it. A room too small for walls is not
        // a refusal: its pad, chests and monuments are what a room is, and they need the same floor either
        // way — so the building is simply not there and the rest is. WX2 therefore refuses one span only, the
        // room's own, and a bound shell that could not stand is the caller's to report (it reads Wall).
        var wall = shellBound && !FootprintTooSmall(maxX - minX, maxZ - minZ, walled: true) ? 1 : 0;

        if (FootprintTooSmall(maxX - minX, maxZ - minZ, walled: false))
        {
            refusal = new Finding(RoomFrameRules.FootprintTooSmall,
                $"footprint {maxX - minX}×{maxZ - minZ} is too small to hold a room: the least span is "
                + $"{MinRoomSpan}×{MinRoomSpan} blocks — a {PadSpan}×{PadSpan} pad and the block of clear "
                + $"floor it keeps on every side. A shell over it needs {MinSpan(walled: true)}×"
                + $"{MinSpan(walled: true)}, and simply does not stand on a footprint smaller than that");
            return null;
        }
        if (minX < pieceMinX || minZ < pieceMinZ || maxX > pieceMaxX || maxZ > pieceMaxZ)
        {
            refusal = new Finding(RoomFrameRules.FootprintOffPiece,
                $"footprint [{minX}, {minZ}]–[{maxX}, {maxZ}] reaches outside the piece it stands on "
                + $"([{pieceMinX}, {pieceMinZ}]–[{pieceMaxX}, {pieceMaxZ}])");
            return null;
        }
        if (MixedParity(markerX, markerZ))
        {
            refusal = new Finding(RoomFrameRules.MarkerParity,
                "marker parity differs between axes; the pad is always square — place the marker on a "
                + "block grid line, or at a block centre, in both axes");
            return null;
        }

        // WX8 — each iron marker in turn: the cube stands in the ring between the footprint and the piece
        // edge, the standing room of IronGap to the shell, never fused. The room has priority and keeps the
        // footprint it was given; a marker with no room for its cube resolves unplaceable (WX9).
        var iron = new List<IronResolution>();
        foreach (var (ironX, ironZ) in ironMarkers)
            iron.Add(PlaceIron(ironX, ironZ, piece, minX, minZ, maxX, maxZ));

        // The pad's allowed region is the interior inset by the wall clearance (WX4) — the whole footprint
        // where no wall stands, since there is nothing to clear.
        var padInset = wall * (1 + PadWallClearance);
        var pad = PlacePad(markerX, markerZ,
            minX + padInset, minZ + padInset, maxX - padInset, maxZ - padInset);
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
            var interiorAcross = (alongX ? maxX - minX : maxZ - minZ) - 2 * wall;
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
                var interiorAcross = (alongX ? maxX - minX : maxZ - minZ) - 2 * wall;
                var width = DoorWidth(interiorAcross);
                // Centre the door on the entry interval, clamped onto the wall run between the corners.
                var (runLo, runHi) = alongX ? (minX + wall, maxX - wall) : (minZ + wall, maxZ - wall);
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

        return new ResolvedRoom(new RoomFrame(minX, minZ, maxX, maxZ, pad.Value, doors, wall), iron);
    }

    /// <summary>The least air a cube keeps between itself and the room shell (WX8): the standing room a
    /// player has to get round it, so it reads as a thing in the yard rather than as part of the wall. A
    /// minimum, not a spacing — a long piece carrying a small house leaves the cube further out, and the
    /// author moves it there.</summary>
    public const int IronGap = 2;

    /// <summary>The side of an iron cube (WX8). One size, whatever the marker's parity: a cube that changed
    /// size under the marker was a second thing to reason about at every seat, and the author moves the
    /// marker rather than reading a size off it.</summary>
    public const int IronSpan = 3;

    // Resolve one iron marker against the footprint: the cube centres on the marker, put back on the block
    // lattice, and stands where it lands. It fits or it does not — the room never gives an edge up for it,
    // and nothing walks a size ladder, so what the author sees on the board is what the export writes.
    // Rounding away from zero keeps a half-block landing symmetric: an orbit image of the cube covers the
    // images of its cells rather than a row one block off.
    private static IronResolution PlaceIron(
        double ironX, double ironZ, BlockRect piece, int minX, int minZ, int maxX, int maxZ)
    {
        int Lo(double marker) => (int)Math.Round(marker - IronSpan / 2.0, MidpointRounding.AwayFromZero);
        int cubeMinX = Lo(ironX), cubeMinZ = Lo(ironZ);
        int cubeMaxX = cubeMinX + IronSpan, cubeMaxZ = cubeMinZ + IronSpan;

        var onPiece = cubeMinX >= piece.MinX && cubeMinZ >= piece.MinZ
            && cubeMaxX <= piece.MaxX && cubeMaxZ <= piece.MaxZ;
        var clearOfShell = maxX <= cubeMinX - IronGap || minX >= cubeMaxX + IronGap
            || maxZ <= cubeMinZ - IronGap || minZ >= cubeMaxZ + IronGap;
        return onPiece && clearOfShell
            ? new IronResolution(ironX, ironZ, cubeMinX, cubeMinZ, IronSpan, Placeable: true)
            : new IronResolution(ironX, ironZ, 0, 0, 0, Placeable: false);
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

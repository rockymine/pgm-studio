using PgmStudio.Geom;

using PgmStudio.Vocabulary;

namespace PgmStudio.Domain;

/// <summary>A door opening cut into a room shell wall: the <see cref="Edge"/> it sits on, the low
/// along-axis block coordinate <see cref="Lo"/> (x for a Z edge, z for an X edge), and its
/// <see cref="Width"/> in blocks.</summary>
public readonly record struct RoomDoor(RoomEdge Edge, int Lo, int Width);

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
    /// <summary>A room with no stated footprint takes its piece inset by 1 block on every side, and by up to 5
    /// blocks on each side a door opens through.</summary>
    /// <remarks>Set the <c>footprint</c> of the spawn or wool in <c>placements.spawns</c> or
    /// <c>placements.wools</c> to the room's rectangle.</remarks>
    [Rule(RuleConcern.Plan, RuleConcern.Structure)]
    public const string ShellFootprint = "WX1";

    /// <summary>A room's footprint is less than 4 blocks across its shorter side.</summary>
    /// <remarks>Change the <c>footprint</c> of the spawn or wool in <c>placements.spawns</c> or
    /// <c>placements.wools</c> until its shorter side is at least 4 blocks.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Structure)]
    public const string FootprintTooSmall = "WX2";

    /// <summary>A room's footprint is not inside its room piece.</summary>
    /// <remarks>Either set the <c>footprint</c> of the spawn or wool in <c>placements.spawns</c> or
    /// <c>placements.wools</c> to a rectangle inside its piece, or widen the <c>rect</c> of the piece in
    /// <c>pieces</c>.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Structure)]
    public const string FootprintOffPiece = "WX12";

    /// <summary>A room's footprint is more than 20 blocks across.</summary>
    /// <remarks>Either set the <c>footprint</c> of the spawn or wool in <c>placements.spawns</c> or
    /// <c>placements.wools</c> to at most 20 by 20 blocks, or shrink the <c>rect</c> of the room piece in
    /// <c>pieces</c>.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Structure, RuleConcern.Spawn)]
    public const string RoomIsAField = "WX13";

    /// <summary>A spawn room or wool room has no house of its own.</summary>
    /// <remarks>Either set the <c>roomStyles.wool</c> or <c>roomStyles.spawn</c> of the sketch to a house, or set
    /// it to <c>null</c> for open ground.</remarks>
    [Rule(RuleCategory.Forbidden, RuleConcern.Style, RuleConcern.Structure, RuleConcern.Spawn, RuleConcern.Objective)]
    public const string BuiltInShell = "WX14";

    /// <summary>A spawn or wool marker sits on a grid line along one axis, and at a block centre along the
    /// other.</summary>
    /// <remarks>Move the spawn or wool in <c>placements.spawns</c> or <c>placements.wools</c> half a block along
    /// one axis.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Structure, RuleConcern.Spawn)]
    public const string MarkerParity = "WX3";

    /// <summary>A pad is less than 1 block from a wall of its room, or does not fit between the walls.</summary>
    /// <remarks>Either move the spawn or wool in <c>placements.spawns</c> or <c>placements.wools</c> until its pad
    /// is at least 1 block from every wall, or set its <c>footprint</c> to a larger room.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Structure, RuleConcern.Spawn)]
    public const string PadClearance = "WX4";

    /// <summary>The exported location of a spawn or wool is the centre of its pad, and not the position of its
    /// marker.</summary>
    /// <remarks>Move the spawn or wool in <c>placements.spawns</c> or <c>placements.wools</c> until the centre of
    /// its pad is where the exported location should be.</remarks>
    [Rule(RuleConcern.Structure, RuleConcern.Spawn, RuleConcern.Objective, RuleConcern.World)]
    public const string PadIsPoint = "WX5";

    /// <summary>A wool's room piece has no shared edge with another piece or a build region.</summary>
    /// <remarks>Either move the <c>rect</c> of the wool's room piece in <c>pieces</c> until it has a shared edge
    /// with another piece, or add a build region to <c>zones</c> along its edge.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Plan, RuleConcern.Structure, RuleConcern.Objective)]
    public const string RoomUnreachable = "WX6";

    /// <summary>A door is 3 blocks wide on an interior wall of odd length, and 4 on an even wall at least 6 across,
    /// or 2 on an even wall 4 across.</summary>
    /// <remarks>Change the <c>footprint</c> of the spawn or wool in <c>placements.spawns</c> or
    /// <c>placements.wools</c> until the interior across the door wall is an even number of at least 6
    /// blocks.</remarks>
    [Rule(RuleConcern.Structure)]
    public const string DoorWidth = "WX7";

    /// <summary>An iron cube reaches outside its piece, or stands less than 2 blocks from the footprint of its
    /// room.</summary>
    /// <remarks>Either move the iron in <c>placements.iron</c> until its cube is inside the piece and at least 2
    /// blocks from the room, or widen the <c>rect</c> of the piece in <c>pieces</c>.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan, RuleConcern.Structure, RuleConcern.Spawn)]
    public const string IronFit = "WX8";

    /// <summary>An iron marker whose cube does not fit stamps nothing, and its room keeps its full
    /// footprint.</summary>
    /// <remarks>Either move the iron in <c>placements.iron</c> until its cube fits, or delete it from
    /// <c>placements.iron</c>.</remarks>
    [Rule(RuleConcern.Structure, RuleConcern.World)]
    public const string MarkerPlaceability = "WX9";

    /// <summary>A house bound to a room is more than 20 blocks tall on a 6 by 6 footprint.</summary>
    /// <remarks>Either delete a storey from <c>storeys</c>, or change the <c>roof.pitch</c> of the house, until the
    /// house is at most 20 blocks tall.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Style, RuleConcern.Structure, RuleConcern.Objective)]
    public const string ShellOverCeiling = "WX10";

    /// <summary>A spawn room or wool room touches ground 2 or more blocks below its floor.</summary>
    /// <remarks>Either level the ground beside the room with terraform until it is within 1 block of the floor, or
    /// move the <c>rect</c> of the room's piece in <c>pieces</c> onto level ground.</remarks>
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
///
/// <para><see cref="Reflected"/> is whether this room is a mirror image of the one the author placed. Every
/// choice a wall cannot centre — the spare block of a window row, the ladder's end, a narrowed door, the porch
/// posts, the order of the monument seats — is taken from a hand (<see cref="RoomEdges.Handed"/>), and a
/// reflection swaps the hands, so the stampers read it here to lay the image out as the mirror of the
/// original. The resolver cannot derive it: it is a fact about the orbit image, set by whoever fanned it.</para>
/// </summary>
public sealed record RoomFrame(
    int MinX, int MinZ, int MaxX, int MaxZ,
    SpawnPad Pad,
    IReadOnlyList<RoomDoor> Doors,
    int Wall = 1,
    bool Reflected = false)
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

    /// <summary>The least span <b>any</b> building footprint may be, in blocks (the author's number, WX2/HP2):
    /// two blocks of something with a block on each side of it. For a room that is a <see cref="PadSpan"/>
    /// pad and the clear floor it keeps, which is also the ring its four chest corners seat in; for a dressed
    /// wing it is two walls and an inside. One number rather than two, because a room's footprint is the
    /// single-wing case of a building's and being played through does not make it want more floor.
    ///
    /// <para>It is what a room needs whether or not a building stands over it — the contents are the same
    /// either way and the walls are the whole difference. Spans are in blocks, never cells.</para></summary>
    public const int MinFootprintSpan = 4;

    /// <summary>The largest building a role piece may raise, in blocks square (the author's number). It is a
    /// hall a player crosses; past it the room is a field with a roof. The other end of
    /// <see cref="MinFootprintSpan"/>, and it lives here for the same reason: the frame is resolved in this
    /// project, and a cap stated where only the plan can reach it is a cap a hand-authored intent never
    /// meets.</summary>
    public const int FootprintCap = 20;

    /// <summary>The largest protection region a role piece may be, in blocks, across its short axis and along
    /// its long one. A region is the ground and the immunity together, so the long axis affords a room its
    /// approach without handing a team a field it cannot be fought in.</summary>
    public const int RegionCapAcross = 20, RegionCapAlong = 30;

    /// <summary>Clear floor kept between the pad and every wall (WX4).</summary>
    public const int PadWallClearance = 1;

    /// <summary>What a wall costs a footprint on each axis: the one course it stands in, on both sides.
    /// <see cref="MinFootprintSpan"/> already carries the pad and the clearance it keeps, so a shell adds only
    /// the courses — which is the whole difference between the two minimums (WX2).</summary>
    public const int WallCost = 2;

    /// <summary>The smallest footprint a room may take (WX2): <see cref="MinFootprintSpan"/> on open ground,
    /// and that plus what a wall costs where a shell stands over it — <b>6×6</b>, a 4×4 interior that still
    /// seats four corner monuments, the chest stacks and a pad.</summary>
    public static int MinSpan(bool walled) => MinFootprintSpan + (walled ? WallCost : 0);

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
    /// <see cref="DefaultGap"/> on every side, and by up to <see cref="DefaultDoorGap"/> on each side a door
    /// opens through. A 20×20 spawn piece with one door on −z opens as an 18×14 room with five blocks of
    /// ground in front of it — a cube and the standing room it keeps, without the room giving up an edge for
    /// it; a corner spawn opening on two sides keeps that ground in front of both.
    ///
    /// <para><b>The door's gap yields to the marker.</b> A marker is where a player arrives and the pad is
    /// derived from it, so a default that pushed the room off its own marker would move the spawn point
    /// (<c>WX4</c> would clamp the pad) on a board nobody had touched. The gap therefore takes only what
    /// leaves the marker seated where it already sat, down to the clean ring every side keeps. A piece too
    /// small to give the door its ground still gives the room its ring, and a footprint under the minimum is
    /// <c>WX2</c>'s to report about the room rather than about a default.</para></summary>
    public static BlockRect DefaultFootprint(
        BlockRect piece, IReadOnlyList<RoomEdge> doorEdges, double markerX, double markerZ, bool walled)
    {
        BlockRect With(int doorGap)
        {
            int Gap(RoomEdge side) => doorEdges.Contains(side) ? doorGap : DefaultGap;
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
        var pad = SpawnPad.Fit(markerX, markerZ, new BlockRect(
            footprint.MinX + inset, footprint.MinZ + inset, footprint.MaxX - inset, footprint.MaxZ - inset),
            PadUse.Standing);
        return pad is { Shifted: false };
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
        IReadOnlyList<RoomEdge> spawnDoorEdges,
        out Finding? refusal)
        => ResolveRoom(piece, footprint, shellBound, markerX, markerZ,
            entries, spawnDoorEdges, [], out refusal)?.Frame;

    /// <summary>
    /// Resolve a room from its piece rect, its marker, its entry interfaces, and the piece's iron markers
    /// (WX1–WX9). <paramref name="entries"/> are degenerate rects on the piece boundary (a seam or
    /// build-zone interface segment; zero-thickness on the seam axis); pass
    /// <paramref name="spawnDoorEdges"/> instead for a spawn room, whose doors are named as whole walls
    /// rather than cut from a segment.
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
    /// <param name="spawnDoorEdges">A spawn room's doors, one per named wall, in place of
    /// <paramref name="entries"/>. Empty leaves the room to <paramref name="entries"/> as a wool cage does.</param>
    /// <param name="ironMarkers">The piece's iron markers, resolved in input order (WX8/WX9).</param>
    /// <param name="refusal">The <see cref="RoomFrameRules"/> finding that refused, where the result is null.</param>
    public static ResolvedRoom? ResolveRoom(
        BlockRect piece, BlockRect? footprint, bool shellBound,
        double markerX, double markerZ,
        IReadOnlyList<(double MinX, double MinZ, double MaxX, double MaxZ)> entries,
        IReadOnlyList<RoomEdge> spawnDoorEdges,
        IReadOnlyList<(double X, double Z)> ironMarkers,
        out Finding? refusal)
    {
        refusal = null;
        int pieceMinX = piece.MinX, pieceMinZ = piece.MinZ, pieceMaxX = piece.MaxX, pieceMaxZ = piece.MaxZ;
        var room = footprint ?? DefaultFootprint(piece, spawnDoorEdges, markerX, markerZ, shellBound);
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
                + $"{MinFootprintSpan}×{MinFootprintSpan} blocks — a {PadSpan}×{PadSpan} pad and the block of clear "
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
        if (SpawnPad.MixedParity(markerX, markerZ))
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
            iron.Add(PlaceIron(ironX, ironZ, piece, new BlockRect(minX, minZ, maxX, maxZ)));

        // The pad's allowed region is the interior inset by the wall clearance (WX4) — the whole footprint
        // where no wall stands, since there is nothing to clear.
        var padInset = wall * (1 + PadWallClearance);
        var pad = SpawnPad.Fit(markerX, markerZ, new BlockRect(
            minX + padInset, minZ + padInset, maxX - padInset, maxZ - padInset), PadUse.Standing);
        if (pad is null)
        {
            refusal = new Finding(RoomFrameRules.PadClearance,
                "no room for the spawn/wool pad inside the interior");
            return null;
        }

        List<RoomDoor> doors;
        if (spawnDoorEdges.Count > 0)
        {
            // A named wall centres its door on itself: the wall is the whole opening's context, where a wool
            // cage's is the segment its neighbour abuts along.
            doors = [];
            foreach (var doorEdge in spawnDoorEdges.Distinct())
            {
                var alongX = doorEdge.AlongX();
                var interiorAcross = (alongX ? maxX - minX : maxZ - minZ) - 2 * wall;
                var width = DoorWidth(interiorAcross);
                var lo = alongX
                    ? minX + (maxX - minX - width) / 2
                    : minZ + (maxZ - minZ - width) / 2;
                doors.Add(new RoomDoor(doorEdge, lo, width));
            }
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

    /// <summary>Courses of bedrock an approach wall stands above the ground it bars (ST4). Three, plus the
    /// cobweb course the stamper caps it with — tall enough to stop a player walking or jumping the line,
    /// short enough that both halves of the lane still read as one place.
    /// <para>It sits here rather than beside either user because both the plan compiler, which answers the
    /// wall's height before a world exists, and the stamper, which lays it over the ground the relief
    /// solved, measure from it — and a second <c>const</c> aliasing one that exists is two rules.</para>
    /// </summary>
    public const int WallCourses = 3;

    /// <summary>The tallest an approach wall may stand over the ground at any column of its run before the
    /// export complains (ST4). A wall is <see cref="WallCourses"/> proud of the highest ground it crosses and
    /// its top is level, so ground that falls away along the seam leaves it taller at the low end; past this
    /// it stops reading as a line to hold and becomes a blank face.</summary>
    public const int WallCoursesMax = 4;

    /// <summary>Resolve one iron marker into the cube it stamps (WX8), or an unplaceable marker (WX9). The
    /// cube centres on the marker, put back on the block lattice, and stands where it lands: it fits inside
    /// the piece and clear of the room, or it does not. The room never gives an edge up for it and nothing
    /// walks a size ladder, so what the author sees on the board is what the export writes.
    ///
    /// <para>Every iron marker on the board resolves here, and a marker on a piece carrying no room passes
    /// <paramref name="room"/> as null: there is no shell to stand clear of, and the piece is the whole
    /// test. One resolver, so a cube cannot be inside its bounds on one path and outside them on the
    /// other.</para></summary>
    /// <param name="ironX">The marker's x, in absolute blocks — the cube centres on it.</param>
    /// <param name="ironZ">The same marker's z.</param>
    /// <param name="piece">The ground the marker rides — what the cube must lie inside.</param>
    /// <param name="room">The room's footprint, or null where the piece carries none.</param>
    public static IronResolution PlaceIron(double ironX, double ironZ, BlockRect piece, BlockRect? room = null)
    {
        // Rounding away from zero keeps a half-block landing symmetric: an orbit image of the cube covers the
        // images of its cells rather than a row one block off.
        int Lo(double marker) => (int)Math.Round(marker - IronSpan / 2.0, MidpointRounding.AwayFromZero);
        int cubeMinX = Lo(ironX), cubeMinZ = Lo(ironZ);
        int cubeMaxX = cubeMinX + IronSpan, cubeMaxZ = cubeMinZ + IronSpan;

        var onPiece = cubeMinX >= piece.MinX && cubeMinZ >= piece.MinZ
            && cubeMaxX <= piece.MaxX && cubeMaxZ <= piece.MaxZ;
        var clearOfRoom = room is not { } shell
            || shell.MaxX <= cubeMinX - IronGap || shell.MinX >= cubeMaxX + IronGap
            || shell.MaxZ <= cubeMinZ - IronGap || shell.MinZ >= cubeMaxZ + IronGap;
        return onPiece && clearOfRoom
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
    /// the cells directly inside the door opening. Each row runs from the left hand of someone standing in
    /// the door looking out, so the n-th seat of a room and of its orbit image are images of each other —
    /// under a reflection too, since a <see cref="RoomFrame.Reflected"/> frame counts from the other hand.
    /// The list's length is the room's monument capacity; the caller takes the first N.
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
        int Hand(int fromLeft) => door.Edge.Handed(frame.Reflected, alongLo, alongHi - 1, fromLeft);

        var slots = new List<MonumentSlot>
        {
            Seat(Hand(alongLo), near, nearWall), Seat(Hand(alongHi - 1), near, nearWall),
            Seat(Hand(alongLo), far, farWall), Seat(Hand(alongHi - 1), far, farWall),
        };
        for (var along = alongLo + 1; along < alongHi - 1; along++) slots.Add(Seat(Hand(along), far, farWall));
        for (var along = alongLo + 1; along < alongHi - 1; along++)
            if (!InDoorSpan(Hand(along))) slots.Add(Seat(Hand(along), near, nearWall));
        return slots;
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

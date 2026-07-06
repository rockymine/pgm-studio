using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// Turns each objective's terminal lane into a real role-bearing ROOM the lane docks to: a compact
/// <see cref="PlanRoles.WoolRoom"/> / <see cref="PlanRoles.Spawn"/> piece carved off the marker's dead-end,
/// with the approach-lane remnant kept as an anonymous <see cref="PlanRoles.Piece"/> in front of it. Without
/// this a generated wool is a marker on a plain piece (no red entrance seam / bedrock floor at export, ST1)
/// and a generated spawn a marker on a plain piece (no auto-renewed iron, ST2) — the role-specific
/// <see cref="PlanCompiler"/> paths never fire. Modelling the room as its own piece is also the dock the
/// spawn/wool-lane grammar attaches to.
///
/// <para>The room is carved at the marker's end of the run (WL1: the far/back end of the dead-end lane) and
/// is two cells deep — a 2×2 / 3×2-cell (≥10×10-block) plateau that covers the export stamp (WL3). The marker
/// keeps its exact world position, re-hosted onto the room, so wool↔spawn (WL2) and wool↔wool (WL7)
/// distances are unchanged. A terminal too short to leave a ≥2-cell approach remnant — or one already
/// isolated behind a bridge (the WL4 isolated wool / SP6 isolated spawn) — becomes the room whole. Every
/// transform is geometrically neutral: room ∪ remnant covers exactly the terminal's cells, so separation,
/// the LN2 chain caps, the land budget and marker distances all still hold.</para>
/// </summary>
public static class SpawnWoolRooms
{
    // WL3: a two-cell (10-block) deep plateau clears the 8×8 export stamp with the lane-width cross span.
    private const int RoomDepthCells = 2;

    /// <summary>Carve a <c>spawn</c> room and one <c>wool-room</c> per wool from <paramref name="unit"/>'s
    /// terminal lanes, returning the unit with the rooms added/relabelled and the placements re-hosted.</summary>
    public static GrownUnit Carve(GrownUnit unit)
    {
        var pieces = unit.Pieces.ToList();

        var (spawnId, spawnAt) = CarveRoom(pieces, unit.Spawn.Piece, unit.Spawn.At, PlanRoles.Spawn, "spawn");
        var spawn = new GrownSpawn(spawnId, spawnAt, unit.Spawn.Facing);

        var wools = new List<GrownWool>(unit.Wools.Count);
        for (var i = 0; i < unit.Wools.Count; i++)
        {
            var (id, at) = CarveRoom(
                pieces, unit.Wools[i].Piece, unit.Wools[i].At, PlanRoles.WoolRoom, $"wool-room-{(char)('a' + i)}");
            wools.Add(new GrownWool(id, at));
        }

        return new GrownUnit(pieces, spawn, wools);
    }

    // Split the marker end of `terminalId` off as the room, mutating `pieces`; return the room's id and the
    // marker's offset within it. When the terminal is a compact dead-end (no approach remnant would survive)
    // or an isolated island, the whole piece becomes the room (relabelled in place) and the marker is untouched.
    private static (string RoomId, double[] At) CarveRoom(
        List<GrownPiece> pieces, string terminalId, double[] at, string role, string roomId)
    {
        var idx = pieces.FindIndex(p => p.Id == terminalId);
        var terminal = pieces[idx];
        var r = terminal.Rect;
        int x = r[0], z = r[1], w = r[2], h = r[3];
        double markerX = x + at[0], markerZ = z + at[1];

        var hasApproach = pieces.Any(o => o.Id != terminalId && SharesEdge(r, o.Rect));
        var runX = w >= h;                                   // the run axis is the longer side
        var runLen = runX ? w : h;

        // whole-piece room: no approach lane to keep clear of, or too short to leave a ≥2-cell remnant
        if (!hasApproach || runLen - RoomDepthCells < 2)
        {
            pieces[idx] = terminal with { Id = roomId, Role = role };
            return (roomId, at);
        }

        int[] roomRect, laneRect;
        if (runX)
        {
            var farMax = markerX >= x + w / 2.0;             // which x-end the marker sits at
            if (farMax) { roomRect = [x + w - RoomDepthCells, z, RoomDepthCells, h]; laneRect = [x, z, w - RoomDepthCells, h]; }
            else        { roomRect = [x, z, RoomDepthCells, h]; laneRect = [x + RoomDepthCells, z, w - RoomDepthCells, h]; }
        }
        else
        {
            var farMax = markerZ >= z + h / 2.0;
            if (farMax) { roomRect = [x, z + h - RoomDepthCells, w, RoomDepthCells]; laneRect = [x, z, w, h - RoomDepthCells]; }
            else        { roomRect = [x, z, w, RoomDepthCells]; laneRect = [x, z + RoomDepthCells, w, h - RoomDepthCells]; }
        }

        // Shrinking the terminal to the remnant can, in rare adjacencies, degrade a full-corridor contact with
        // a neighbour into a bare corner or narrow seam (which the grower never authors). When it would, keep
        // the whole terminal as the room instead — it stays the validated footprint, just role-bearing.
        var others = pieces.Where(p => p.Id != terminalId).Select(p => p.Rect);
        if (others.All(o => CleanContact(roomRect, o) && CleanContact(laneRect, o)))
        {
            pieces[idx] = terminal with { Rect = laneRect }; // the approach lane keeps the terminal's id
            pieces.Add(new GrownPiece(roomId, roomRect, role));
            return (roomId, [markerX - roomRect[0], markerZ - roomRect[1]]);
        }

        pieces[idx] = terminal with { Id = roomId, Role = role };
        return (roomId, at);
    }

    // Two cell rects share a full edge (abut along one axis with a ≥2-cell — full-corridor — overlap on the other).
    private static bool SharesEdge(int[] a, int[] b)
    {
        var (ix, iz) = Overlaps(a, b);
        return (ix == 0 && iz >= 2) || (iz == 0 && ix >= 2);
    }

    // The two rects meet only as the grower authors contacts: a full-corridor land edge (≥2 cells) or disjoint —
    // never an overlap, bare corner, or narrow (&lt;2-cell) seam.
    private static bool CleanContact(int[] a, int[] b)
    {
        var (ix, iz) = Overlaps(a, b);
        if (ix < 0 || iz < 0) return true;                   // disjoint
        if (ix > 0 && iz > 0) return false;                  // area overlap
        return Math.Max(ix, iz) >= 2;                        // edge: full corridor, not a corner/narrow seam
    }

    private static (int Ix, int Iz) Overlaps(int[] a, int[] b) =>
        (Math.Min(a[0] + a[2], b[0] + b[2]) - Math.Max(a[0], b[0]),
         Math.Min(a[1] + a[3], b[1] + b[3]) - Math.Max(a[1], b[1]));
}

using PgmStudio.Geom;

namespace PgmStudio.Pgm.Shapes;

/// <summary>
/// The slot half of the emit↔derive mirror: given a classified <see cref="ShapeFamily"/> and its pieces (the
/// terminal named), re-derive each piece's <see cref="ApproachSlots"/> slot from <b>topology alone</b> — path
/// order for the chain families, adjacency for the branches, hole-edge geometry for the donut — and never
/// from a canonical rect position, so the emitter's placement manipulations (entry/wool shift, side-tuck,
/// donut attachment offset/width/count, room-at-end, flips, and any mouth reorientation) all survive. It is
/// the classifier-side counterpart of the emitter's own slot stamping (<c>GrownPiece.Slot</c>): the emitter
/// labels, this recovers, and asserting the two agree closes the mirror at the slot level, not just the
/// family level. See <c>docs/contracts/map-generation.md</c> §5.3/§5.4.
///
/// <para><b>Scope.</b> Like the family mirror, this reads the generator's own artifacts — a single approach's
/// emitted pieces, bounded by its box — never a finished map's welded terrain (§5.4). It takes the family as
/// given (from <see cref="ShapeClassifier"/>) and the terminal id (the room), the two facts the emitter
/// always knows.</para>
/// </summary>
public static class SlotAssignment
{
    /// <summary>Re-derive the slot of every piece in <paramref name="pieces"/> for the classified
    /// <paramref name="family"/>, with <paramref name="roomId"/> naming the terminal. Returns id → slot,
    /// with the terminal at <see cref="ApproachSlots.Room"/>.</summary>
    public static IReadOnlyDictionary<string, string> AssignSlots(
        ShapeFamily family, IReadOnlyList<(string Id, int[] Rect)> pieces, string roomId)
    {
        var slots = new Dictionary<string, string> { [roomId] = ApproachSlots.Room };
        var room = pieces.First(p => p.Id == roomId);
        var terrain = pieces.Where(p => p.Id != roomId).ToList();

        switch (family)
        {
            case ShapeFamily.I:                                  // entry · room — the lone terrain piece
            case ShapeFamily.Clamp:                              // entry · entry · room — both legs are entries
                foreach (var t in terrain) slots[t.Id] = ApproachSlots.Entry;
                break;

            case ShapeFamily.L:                                  // a simple path entry → … → room
            case ShapeFamily.Z:
            case ShapeFamily.Scythe:
                AssignChain(family, room, terrain, slots);
                break;

            case ShapeFamily.U:                                  // bar (room's neighbour) · two entry legs
            {
                var bar = terrain.First(t => EdgeAdjacent(t.Rect, room.Rect));
                slots[bar.Id] = ApproachSlots.Bar;
                foreach (var t in terrain.Where(t => t.Id != bar.Id)) slots[t.Id] = ApproachSlots.Entry;
                break;
            }

            case ShapeFamily.H:                                  // room · room-run · bar · two entry legs
            {
                var roomRun = terrain.First(t => EdgeAdjacent(t.Rect, room.Rect));
                var bar = terrain.First(t => t.Id != roomRun.Id && EdgeAdjacent(t.Rect, roomRun.Rect));
                slots[roomRun.Id] = ApproachSlots.RoomRun;
                slots[bar.Id] = ApproachSlots.Bar;
                foreach (var t in terrain.Where(t => t.Id != roomRun.Id && t.Id != bar.Id))
                    slots[t.Id] = ApproachSlots.Entry;
                break;
            }

            case ShapeFamily.Donut:
                AssignDonut(room, terrain, slots);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(family), family, "no slot template for this family");
        }
        return slots;
    }

    // I/L/Z/scythe are simple piece paths with the room at one leaf; walk from the room to the far leaf and
    // zip the visited pieces onto the family template read back-to-front (template ends at the room).
    private static void AssignChain(
        ShapeFamily family, (string Id, int[] Rect) room, List<(string Id, int[] Rect)> terrain,
        Dictionary<string, string> slots)
    {
        var template = ApproachSlots.Template(family);            // [entry, …, room]
        var order = new List<(string Id, int[] Rect)>();
        var visited = new HashSet<string> { room.Id };
        var cur = room;
        while (order.Count < terrain.Count)
        {
            var next = terrain.FirstOrDefault(t => !visited.Contains(t.Id) && EdgeAdjacent(cur.Rect, t.Rect));
            if (next.Id is null) break;                          // not a clean path — the mirror will flag it
            order.Add(next);
            visited.Add(next.Id);
            cur = next;
        }
        // order[0] is the room's neighbour (template[^2]); the last is the entry (template[0])
        for (var i = 0; i < order.Count; i++)
            slots[order[i].Id] = template[template.Count - 2 - i];
    }

    // The donut ring encloses a rectangular hole. Its four bordering pieces are two bars (on opposite hole
    // edges) and two legs (the perpendicular pair). The room hangs off one bar (room-bar, reached directly or
    // through a wool-extend run); the opposite bar is the entry-bar (the one a hub attachment docks); every
    // other terrain piece is a hub attachment (an entry). All of it is read off the hole edges and adjacency,
    // so it is orientation-free and survives the attachment/room-at-end/extend/flip manipulations.
    private static void AssignDonut(
        (string Id, int[] Rect) room, List<(string Id, int[] Rect)> terrain, Dictionary<string, string> slots)
    {
        var mask = new HashSet<(int, int)>();
        foreach (var p in terrain.Append(room)) foreach (var c in CellsOf(p.Rect)) mask.Add(c);
        var hole = Cells.EnclosedVoid(mask);
        var (hx0, hz0, hx1, hz1) = Cells.BoundingBox(hole);

        bool Borders(int[] r)
        {
            foreach (var (x, z) in CellsOf(r))
                if (hole.Contains((x - 1, z)) || hole.Contains((x + 1, z)) ||
                    hole.Contains((x, z - 1)) || hole.Contains((x, z + 1))) return true;
            return false;
        }
        var ring = terrain.Where(t => Borders(t.Rect)).ToList();
        var ringIds = ring.Select(t => t.Id).ToHashSet();

        // which hole edge a ring piece borders (its inner face) — top = above the hole, etc.
        string Side(int[] r)
        {
            var cells = CellsOf(r).ToHashSet();
            bool Row(int z) { for (var x = hx0; x <= hx1; x++) if (cells.Contains((x, z))) return true; return false; }
            bool Col(int x) { for (var z = hz0; z <= hz1; z++) if (cells.Contains((x, z))) return true; return false; }
            if (Row(hz0 - 1)) return "top";
            if (Row(hz1 + 1)) return "bottom";
            return Col(hx0 - 1) ? "left" : "right";
        }
        var sideOf = ring.ToDictionary(t => t.Id, t => Side(t.Rect));
        (string Id, int[] Rect) Opposite((string Id, int[] Rect) r)
        {
            var opp = sideOf[r.Id] switch { "top" => "bottom", "bottom" => "top", "left" => "right", _ => "left" };
            return ring.First(t => t.Id != r.Id && sideOf[t.Id] == opp);
        }
        // a ring piece with an external neighbour (a hub attachment, or a wool-extend run) is a bar; the
        // entry-bar is the one an attachment docks, so it is the reliable anchor when the room is ambiguous.
        bool HasAttachment((string Id, int[] Rect) rp) =>
            terrain.Any(t => t.Id != rp.Id && t.Id != room.Id && !ringIds.Contains(t.Id) && EdgeAdjacent(t.Rect, rp.Rect));

        // room-bar: the ring piece the room reaches — directly, or through a wool-extend run kept as its own
        // slot. When the room caps a ring corner it touches a bar and a leg both; pick the bar, identified as
        // the one whose opposite ring piece carries the hub attachment (the entry-bar).
        var roomNbrs = terrain.Where(t => t.Id != room.Id && EdgeAdjacent(t.Rect, room.Rect)).ToList();
        (string Id, int[] Rect) roomBar;
        var run = roomNbrs.FirstOrDefault(t => !ringIds.Contains(t.Id));
        if (run.Id is not null)
        {
            slots[run.Id] = ApproachSlots.Run;
            roomBar = ring.First(t => EdgeAdjacent(t.Rect, run.Rect));
        }
        else
        {
            var ringNbrs = roomNbrs.Where(t => ringIds.Contains(t.Id)).ToList();
            roomBar = ringNbrs.Count == 1 ? ringNbrs[0]
                : ringNbrs.FirstOrDefault(r => HasAttachment(Opposite(r))) is { Id: not null } bar ? bar
                : ringNbrs[0];
        }

        var entryBar = Opposite(roomBar);
        slots[roomBar.Id] = ApproachSlots.RoomBar;
        slots[entryBar.Id] = ApproachSlots.EntryBar;
        foreach (var t in ring.Where(t => t.Id != roomBar.Id && t.Id != entryBar.Id))
            slots[t.Id] = ApproachSlots.Leg;                     // the perpendicular pair
        // whatever is left — not ring, not room, not the run — is a hub attachment
        foreach (var t in terrain.Where(t => !slots.ContainsKey(t.Id)))
            slots[t.Id] = ApproachSlots.Entry;
    }

    // two cell rects share a positive-length edge (abut on one axis with real overlap on the other) — a bare
    // corner point does not count, so the ¾-solid corners inside a ring never read as a walkable adjacency
    private static bool EdgeAdjacent(int[] a, int[] b)
    {
        var ix = Math.Min(a[0] + a[2], b[0] + b[2]) - Math.Max(a[0], b[0]);
        var iz = Math.Min(a[1] + a[3], b[1] + b[3]) - Math.Max(a[1], b[1]);
        return (ix == 0 && iz > 0) || (iz == 0 && ix > 0);
    }

    private static IEnumerable<(int, int)> CellsOf(int[] r)
    {
        for (var x = r[0]; x < r[0] + r[2]; x++)
            for (var z = r[1]; z < r[1] + r[3]; z++) yield return (x, z);
    }
}

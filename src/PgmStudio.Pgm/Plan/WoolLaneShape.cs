namespace PgmStudio.Pgm.Plan;

/// <summary>
/// Classifies a wool room's approach LANE by the rectilinear topology of the corridor it caps, read on ONE
/// team unit's terrain (the k=0 image — a fanned mirror image would merge into the corridor near the centre).
/// <list type="bullet">
///   <item><b>I</b> straight · <b>L</b> one bend · <b>Z</b> two bends · <b>complex</b> ≥3 bends (the wool sits
///     on a chunky island, not a clean lane).</item>
///   <item><b>plaza</b> a chunk right at the room · <b>none</b> no terrain corridor (the room docks a build
///     zone / is the whole island).</item>
/// </list>
/// Method: seed at the room's terrain neighbours; the corridor width <c>W</c> is that first cross-section; a
/// cell is a JUNCTION if it sits in a filled <c>(W+1)×(W+1)</c> block (a region wider than the corridor — this
/// survives corners, where two W-wide arms overlap in only a W×W square, and stops at any wider hub). Flood the
/// non-junction terrain from the room = the corridor; its reflex (concave) corners are the bends.
/// </summary>
public static class WoolLaneShape
{
    /// <summary>Classify the lane of the wool room piece <paramref name="woolPieceId"/> in <paramref name="plan"/>
    /// (k=0 unit terrain).</summary>
    public static (string Shape, int Width) Classify(PlanModel plan, string woolPieceId)
    {
        var filled = new HashSet<(int, int)>();
        foreach (var p in plan.Pieces)
        {
            if (p.Role is PlanRoles.Buffer or PlanRoles.Connector) continue;
            for (var x = p.Rect[0]; x < p.Rect[0] + p.Rect[2]; x++)
                for (var z = p.Rect[1]; z < p.Rect[1] + p.Rect[3]; z++) filled.Add((x, z));
        }
        var wp = plan.Pieces.FirstOrDefault(p => p.Id == woolPieceId);
        if (wp is null) return ("none", 0);
        var room = new HashSet<(int, int)>();
        for (var x = wp.Rect[0]; x < wp.Rect[0] + wp.Rect[2]; x++)
            for (var z = wp.Rect[1]; z < wp.Rect[1] + wp.Rect[3]; z++) room.Add((x, z));
        return Classify(filled, room);
    }

    /// <summary>Classify the lane a <paramref name="room"/> caps within a single unit's <paramref name="filled"/>
    /// terrain (both in cell coordinates).</summary>
    public static (string Shape, int Width) Classify(IReadOnlySet<(int, int)> filled, IReadOnlySet<(int, int)> room)
    {
        int Hrun((int, int) c) { int n = 1; for (var x = c.Item1 - 1; filled.Contains((x, c.Item2)); x--) n++; for (var x = c.Item1 + 1; filled.Contains((x, c.Item2)); x++) n++; return n; }
        int Vrun((int, int) c) { int n = 1; for (var z = c.Item2 - 1; filled.Contains((c.Item1, z)); z--) n++; for (var z = c.Item2 + 1; filled.Contains((c.Item1, z)); z++) n++; return n; }

        var seeds = new List<(int, int)>();
        foreach (var r in room) foreach (var nb in N4(r)) if (filled.Contains(nb) && !room.Contains(nb)) seeds.Add(nb);
        if (seeds.Count == 0) return ("none", 0);

        int w = Math.Clamp(seeds.Min(c => Math.Min(Hrun(c), Vrun(c))), 2, 6), k = w + 1;
        bool Blk((int, int) o) { for (var dx = 0; dx < k; dx++) for (var dz = 0; dz < k; dz++) if (!filled.Contains((o.Item1 + dx, o.Item2 + dz))) return false; return true; }
        bool Thick((int, int) c) { for (var x = c.Item1 - k + 1; x <= c.Item1; x++) for (var z = c.Item2 - k + 1; z <= c.Item2; z++) if (Blk((x, z))) return true; return false; }
        bool Narrow((int, int) c) => filled.Contains(c) && !room.Contains(c) && !Thick(c);

        var lane = new HashSet<(int, int)>();
        var q = new Queue<(int, int)>();
        foreach (var s in seeds) if (Narrow(s) && lane.Add(s)) q.Enqueue(s);
        while (q.Count > 0) { var cur = q.Dequeue(); foreach (var nb in N4(cur)) if (Narrow(nb) && lane.Add(nb)) q.Enqueue(nb); }
        if (lane.Count == 0) return ("plaza", w);

        int reflex = 0, mnx = lane.Min(c => c.Item1), mxx = lane.Max(c => c.Item1) + 1, mnz = lane.Min(c => c.Item2), mxz = lane.Max(c => c.Item2) + 1;
        for (var x = mnx; x <= mxx; x++)
            for (var z = mnz; z <= mxz; z++)
            {
                int cnt = 0;
                if (lane.Contains((x, z))) cnt++; if (lane.Contains((x - 1, z))) cnt++;
                if (lane.Contains((x, z - 1))) cnt++; if (lane.Contains((x - 1, z - 1))) cnt++;
                if (cnt == 3) reflex++;   // a reflex (concave) corner = one bend
            }
        return (reflex == 0 ? "I" : reflex == 1 ? "L" : reflex == 2 ? "Z" : "complex", w);
    }

    private static IEnumerable<(int, int)> N4((int, int) c)
    {
        yield return (c.Item1 + 1, c.Item2); yield return (c.Item1 - 1, c.Item2);
        yield return (c.Item1, c.Item2 + 1); yield return (c.Item1, c.Item2 - 1);
    }
}

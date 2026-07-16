namespace PgmStudio.Pgm.Compose;

/// <summary>The result of an isolation cut: the unit's pieces with the severed one translated away from its
/// lane (plus a connector extrusion when the dock needed one), the bridge zone rect (cells), and the severed
/// piece's id.</summary>
public sealed record CutResult(IReadOnlyList<GrownPiece> Pieces, int[] BridgeRect, string SeveredId);

/// <summary>
/// The CT5 team-side cut: sever one dead-end marker piece — a wool plateau by preference (WL4's isolated
/// wool), else the spawn piece (SP6, big teams only: an isolated spawn slows small-map spawning too much) —
/// by translating it away from its lane to open a 10..20-block void, bridged by a build zone that docks
/// FLUSH against both sides (BZ7: shared borders, zero overlap) at the full interface width (BZ9). When the
/// severed piece leaves a mid-face scar on a longer edge, a small connector extrusion (BZ8's readable
/// "offspring" piece) carries the interface and the bridge docks full-width against the connector's end; a
/// corner-flush or full-width scar docks directly. The severed piece keeps its marker. Skipped, never fatal,
/// when no candidate placement survives the invariants.
///
/// Draw order (after the mid draws): (x1) whether to cut, (x2) the wool candidate rotation, (x3) the gap.
///
/// <para><b>Not in the compose loop.</b> Kept intact but no longer called by <see cref="Composer"/>: it cut a
/// wool approach before fragmentation had slot-carving rules (a bridge landing across a clean lane). It returns
/// as a proper slot-aware fragment pass — cutting only a <c>run</c>/<c>bar</c>, never a <c>room</c>/<c>entry</c>
/// — at which point <see cref="Composer"/> calls it again.</para>
/// </summary>
public static class IsolationCut
{
    public static CutResult? TryApply(ComposeEnvelope env, ComposeRng rng, GrownUnit unit, MidResult mid)
    {
        if (!rng.NextBool(0.4)) return null;                       // (x1)
        var rot = rng.NextInt(0, unit.Wools.Count);                // (x2)
        var gap = rng.NextInt(2, 5);                               // (x3) WL4: 10..20 blocks at cell 5

        var candidates = Enumerable.Range(0, unit.Wools.Count)
            .Select(i => unit.Wools[(rot + i) % unit.Wools.Count].Piece)
            .ToList();
        if (env.PlayersPerTeam >= 10) candidates.Add(unit.Spawn.Piece);

        foreach (var id in candidates)
            if (TrySever(env, unit, mid, id, gap) is { } result)
                return result;
        return null;
    }

    private static CutResult? TrySever(ComposeEnvelope env, GrownUnit unit, MidResult mid, string id, int gap)
    {
        var pieces = unit.Pieces;
        var p = pieces.First(x => x.Id == id).Rect;

        // a severable piece is a dead end: exactly one land neighbour (its lane), which the cut opens away from
        var neighbours = pieces.Where(o => o.Id != id && BorderLen(p, o.Rect) > 0).ToList();
        if (neighbours.Count != 1) return null;
        var n = neighbours[0].Rect;

        // translate along the axis they abut on, away from the neighbour. The scar on the neighbour's face
        // decides the dock (BZ8): full-width or corner-flush docks directly; a mid-face scar gets a 1-cell
        // connector extrusion carrying the interface, and the piece moves one cell further.
        int[] moved, bridge;
        int[]? connector = null;
        var ix = Overlap(p[0], p[2], n[0], n[2]);
        var iz = Overlap(p[1], p[3], n[1], n[3]);
        if (ix == 0 && iz > 0)
        {
            var lo = Math.Max(p[1], n[1]);
            var hi = Math.Min(p[1] + p[3], n[1] + n[3]);
            var midFace = lo > n[1] && hi < n[1] + n[3];
            var c = midFace ? 1 : 0;
            var sign = p[0] >= n[0] + n[2] ? 1 : -1;
            var edge = sign > 0 ? n[0] + n[2] : n[0];
            moved = [p[0] + sign * (gap + c), p[1], p[2], p[3]];
            if (midFace) connector = [sign > 0 ? edge : edge - 1, lo, 1, hi - lo];
            bridge = [sign > 0 ? edge + c : edge - c - gap, lo, gap, hi - lo];
        }
        else if (iz == 0 && ix > 0)
        {
            var lo = Math.Max(p[0], n[0]);
            var hi = Math.Min(p[0] + p[2], n[0] + n[2]);
            var midFace = lo > n[0] && hi < n[0] + n[2];
            var c = midFace ? 1 : 0;
            var sign = p[1] >= n[1] + n[3] ? 1 : -1;
            var edge = sign > 0 ? n[1] + n[3] : n[1];
            moved = [p[0], p[1] + sign * (gap + c), p[2], p[3]];
            if (midFace) connector = [lo, sign > 0 ? edge : edge - 1, hi - lo, 1];
            bridge = [lo, sign > 0 ? edge + c : edge - c - gap, hi - lo, gap];
        }
        else
            return null;

        // ── hard invariants: the moved piece is isolated land (full clearance to everything, fanned), the
        // marker distances and the LN2 chains still hold, the land budget stays in band with the connector
        // counted, and neither the piece nor its bridge reaches the mid band or its stones ──
        var land = pieces.Where(x => x.Id != id).Select(x => x.Rect).ToList();
        if (connector is not null) land.Add(connector);
        var isolated = mid.Stones.Select(s => s.Rect).Append(moved).ToList();
        if (!ComposeGeometry.SeparationOk(env, land, isolated)) return null;

        var allRects = land.Append(moved).ToList();
        if (TeamUnitGrower.MaxChainBlocks(env.Cell, allRects) > TeamUnitGrower.LaneChainMaxBlocks) return null;
        var area = allRects.Sum(r => (double)r[2] * r[3] * env.Cell * env.Cell);
        if (area > env.LandPerTeam * 1.2) return null;

        var rectById = pieces.ToDictionary(x => x.Id, x => x.Id == id ? moved : x.Rect);
        if (!TeamUnitGrower.ValidateMarkers(env, rectById, unit.Spawn.Piece, unit.Spawn.At, unit.Wools)) return null;

        if (Touches(moved, mid.BandRect) || Touches(bridge, mid.BandRect)) return null;
        if (connector is not null && Touches(connector, mid.BandRect)) return null;
        foreach (var stone in mid.Stones)
            if (Touches(bridge, stone.Rect)) return null;

        // the severed piece is translated, never rebuilt: role, slot and box ownership ride along
        var newPieces = pieces.Select(x => x.Id == id ? x with { Rect = moved } : x).ToList();
        if (connector is not null) newPieces.Add(new GrownPiece("connector-a", connector));
        return new CutResult(newPieces, bridge, id);
    }

    private static int Overlap(int aMin, int aSpan, int bMin, int bSpan) =>
        Math.Min(aMin + aSpan, bMin + bSpan) - Math.Max(aMin, bMin);

    private static int BorderLen(int[] a, int[] b)
    {
        var ix = Overlap(a[0], a[2], b[0], b[2]);
        var iz = Overlap(a[1], a[3], b[1], b[3]);
        if (ix == 0 && iz > 0) return iz;
        if (iz == 0 && ix > 0) return ix;
        return 0;
    }

    private static bool Touches(int[] a, int[] b) =>
        Overlap(a[0], a[2], b[0], b[2]) >= 0 && Overlap(a[1], a[3], b[1], b[3]) >= 0
        && !(Overlap(a[0], a[2], b[0], b[2]) == 0 && Overlap(a[1], a[3], b[1], b[3]) == 0);
}

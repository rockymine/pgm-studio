using PgmStudio.Geom;

namespace PgmStudio.Pgm.Plan;

/// <summary>A single validation finding: its <see cref="Severity"/>, a message, (for lint) the rule id it
/// cites from the layout-rules checklist, and the ids of the pieces/zones it implicates (for the editor to
/// highlight on click).</summary>
public sealed record PlanFinding(PlanSeverity Severity, string Message, string? Rule = null, IReadOnlyList<string>? Subjects = null)
{
    /// <summary>The implicated piece/zone ids, never null.</summary>
    public IReadOnlyList<string> SubjectIds => Subjects ?? [];
}

public enum PlanSeverity { Error, Lint }

/// <summary>
/// The plan validator: structural <b>errors</b> that block a compile (unreachable wool, a wool path only
/// through a spawn piece, a placement outside its piece, different-surface piece overlaps) and non-blocking
/// <b>lint</b> that cites a provisional layout rule by id — including corner contacts (PC-C), which never form a
/// land interface but are author judgment, not blockers. (Narrow seams are legal connecting geometry, so there
/// is no per-seam width lint — corridor quality of the assembled footprint is a later concern.) Pure — a plan
/// validates the same on the server and in the editor. Lint rules are a small extensible table (see
/// <see cref="LintRules"/>).
/// </summary>
public static class PlanValidator
{
    public static IReadOnlyList<PlanFinding> Validate(PlanModel plan)
    {
        var d = ContactGraph.Build(plan);
        var findings = new List<PlanFinding>();
        findings.AddRange(Errors(plan, d));
        foreach (var rule in LintRules) findings.AddRange(rule(plan, d));
        return findings;
    }

    public static IReadOnlyList<PlanFinding> Errors(PlanModel plan) => Errors(plan, ContactGraph.Build(plan)).ToList();

    public static bool HasErrors(PlanModel plan) => Validate(plan).Any(f => f.Severity == PlanSeverity.Error);

    // ── errors (block the compile) ──────────────────────────────────────────────────────────────────────

    private static IEnumerable<PlanFinding> Errors(PlanModel plan, ContactGraph d)
    {
        var findings = new List<PlanFinding>();
        void Error(string m, params string[] subjects) =>
            findings.Add(new PlanFinding(PlanSeverity.Error, m, null, subjects.Length > 0 ? subjects : null));

        // different-surface overlaps: two pieces claim the same ground at incompatible heights — no coherent
        // surface, a genuine structural error. (Narrow seams connect and are legal; corner contacts are author
        // judgment and lint, not errors — see PC-C.)
        foreach (var c in d.Contacts)
            if (c.Kind == ContactKind.Overlap && c.SurfaceDelta != 0)
                Error($"overlapping pieces '{c.A}' and '{c.B}' have different surfaces (delta {c.SurfaceDelta})", c.A, c.B);

        // placements must reference a real piece and sit inside it (a wool's flat area is its piece footprint)
        foreach (var s in plan.Placements.Spawns) CheckInside(d, "spawn", s.Piece, s.At, findings);
        foreach (var w in plan.Placements.Wools) CheckInside(d, "wool", w.Piece, w.At, findings);
        foreach (var ir in plan.Placements.Iron) CheckInside(d, "iron", ir.Piece, ir.At, findings);

        // a wall mark must land on a real shared land interface (else there is no lane seam to build across)
        var landPairs = new HashSet<(string, string)>();
        foreach (var c in d.LandInterfaces) { landPairs.Add((c.A, c.B)); landPairs.Add((c.B, c.A)); }
        foreach (var w in plan.Walls)
            if (!landPairs.Contains((w.A, w.B)))
                Error($"wall '{w.A}'–'{w.B}' is not a shared land interface", w.A, w.B);

        // reachability over the fanned board: every wool reachable from each capturing team's spawn, and not
        // only via a spawn piece
        findings.AddRange(ReachabilityErrors(plan, d));
        return findings;
    }

    private static void CheckInside(ContactGraph d, string kind, string pieceId, double[] at, List<PlanFinding> findings)
    {
        var piece = d.Plan.Pieces.FirstOrDefault(p => p.Id == pieceId);
        if (piece is null) { findings.Add(new PlanFinding(PlanSeverity.Error, $"{kind} references unknown piece '{pieceId}'", null, [pieceId])); return; }
        // A buffer is reserved empty space — it produces no terrain, so nothing may be placed on it.
        if (PlanRoles.IsAnnotation(piece.Role)) { findings.Add(new PlanFinding(PlanSeverity.Error, $"{kind} references non-generating buffer '{pieceId}'", null, [pieceId])); return; }
        double x = at[0], z = at[1];
        int w = piece.Rect[2], h = piece.Rect[3];
        if (x < 0 || z < 0 || x > w || z > h)
            findings.Add(new PlanFinding(PlanSeverity.Error, $"{kind} at [{x},{z}] falls outside piece '{pieceId}' (0..{w}, 0..{h})", null, [pieceId]));
    }

    // Build the fanned piece graph (land + gap edges), then check each wool node is reachable from a capturing
    // team's spawn, and that some frontline path reaches it without passing through a spawn piece.
    private static IEnumerable<PlanFinding> ReachabilityErrors(PlanModel plan, ContactGraph d)
    {
        var findings = new List<PlanFinding>();
        var spawnPieces = plan.Placements.Spawns.Select(s => s.Piece).ToList();
        var woolPieces = plan.Placements.Wools.Select(w => w.Piece).ToList();
        if (spawnPieces.Count == 0 || woolPieces.Count == 0) return findings;

        var graph = FannedGraph.Build(d);
        var spawnNodes = graph.Nodes.Where(n => spawnPieces.Contains(n.PieceId)).Select(n => n.Key).ToHashSet();

        foreach (var wp in woolPieces)
            for (var owner = 0; owner < d.Order; owner++)
            {
                var woolNode = (owner, wp);
                if (!graph.Nodes.Any(n => n.Key == woolNode)) continue;

                // every other team captures this wool: each capturing spawn must reach the wool node
                for (var captor = 0; captor < d.Order; captor++)
                {
                    if (captor == owner) continue;
                    var from = graph.Nodes.Where(n => n.Team == captor && spawnPieces.Contains(n.PieceId)).Select(n => n.Key);
                    if (!graph.Reachable(from, woolNode))
                        findings.Add(new PlanFinding(PlanSeverity.Error,
                            $"wool on '{wp}' (team {owner}) is unreachable from team {captor}'s spawn", null, [wp]));
                }

                // SP1: the wool must be reachable from a frontline piece without crossing a spawn piece
                var frontStarts = graph.Nodes.Where(n => graph.Frontline.Contains(n.Key) && !spawnNodes.Contains(n.Key)).Select(n => n.Key);
                if (!graph.ReachableAvoiding(frontStarts, woolNode, spawnNodes))
                    findings.Add(new PlanFinding(PlanSeverity.Error,
                        $"wool on '{wp}' (team {owner}) is only reachable through a spawn piece (SP1)", null, [wp]));
            }
        return findings;
    }

    // ── lint (never blocks; each cites a rule id) ───────────────────────────────────────────────────────

    /// <summary>The lint table — one entry per checked rule; add a rule by appending a delegate.</summary>
    public static readonly IReadOnlyList<Func<PlanModel, ContactGraph, IEnumerable<PlanFinding>>> LintRules =
    [
        LintPcC, LintG2, LintG5, LintSp2, LintBz5, LintEl1, LintEl3, LintSt2,
    ];

    private static PlanFinding Lint(string rule, string msg, params string[] subjects) =>
        new(PlanSeverity.Lint, msg, rule, subjects.Length > 0 ? subjects : null);

    // PC-C — a corner contact: two pieces meet at a single point. Per the Definitions a corner touch is never a
    // connection (no walkable corridor mouth). A corner as the pair's only relationship is a sneaky diagonal
    // between otherwise-separate areas; when the pieces already join the same land component through real
    // interfaces the corner is harmless, so it is suppressed.
    private static IEnumerable<PlanFinding> LintPcC(PlanModel plan, ContactGraph d)
    {
        var comp = ComponentIndex(d);
        foreach (var c in d.Contacts)
            if (c.Kind == ContactKind.Corner && !SameComponent(comp, c.A, c.B))
                yield return Lint("PC-C", $"corner contact between separate areas: '{c.A}' and '{c.B}' touch at a point, not a corridor (no land interface)", c.A, c.B);
    }

    // Map each piece id to its land component index (components join pieces via real land interfaces and
    // same-surface overlaps). Two pieces in the same component are already walkably connected.
    private static Dictionary<string, int> ComponentIndex(ContactGraph d)
    {
        var comp = new Dictionary<string, int>();
        for (var i = 0; i < d.Components.Count; i++)
            foreach (var id in d.Components[i]) comp[id] = i;
        return comp;
    }

    private static bool SameComponent(Dictionary<string, int> comp, string a, string b) =>
        comp.TryGetValue(a, out var ca) && comp.TryGetValue(b, out var cb) && ca == cb;

    // G2 — minimum corridor width 10: a build zone narrower than the corridor minimum in either dimension.
    private static IEnumerable<PlanFinding> LintG2(PlanModel plan, ContactGraph d)
    {
        foreach (var z in plan.Zones)
        {
            var r = ContactGraph.ToBlock(z.Rect, d.Cell);
            var min = Math.Min(r.Width, r.Depth);
            if (min < ContactGraph.CorridorMin)
                yield return Lint("G2", $"zone '{z.Id}' corridor width {min} < {ContactGraph.CorridorMin}", z.Id);
        }
    }

    // G5 — void gaps between individual landmasses are 10–20 per hop.
    private static IEnumerable<PlanFinding> LintG5(PlanModel plan, ContactGraph d)
    {
        foreach (var g in d.GapLinks)
        {
            if (g.Hop == 0) continue;   // abutting inside the zone — not a hop
            if (g.Hop < 10) yield return Lint("G5", $"gap hop {g.Hop} < 10 between '{g.A}' and '{g.B}'", g.A, g.B);
            else if (g.Hop > 20) yield return Lint("G5", $"gap hop {g.Hop} > 20 between '{g.A}' and '{g.B}'", g.A, g.B);
        }
    }

    // SP2 — spawn near the back of its lane (toward the map edge), not the front half.
    private static IEnumerable<PlanFinding> LintSp2(PlanModel plan, ContactGraph d)
    {
        foreach (var s in plan.Placements.Spawns)
        {
            var piece = d.Piece(s.Piece);
            if (piece is null) continue;
            var (bx, bz) = ResolveBlock(piece.Value.Rect, s.At, d.Cell);
            // back = the piece half farther from the centre along its dominant axis
            var r = piece.Value.Rect;
            bool zAxis = r.Depth >= r.Width;
            double pos = zAxis ? bz : bx, mid = zAxis ? r.CenterZ : r.CenterX, center = 0;
            bool inBack = Math.Abs(pos - center) >= Math.Abs(mid - center);
            if (!inBack) yield return Lint("SP2", $"spawn on '{s.Piece}' not near the back of its lane", s.Piece);
        }
    }

    // BZ5 — build zones never touch a spawn piece.
    private static IEnumerable<PlanFinding> LintBz5(PlanModel plan, ContactGraph d)
    {
        var spawnPieces = plan.Placements.Spawns.Select(s => s.Piece).ToHashSet();
        foreach (var z in plan.Zones)
        {
            var zr = ContactGraph.ToBlock(z.Rect, d.Cell);
            foreach (var p in d.Pieces)
                if (spawnPieces.Contains(p.Id) && Touches(p.Rect, zr))
                    yield return Lint("BZ5", $"build zone '{z.Id}' touches spawn piece '{p.Id}'", z.Id, p.Id);
        }
    }

    // EL1 — plateau step unit is 2: every piece surface delta from the base is a multiple of 2.
    private static IEnumerable<PlanFinding> LintEl1(PlanModel plan, ContactGraph d)
    {
        foreach (var p in plan.Pieces)
        {
            if (PlanRoles.IsAnnotation(p.Role)) continue;   // buffers produce no terrain — no plateau step to check
            var delta = (p.Surface ?? plan.Globals.Surface) - plan.Globals.Surface;
            if (delta % 2 != 0) yield return Lint("EL1", $"piece '{p.Id}' surface delta {delta} is not a multiple of 2", p.Id);
        }
    }

    // EL3/EL6 — a land interface with |Δsurface| ≥ 4 must carry a `cliffs` mark only when it is a genuine
    // cliff by the EL6 qualification, not merely the edge of a staircase. A tall seam qualifies as a cliff
    // when (b) it cuts a full-lane width (seam length ≥ the corridor minimum) and (c) either has no gentler
    // bypass, or — where a stepped bypass runs alongside — the drop is ≥ 6. Shorter seams and those relieved
    // by a ≤ 4 bypass are stepped path edges and need no mark. The finding cites EL6, the qualification.
    private static IEnumerable<PlanFinding> LintEl3(PlanModel plan, ContactGraph d)
    {
        var cliffs = plan.Cliffs.Select(c => (c.A, c.B)).ToHashSet();
        foreach (var c in d.LandInterfaces)
        {
            if (!QualifiesAsCliff(d, c)) continue;
            if (cliffs.Contains((c.A, c.B)) || cliffs.Contains((c.B, c.A))) continue;
            yield return Lint("EL6", $"land interface '{c.A}'–'{c.B}' delta {c.SurfaceDelta} ≥ 4 is a cliff (EL6) and needs a `cliffs` mark", c.A, c.B);
        }
    }

    // EL6 cliff qualification: a Δ ≥ 4 land seam reads as a cliff (as opposed to a stepped path edge beside a
    // staircase) only when it cuts a full-lane width — seam length ≥ the corridor minimum. Given the width,
    // a big drop (Δ ≥ 6) is always a cliff; a shallow drop (Δ 4–5) is a cliff only where it walls a pit — a low
    // floor pinched between opposing cliffs (EL7) with no gentler way out — and not where it is a lone step up
    // onto a plateau or the edge of a staircase (a stepped bypass runs alongside).
    private static bool QualifiesAsCliff(ContactGraph d, Contact c)
    {
        var delta = Math.Abs(c.SurfaceDelta);
        if (delta < 4) return false;
        if (c.BorderLength < ContactGraph.CorridorMin) return false;   // narrower than a lane → stepped edge
        if (delta >= 6) return true;                                  // a full-width big drop is always a cliff
        return WallsAPit(d, c) && !HasSteppedBypass(d, c);            // a shallow drop: only a sealed pit wall
    }

    // The seam walls a pit when its lower (floor) piece is pinched between opposing cliffs: it carries another
    // Δ ≥ 4 land seam, to a higher piece, on the face opposite this one (west↔east or north↔south).
    private static bool WallsAPit(ContactGraph d, Contact c)
    {
        var a = d.Piece(c.A)!.Value;
        var b = d.Piece(c.B)!.Value;
        var floor = a.Surface <= b.Surface ? a : b;
        var faces = new HashSet<char>();
        foreach (var e in d.LandInterfaces)
        {
            if (Math.Abs(e.SurfaceDelta) < 4) continue;
            var other = e.A == floor.Id ? d.Piece(e.B) : e.B == floor.Id ? d.Piece(e.A) : null;
            if (other is null || other.Value.Surface <= floor.Surface) continue;   // wall must be the higher side
            faces.Add(FaceToward(floor.Rect, other.Value.Rect));
        }
        return (faces.Contains('W') && faces.Contains('E')) || (faces.Contains('N') && faces.Contains('S'));
    }

    // Which face of the floor rect the wall abuts (W/E on a vertical seam, N/S on a horizontal one).
    private static char FaceToward(BlockRect floor, BlockRect wall)
    {
        var (x1, z1, x2, _) = ContactGraph.BorderSegment(floor, wall);
        if (x1 == x2) return x1 == floor.MinX ? 'W' : 'E';
        return z1 == floor.MinZ ? 'N' : 'S';
    }

    // A stepped bypass exists when pieces c.A and c.B are joined by an alternative land path — every hop a
    // walkable (|Δ| ≤ 2) land seam — that does not use the seam c itself: a gentler way around the drop.
    private static bool HasSteppedBypass(ContactGraph d, Contact c)
    {
        var adj = new Dictionary<string, List<string>>();
        void Link(string x, string y)
        {
            if (!adj.TryGetValue(x, out var l)) adj[x] = l = [];
            l.Add(y);
        }
        foreach (var e in d.LandInterfaces)
        {
            if ((e.A == c.A && e.B == c.B) || (e.A == c.B && e.B == c.A)) continue;   // the seam itself
            if (Math.Abs(e.SurfaceDelta) > 2) continue;                               // not a gentle step
            Link(e.A, e.B);
            Link(e.B, e.A);
        }
        var seen = new HashSet<string> { c.A };
        var q = new Queue<string>();
        q.Enqueue(c.A);
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (cur == c.B) return true;
            if (!adj.TryGetValue(cur, out var nbs)) continue;
            foreach (var nb in nbs)
                if (seen.Add(nb)) q.Enqueue(nb);
        }
        return false;
    }

    // ST2 — when a spawn-role piece exists, every iron marker belongs inside a spawn piece (iron inside the
    // spawn region auto-renews in the export; iron elsewhere is dead resource).
    private static IEnumerable<PlanFinding> LintSt2(PlanModel plan, ContactGraph d)
    {
        var spawnPieces = d.Pieces.Where(p => p.Role == PlanRoles.Spawn).ToList();
        if (spawnPieces.Count == 0) yield break;
        foreach (var ir in plan.Placements.Iron)
        {
            var piece = d.Piece(ir.Piece);
            if (piece is null) continue;                       // unknown-piece handled as a structural error
            var (bx, bz) = ResolveBlock(piece.Value.Rect, ir.At, d.Cell);
            bool inside = spawnPieces.Any(sp =>
                bx >= sp.Rect.MinX && bx <= sp.Rect.MaxX && bz >= sp.Rect.MinZ && bz <= sp.Rect.MaxZ);
            if (!inside) yield return Lint("ST2", $"iron at ({bx:0},{bz:0}) outside the spawn piece", ir.Piece);
        }
    }

    // ── shared helpers ──────────────────────────────────────────────────────────────────────────────────

    private static (double X, double Z) ResolveBlock(BlockRect piece, double[] at, int cell) =>
        (piece.MinX + at[0] * cell, piece.MinZ + at[1] * cell);

    private static bool Touches(BlockRect a, BlockRect b)
    {
        int ix = Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX);
        int iz = Math.Min(a.MaxZ, b.MaxZ) - Math.Max(a.MinZ, b.MinZ);
        return ix >= 0 && iz >= 0 && !(ix == 0 && iz == 0);
    }
}

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
/// <b>lint</b> that cites a provisional layout rule by id — including sliver/corner contacts (PC-S/PC-C), which
/// never form a land interface but are author judgment, not blockers. Pure — a plan validates the same on the
/// server and in the editor. Lint rules are a small extensible table (see <see cref="LintRules"/>).
/// </summary>
public static class PlanValidator
{
    public static IReadOnlyList<PlanFinding> Validate(PlanModel plan)
    {
        var d = PlanDerived.Build(plan);
        var findings = new List<PlanFinding>();
        findings.AddRange(Errors(plan, d));
        foreach (var rule in LintRules) findings.AddRange(rule(plan, d));
        return findings;
    }

    public static IReadOnlyList<PlanFinding> Errors(PlanModel plan) => Errors(plan, PlanDerived.Build(plan)).ToList();

    public static bool HasErrors(PlanModel plan) => Validate(plan).Any(f => f.Severity == PlanSeverity.Error);

    // ── errors (block the compile) ──────────────────────────────────────────────────────────────────────

    private static IEnumerable<PlanFinding> Errors(PlanModel plan, PlanDerived d)
    {
        var findings = new List<PlanFinding>();
        void Error(string m, params string[] subjects) =>
            findings.Add(new PlanFinding(PlanSeverity.Error, m, null, subjects.Length > 0 ? subjects : null));

        // different-surface overlaps: two pieces claim the same ground at incompatible heights — no coherent
        // surface, a genuine structural error. (Sliver and corner contacts are author judgment and lint, not
        // errors — see PC-S / PC-C.)
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

    private static void CheckInside(PlanDerived d, string kind, string pieceId, double[] at, List<PlanFinding> findings)
    {
        var piece = d.Plan.Pieces.FirstOrDefault(p => p.Id == pieceId);
        if (piece is null) { findings.Add(new PlanFinding(PlanSeverity.Error, $"{kind} references unknown piece '{pieceId}'", null, [pieceId])); return; }
        double x = at[0], z = at[1];
        int w = piece.Rect[2], h = piece.Rect[3];
        if (x < 0 || z < 0 || x > w || z > h)
            findings.Add(new PlanFinding(PlanSeverity.Error, $"{kind} at [{x},{z}] falls outside piece '{pieceId}' (0..{w}, 0..{h})", null, [pieceId]));
    }

    // Build the fanned piece graph (land + gap edges), then check each wool node is reachable from a capturing
    // team's spawn, and that some frontline path reaches it without passing through a spawn piece.
    private static IEnumerable<PlanFinding> ReachabilityErrors(PlanModel plan, PlanDerived d)
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
    public static readonly IReadOnlyList<Func<PlanModel, PlanDerived, IEnumerable<PlanFinding>>> LintRules =
    [
        LintPcS, LintPcC, LintG2, LintG5, LintSp2, LintWl2, LintBz5, LintEl1, LintEl3, LintSt2,
    ];

    private static PlanFinding Lint(string rule, string msg, params string[] subjects) =>
        new(PlanSeverity.Lint, msg, rule, subjects.Length > 0 ? subjects : null);

    // PC-S — a sliver contact: two pieces share a border shorter than the corridor minimum. Per the
    // Definitions in the layout rules a connection is a straight border ≥ one corridor width, so a sliver is
    // never a land interface — but a deliberate thin ledge (lane-thickness variation) is author judgment, so
    // this lints rather than blocks.
    private static IEnumerable<PlanFinding> LintPcS(PlanModel plan, PlanDerived d)
    {
        foreach (var c in d.Contacts)
            if (c.Kind == ContactKind.Sliver)
                yield return Lint("PC-S", $"sliver contact: '{c.A}' and '{c.B}' share only {c.BorderLength} < {PlanDerived.CorridorMin} blocks (no land interface)", c.A, c.B);
    }

    // PC-C — a corner contact: two pieces meet at a single point. Per the Definitions a corner touch is never a
    // connection (no walkable corridor mouth); it is harmless and often deliberate, so this lints, not blocks.
    private static IEnumerable<PlanFinding> LintPcC(PlanModel plan, PlanDerived d)
    {
        foreach (var c in d.Contacts)
            if (c.Kind == ContactKind.Corner)
                yield return Lint("PC-C", $"corner contact: '{c.A}' and '{c.B}' touch at a point, not a corridor (no land interface)", c.A, c.B);
    }

    // G2 — minimum corridor width 10: a build zone narrower than the corridor minimum in either dimension.
    private static IEnumerable<PlanFinding> LintG2(PlanModel plan, PlanDerived d)
    {
        foreach (var z in plan.Zones)
        {
            var r = PlanDerived.ToBlock(z.Rect, d.Cell);
            var min = Math.Min(r.Width, r.Depth);
            if (min < PlanDerived.CorridorMin)
                yield return Lint("G2", $"zone '{z.Id}' corridor width {min} < {PlanDerived.CorridorMin}", z.Id);
        }
    }

    // G5 — void gaps between individual landmasses are 10–20 per hop.
    private static IEnumerable<PlanFinding> LintG5(PlanModel plan, PlanDerived d)
    {
        foreach (var g in d.GapLinks)
        {
            if (g.Hop == 0) continue;   // abutting inside the zone — not a hop
            if (g.Hop < 10) yield return Lint("G5", $"gap hop {g.Hop} < 10 between '{g.A}' and '{g.B}'", g.A, g.B);
            else if (g.Hop > 20) yield return Lint("G5", $"gap hop {g.Hop} > 20 between '{g.A}' and '{g.B}'", g.A, g.B);
        }
    }

    // SP2 — spawn near the back of its lane (toward the map edge), not the front half.
    private static IEnumerable<PlanFinding> LintSp2(PlanModel plan, PlanDerived d)
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

    // WL2 — a team's wool sits ≥ 20 blocks from its spawn.
    private static IEnumerable<PlanFinding> LintWl2(PlanModel plan, PlanDerived d)
    {
        foreach (var s in plan.Placements.Spawns)
        {
            var sp = d.Piece(s.Piece);
            if (sp is null) continue;
            var (sx, sz) = ResolveBlock(sp.Value.Rect, s.At, d.Cell);
            foreach (var w in plan.Placements.Wools)
            {
                var wp = d.Piece(w.Piece);
                if (wp is null) continue;
                var (wx, wz) = ResolveBlock(wp.Value.Rect, w.At, d.Cell);
                var dist = Math.Sqrt((wx - sx) * (wx - sx) + (wz - sz) * (wz - sz));
                if (dist < 20) yield return Lint("WL2", $"wool on '{w.Piece}' is {dist:0} < 20 blocks from spawn on '{s.Piece}'", w.Piece, s.Piece);
            }
        }
    }

    // BZ5 — build zones never touch a spawn piece.
    private static IEnumerable<PlanFinding> LintBz5(PlanModel plan, PlanDerived d)
    {
        var spawnPieces = plan.Placements.Spawns.Select(s => s.Piece).ToHashSet();
        foreach (var z in plan.Zones)
        {
            var zr = PlanDerived.ToBlock(z.Rect, d.Cell);
            foreach (var p in d.Pieces)
                if (spawnPieces.Contains(p.Id) && Touches(p.Rect, zr))
                    yield return Lint("BZ5", $"build zone '{z.Id}' touches spawn piece '{p.Id}'", z.Id, p.Id);
        }
    }

    // EL1 — plateau step unit is 2: every piece surface delta from the base is a multiple of 2.
    private static IEnumerable<PlanFinding> LintEl1(PlanModel plan, PlanDerived d)
    {
        foreach (var p in plan.Pieces)
        {
            var delta = (p.Surface ?? plan.Globals.Surface) - plan.Globals.Surface;
            if (delta % 2 != 0) yield return Lint("EL1", $"piece '{p.Id}' surface delta {delta} is not a multiple of 2", p.Id);
        }
    }

    // EL3 — a land interface with |Δsurface| ≥ 4 must be an explicit cliff.
    private static IEnumerable<PlanFinding> LintEl3(PlanModel plan, PlanDerived d)
    {
        var cliffs = plan.Cliffs.Select(c => (c.A, c.B)).ToHashSet();
        foreach (var c in d.LandInterfaces)
        {
            if (Math.Abs(c.SurfaceDelta) < 4) continue;
            if (cliffs.Contains((c.A, c.B)) || cliffs.Contains((c.B, c.A))) continue;
            yield return Lint("EL3", $"land interface '{c.A}'–'{c.B}' delta {c.SurfaceDelta} ≥ 4 requires a cliff", c.A, c.B);
        }
    }

    // ST2 — when a spawn-role piece exists, every iron marker belongs inside a spawn piece (iron inside the
    // spawn region auto-renews in the export; iron elsewhere is dead resource).
    private static IEnumerable<PlanFinding> LintSt2(PlanModel plan, PlanDerived d)
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

using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Compose;

/// <summary>Everything one compose attempt produced, kept apart so tests can gate each step separately.</summary>
public sealed record ComposedStages(
    ComposeEnvelope Envelope, GrownUnit Unit, CrossingDesign Crossing, MidResult Mid, CutResult? Cut, PlanModel Plan);

/// <summary>
/// Composes a full <see cref="PlanModel"/> from nothing but a player count and team shape, running the
/// design-doc order of operations: derive the board envelope (<see cref="Envelope"/>), sample the crossing
/// arithmetic (<see cref="MidCarver.SampleCrossing"/>), grow one team's authored unit against it
/// (<see cref="TeamUnitGrower"/>), carve the mid band and stones (<see cref="MidCarver"/>), optionally sever
/// one marker piece behind a bridge (<see cref="IsolationCut"/>), and assemble the plan. Every attempt's
/// assembled plan must pass the final acceptance gate — zero validator errors, every void hop in G5's band,
/// and no closure hole ringed by a wool plateau (WL8 is out of the grammar) — or the whole attempt is
/// resampled. Cliffs and walls stay empty (the elevation pass fills them later).
/// </summary>
public static class Composer
{
    private const int ComposeAttempts = 60;
    private const int GrowAttemptsPerCrossing = 8;
    private const int HoleHuntAttempts = 16;

    public static PlanModel Compose(ComposeRequest request) => ComposeStages(request).Plan;

    public static ComposedStages ComposeStages(ComposeRequest request)
    {
        var rng = new ComposeRng(request.Seed);
        var envelope = Envelope.Derive(request, rng);

        // (p1) CT8: a closure hole per team side is the default, holelessness the sampled exception — most
        // composes hunt for a plan whose closure encloses a rotation pocket, keeping the first acceptable
        // holeless plan as the fallback. Tiny boards (no frontline budget) cannot form the recess and skip
        // the hunt (and its draw) entirely.
        var wantHole = envelope.LandPerTeam >= 800 && rng.NextBool(0.8);

        ComposedStages? fallback = null;
        for (var attempt = 0; attempt < ComposeAttempts; attempt++)
        {
            var crossing = MidCarver.SampleCrossing(envelope, rng);
            var unit = TeamUnitGrower.TryGrowUnit(envelope, rng, crossing, GrowAttemptsPerCrossing);
            if (unit is null) continue;
            var mid = MidCarver.TryCarve(envelope, rng, crossing, unit);
            if (mid is null) continue;
            var cut = IsolationCut.TryApply(envelope, rng, unit, mid);
            if (cut is not null) unit = new GrownUnit(cut.Pieces, unit.Spawn, unit.Wools);

            var plan = Assemble(request, envelope, unit, mid, cut);
            if (!Acceptable(plan, unit)) continue;

            // hunt for the sampled hole outcome (CT8): keep resampling for a plan whose hole state matches
            // wantHole — a rotation pocket when wantHole, a holeless closure when not — keeping the first
            // acceptable plan as the fallback and returning it once the hunt budget is spent.
            var stages = new ComposedStages(envelope, unit, crossing, mid, cut, plan);
            if ((ClosureAnalysis.HoleSizes(plan).Count > 0) == wantHole) return stages;
            fallback ??= stages;
            if (attempt + 1 >= HoleHuntAttempts) return fallback;
        }
        if (fallback is not null) return fallback;
        throw new ComposeException(
            $"composition could not assemble an acceptable plan within {ComposeAttempts} attempts " +
            $"(players {request.PlayersPerTeam}, teams {request.Teams}, symmetry '{request.Symmetry}', seed {request.Seed})");
    }

    // The final acceptance gate on the assembled plan: it must compile-validate with zero errors (every wool
    // reachable from every capturing spawn over the fanned board, no wool locked behind a spawn piece — SP1),
    // every gap-link hop must sit in G5's 10..20 band (the geometric constructions guarantee the designed
    // hops; this also catches any incidental link the zones create), the mid band must clear every
    // wool-carrying piece by two full cells across all orbit images (BZ6 — re-checked here because the cut
    // may move a wool after the band is carved), and no closure hole may be ringed by a wool plateau (a
    // wool-ringed hole is the two-approaches motif, WL8, which the grammar does not author).
    private static bool Acceptable(PlanModel plan, GrownUnit unit)
    {
        if (PlanValidator.Validate(plan).Any(f => f.Severity == PlanSeverity.Error)) return false;
        var derived = PlanDerived.Build(plan);
        if (derived.GapLinks.Any(g => g.Hop < 10 || g.Hop > 20)) return false;

        var woolPieces = unit.Wools.Select(w => w.Piece).ToHashSet();

        // BZ6: band ↔ wool clearance ≥ 2 cells, every image pair
        var order = Geom.Symmetry.Order(plan.Globals.Symmetry);
        var axes = Geom.Symmetry.OrbitAxes(plan.Globals.Symmetry);
        var band = plan.Zones.First(z => z.Id == "mid-band").Rect;
        var bandImages = Enumerable.Range(0, order)
            .Select(k => ComposeGeometry.FanImage(band[0], band[1], band[0] + band[2], band[1] + band[3], axes, k))
            .ToList();
        foreach (var piece in plan.Pieces.Where(p => woolPieces.Contains(p.Id)))
            for (var k = 0; k < order; k++)
            {
                var (px1, pz1, px2, pz2) = ComposeGeometry.FanImage(
                    piece.Rect[0], piece.Rect[1], piece.Rect[0] + piece.Rect[2], piece.Rect[1] + piece.Rect[3], axes, k);
                foreach (var b in bandImages)
                {
                    var ix = Math.Min(px2, b.X2) - Math.Max(px1, b.X1);
                    var iz = Math.Min(pz2, b.Z2) - Math.Max(pz1, b.Z1);
                    if (ix > -2 + 1e-9 && iz > -2 + 1e-9) return false;
                }
            }

        return !ClosureAnalysis.AnyHoleRingedBy(plan, woolPieces);
    }

    private static PlanModel Assemble(
        ComposeRequest request, ComposeEnvelope envelope, GrownUnit unit, MidResult mid, CutResult? cut)
    {
        var plan = new PlanModel
        {
            Meta = new PlanMeta { Name = $"Composed p{request.PlayersPerTeam} t{request.Teams} #{request.Seed}" },
            Globals = new PlanGlobals
            {
                Cell = envelope.Cell,
                Symmetry = envelope.Symmetry,
                MaxPlayers = request.PlayersPerTeam,
                Surface = envelope.Surface,
                Headroom = envelope.Headroom,
            },
        };

        foreach (var piece in unit.Pieces)
            plan.Pieces.Add(new PlanPiece { Id = piece.Id, Role = PlanRoles.Piece, Rect = piece.Rect });
        foreach (var stone in mid.Stones)
            plan.Pieces.Add(new PlanPiece
            {
                Id = stone.Id,
                Role = PlanRoles.Piece,
                Rect = stone.Rect,
                Surface = stone.Surface == envelope.Surface ? null : stone.Surface,
            });

        plan.Zones.Add(new PlanZone { Id = "mid-band", Rect = mid.BandRect });
        if (cut is not null)
            plan.Zones.Add(new PlanZone { Id = "bridge-a", Rect = cut.BridgeRect });

        plan.Placements.Spawns.Add(new SpawnPlacement
        {
            Piece = unit.Spawn.Piece, At = unit.Spawn.At, Facing = unit.Spawn.Facing,
        });
        foreach (var wool in unit.Wools)
            plan.Placements.Wools.Add(new WoolPlacement { Piece = wool.Piece, At = wool.At });

        return plan;
    }
}

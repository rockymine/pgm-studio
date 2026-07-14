using PgmStudio.Pgm.Evaluate;
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
/// assembled plan must pass the <see cref="LayoutEvaluator"/> hard-terms gate — no structural errors, no
/// WL2/PC-C/G2 lint, every void hop in G5's band, the mid band clear of every wool by two cells (BZ6), and no
/// closure hole ringed by a wool plateau (WL8) — or the whole attempt is resampled (an optional
/// <see cref="IComposeRejectSink"/> captures why). Cliffs and walls stay empty (the elevation pass fills them
/// later).
/// </summary>
public static class Composer
{
    private const int ComposeAttempts = 60;
    private const int GrowAttemptsPerCrossing = 8;
    private const int HoleHuntAttempts = 16;

    public static PlanModel Compose(ComposeRequest request, IComposeRejectSink? rejects = null) =>
        ComposeStages(request, rejects).Plan;

    public static ComposedStages ComposeStages(ComposeRequest request, IComposeRejectSink? rejects = null)
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

            // carve the terminal lanes into real spawn / wool-room pieces (the lane docks to the room) — after
            // the cut so a severed marker piece becomes its own isolated room (WL4/SP6), not a split island
            unit = SpawnWoolRooms.Carve(unit);

            var plan = Assemble(request, envelope, unit, mid, cut);
            var violation = LayoutEvaluator.Gate(EvalContext.Build(plan), EvaluationProfile.Default);
            if (violation is not null)
            {
                rejects?.Reject(new RejectRecord(
                    request.Seed, request.PlayersPerTeam, request.Teams, request.Symmetry, attempt, "acceptance",
                    violation.TermId, violation.RuleId, violation.Subjects));
                continue;
            }

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
            plan.Pieces.Add(new PlanPiece { Id = piece.Id, Role = piece.Role, Rect = piece.Rect });
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

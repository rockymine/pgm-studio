using PgmStudio.Pgm.Evaluate;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Compose;

/// <summary>Everything one compose attempt produced, kept apart so tests can gate each step separately.</summary>
public sealed record ComposedStages(
    ComposeEnvelope Envelope, GrownUnit Unit, CrossingDesign Crossing, MidResult Mid, PlanModel Plan);

/// <summary>
/// Composes a full <see cref="PlanModel"/> from nothing but a player count and team shape, through the
/// partition-first box pipeline: derive the board envelope (<see cref="Envelope"/>), fix the crossing
/// arithmetic (<see cref="MidCarver.BandOnly"/>), allocate the team unit's box partition under the front
/// guard (<see cref="TeamUnitAllocator"/>), fill it hub-first into labeled pieces + rooms
/// (<see cref="TeamUnitFiller"/>), carve the mid band (<see cref="MidCarver.TryCarve"/>), and assemble the
/// plan. The mid is one plain build band spanning the axis (uniform 20-block gap, no stones, no centre
/// island), docked <b>flush</b> against the unit's front faces — a flat front edge takes the build zone
/// straight against it — so the fanned board is two units connected by the band alone; richer mids (stones,
/// centre islands, the split band) layer back in on this path. Every attempt's assembled plan must pass the
/// <see cref="LayoutEvaluator"/> hard-terms gate — no structural errors, no WL2/PC-C/G2 lint, every void hop
/// in G5's band, the mid band clear of every wool by two cells (BZ6), and no closure hole ringed by a wool
/// plateau (WL8) — or the whole attempt is resampled (an optional <see cref="IComposeRejectSink"/> captures
/// why). Cliffs and walls stay empty (the elevation pass fills them later).
/// </summary>
public static class Composer
{
    private const int ComposeAttempts = 60;

    public static PlanModel Compose(ComposeRequest request, IComposeRejectSink? rejects = null) =>
        ComposeStages(request, rejects).Plan;

    public static ComposedStages ComposeStages(ComposeRequest request, IComposeRejectSink? rejects = null)
    {
        var rng = new ComposeRng(request.Seed);
        var envelope = Envelope.Derive(request, rng);
        var crossing = MidCarver.BandOnly(envelope);

        for (var attempt = 0; attempt < ComposeAttempts; attempt++)
        {
            if (TeamUnitAllocator.Allocate(envelope, rng, crossing) is not { } alloc) continue;
            if (TeamUnitFiller.Fill(alloc.Partition, alloc.SpawnFacing, rng) is not { } filled) continue;
            // parallel fronts: under a laterally-flipping symmetry the opposing image mirrors v, so the unit's
            // front faces must mirror onto themselves — else the two sides' fronts sit offset and the band
            // overflows past the faces it docks (an asymmetric-front hub form, an off-centre frontline) — resample
            if (MidCarver.LateralFlip(envelope.Symmetry) && !FrontFacesSymmetric(envelope, filled.Unit)) continue;
            var mid = MidCarver.TryCarve(envelope, crossing, filled.Unit);
            if (mid is null) continue;

            var plan = Assemble(request, envelope, filled.Unit, mid);
            var violation = LayoutEvaluator.Gate(EvalContext.Build(plan), EvaluationProfile.Default);
            if (violation is not null)
            {
                rejects?.Reject(new RejectRecord(
                    request.Seed, request.PlayersPerTeam, request.Teams, request.Symmetry, attempt, "acceptance",
                    violation.TermId, violation.RuleId, violation.Subjects));
                continue;
            }
            return new ComposedStages(envelope, filled.Unit, crossing, mid, plan);
        }
        throw new ComposeException(
            $"composition could not assemble an acceptable plan within {ComposeAttempts} attempts " +
            $"(players {request.PlayersPerTeam}, teams {request.Teams}, symmetry '{request.Symmetry}', seed {request.Seed})");
    }

    /// <summary>Whether the unit's front faces (the min-u pieces' lateral intervals) mirror onto themselves
    /// under v → -v — the parallel-fronts requirement of a laterally-flipping symmetry: only then does the
    /// opposing image's front align with the unit's own, and the band dock both flush without lateral
    /// overflow.</summary>
    private static bool FrontFacesSymmetric(ComposeEnvelope env, GrownUnit unit)
    {
        var frame = Frame.For(env.Symmetry);
        var uv = unit.Pieces.Select(p => frame.FromRect(p.Rect)).ToList();
        var minU = uv.Min(r => r.UMin);
        var faces = uv.Where(r => r.UMin == minU).Select(r => (Lo: r.VMin, Hi: r.VMin + r.VSpan)).ToHashSet();
        return faces.All(f => faces.Contains((-f.Hi, -f.Lo)));
    }

    private static PlanModel Assemble(
        ComposeRequest request, ComposeEnvelope envelope, GrownUnit unit, MidResult mid)
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

        plan.Placements.Spawns.Add(new SpawnPlacement
        {
            Piece = unit.Spawn.Piece, At = unit.Spawn.At, Facing = unit.Spawn.Facing,
        });
        foreach (var wool in unit.Wools)
            plan.Placements.Wools.Add(new WoolPlacement { Piece = wool.Piece, At = wool.At });

        return plan;
    }
}

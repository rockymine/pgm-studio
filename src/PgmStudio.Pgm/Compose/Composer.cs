using PgmStudio.Geom;
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
/// why). Walls stay empty — a defence wall is authored, never composed (review.md MG21).
/// </summary>
public static class Composer
{
    private const int ComposeAttempts = 60;

    /// <summary>The lateral slack the mid band may carry over the front it docks, in cells
    /// (<see cref="FrontHullSlackCells"/>). Zero admits every front whose hull is symmetric about the axis — the
    /// twin front with unequal legs, the asymmetric hub form — which a per-face test refuses. A small
    /// positive cap additionally admits a narrow front slid off-centre, at the cost of the band reaching past it
    /// by that much; the slack is <c>2 ×</c> the front hull's offset from the axis, so this allows one cell of
    /// offset per cell of cap, halved.
    ///
    /// <para>This bounds an <b>authored</b> plan (the BZ9 finding in <see cref="Producibility"/>), which may draw
    /// its front anywhere. The composer no longer consults it: it centres the unit on its face
    /// (<see cref="UnitPlacement.CentreFaceOnAxis"/>) and the face's span is even
    /// (<see cref="TeamUnitAllocator"/>), so a composed front's residual offset is at most half a cell — one cell
    /// of slack against a cap of four, which is why nothing resamples against it.</para></summary>
    internal const int FrontSlackCapCells = 4;

    public static PlanModel Compose(ComposeRequest request, IComposeRejectSink? rejects = null) =>
        ComposeStages(request, rejects).Plan;

    public static ComposedStages ComposeStages(ComposeRequest request, IComposeRejectSink? rejects = null)
    {
        var rng = new ComposeRng(request.Seed);
        var envelope = Envelope.Derive(request, rng);
        // whether this board wants a split mid, decided once for the board. Only drawn where the symmetry could
        // carry one, so a mirror board's sequence is untouched; the carve still grants it only if the face it
        // ends up with can host it, and a face that can is equally valid crossed by a single band.
        var crossing = MidCarver.BandOnly(envelope) with
        {
            SplitBand = MidCarver.LateralFlip(envelope.Symmetry) && rng.NextBool(MidCarver.SplitBandChance),
        };

        for (var attempt = 0; attempt < ComposeAttempts; attempt++)
        {
            if (TeamUnitAllocator.Allocate(envelope, rng, crossing) is not { } alloc) continue;
            if (TeamUnitFiller.Fill(alloc.Partition, alloc.SpawnFacing, rng) is not { } filled) continue;
            // place the finished unit rather than take where it was built: the allocator anchors on the hub, but
            // the face is what the mid docks, so re-anchor the unit on its face before the band is derived
            filled = filled with { Unit = UnitPlacement.CentreFaceOnAxis(filled.Unit, envelope.Symmetry) };
            // the parallel-fronts resample stood here: under a laterally-flipping symmetry a front sitting off
            // the axis makes the band overflow past the faces it docks, and a unit whose slack ran over the cap
            // was thrown away. Centring the unit on its face is what that guard was reaching for, so it now has
            // nothing left to catch — the residual offset is half a cell at most. The rule still binds authored
            // plans, which may draw a front anywhere; it is read back there (BZ9).
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

    /// <summary>
    /// The lateral <b>slack</b> the mid band carries because the unit's front faces are not centred on the axis,
    /// in cells — the parallel-fronts requirement, measured rather than assumed.
    ///
    /// <para><see cref="MidCarver.TryCarve"/> spans the band across the hull of <em>both</em> images' faces, so
    /// the band is self-symmetric by construction and an off-centre front never makes it impossible — it makes it
    /// <b>wider than the front it docks</b>, which is precisely what BZ9 bounds ("fit to the crossing it serves,
    /// no slack"). This measures that excess: zero when the faces' hull is symmetric about the axis, growing as
    /// the front slides off it.</para>
    ///
    /// <para>It replaces the older per-face mirror test (G123). That test asked every face to have its mirror
    /// among the faces — which only a centred front can satisfy, so it rejected every shifted or asymmetrically
    /// legged front outright, including ones whose hull is perfectly symmetric and which cost the band nothing.
    /// The hull is what the band actually reads.</para>
    ///
    /// <para>Takes the <paramref name="frame"/> rather than a symmetry string because a composed unit always grows
    /// on the +u side while an authored one may be drawn on either, and reading the front off the wrong side
    /// measures the back of the unit.</para>
    /// </summary>
    internal static int FrontHullSlackCells(Frame frame, IEnumerable<CellRect> rects)
    {
        var uv = rects.Select(frame.FromRect).ToList();
        if (uv.Count == 0) return 0;
        var minU = uv.Min(r => r.UMin);
        var faces = uv.Where(r => r.UMin == minU).Select(r => (Lo: r.VMin, Hi: r.VMin + r.VSpan)).ToList();
        int ownLo = faces.Min(f => f.Lo), ownHi = faces.Max(f => f.Hi);
        // the band takes the hull of the faces and their mirrors; its excess over the unit's own hull is the slack
        return Math.Max(ownHi, -ownLo) - Math.Min(ownLo, -ownHi) - (ownHi - ownLo);
    }

    internal static PlanModel Assemble(
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
            Piece = unit.Spawn.Piece, At = MarkerOffset(unit.Spawn.At, plan.Globals.Cell),
            Facing = unit.Spawn.Facing,
        });
        foreach (var wool in unit.Wools)
            plan.Placements.Wools.Add(new WoolPlacement
            {
                Piece = wool.Piece, At = MarkerOffset(wool.At, plan.Globals.Cell),
            });

        return plan;
    }

    // The emitters work in cells; a placement states blocks, so the conversion happens here, at the one
    // boundary where an emitted point becomes a stored offset.
    //
    // A marker must land on the block lattice with one parity on both axes (WX3): a grid line on both (the
    // 2×2 pad) or a block centre on both (3×3/1×1) — the room pad is always square. A room centre is mixed
    // exactly when the room is an odd number of blocks across on one axis only, so the emitters' centred
    // markers need the offending axis nudged half a block onto a grid line — the nearest legal point.
    internal static double[] MarkerOffset(double[] atCells, int cell)
    {
        double[] at = [atCells[0] * cell, atCells[1] * cell];
        bool OnGrid(double component) => component == Math.Floor(component);
        if (OnGrid(at[0]) == OnGrid(at[1])) return at;
        var axis = OnGrid(at[0]) ? 1 : 0;
        at[axis] = Math.Max(0, at[axis] - 0.5);
        return at;
    }
}

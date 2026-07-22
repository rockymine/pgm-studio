using FastEndpoints;
using PgmStudio.Contracts;
using PgmStudio.Data.Plan;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Evaluate;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Render;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// GET /api/compose — the browse feed. For each seed from the cursor it composes a board, reads its
/// <see cref="StructureSummary"/>, applies the <b>structural</b> sieve (wool families must-include; hub /
/// frontline forms any-of), then — for survivors only — evaluates, applies the score/wool sieve, and renders
/// the SVG. Structural classification on the tiny box masks is cheaper than evaluation, so structural rejects
/// skip the evaluator and the render entirely. The filters live wholly outside the compose call (never
/// aborting attempts mid-loop), so the same seed yields the same board under every filter and the descriptor's
/// reproduction promise — and the pin path — hold. Returns a page with the resume cursor, an exhausted flag,
/// and the seeds scanned (matched = card count); a low match rate under a strict filter is the signal to
/// promote it to a held target (G98). Teams fixed at 2 this pass; an unsupported symmetry is answered 400.
/// </summary>
public sealed class ComposeBrowseEndpoint : EndpointWithoutRequest
{
    private static readonly string[] Supported = ["rot_180", "mirror_z"];

    // A structural filter (donut ∧ L, say) can be a few percent of seeds, so bound the scan generously and
    // report what was scanned rather than hanging. Compose is milliseconds, so a few hundred stays responsive.
    private const int StructuralScanBudget = 400;

    public override void Configure() { Get("/compose"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var players = Query<int?>("players", isRequired: false) ?? 12;
        var symmetry = Query<string?>("symmetry", isRequired: false) ?? "rot_180";
        var cell = Query<int?>("cell", isRequired: false) ?? 5;
        var seedStart = Math.Max(0, Query<int?>("seedStart", isRequired: false) ?? 0);
        var count = Math.Clamp(Query<int?>("count", isRequired: false) ?? 12, 1, 48);
        var maxScore = Query<double?>("maxScore", isRequired: false);
        var woolMin = Query<int?>("woolMin", isRequired: false);
        var woolMax = Query<int?>("woolMax", isRequired: false);
        var woolReq = Csv("wools");    // approach families — must-include (each named present at least once)
        var hubReq = Csv("hub");       // hub form — any-of
        var frontReq = Csv("front");   // frontline form — any-of

        if (!Supported.Contains(symmetry))
        {
            await Send.ResponseAsync(new { error = $"unsupported symmetry '{symmetry}'" }, 400, ct);
            return;
        }

        var profile = EvaluationProfile.Default;
        var structural = woolReq.Count > 0 || hubReq.Count > 0 || frontReq.Count > 0;
        var cards = new List<ComposeCard>();
        var seed = seedStart;
        var scanCap = seedStart + (structural ? StructuralScanBudget : count * 4);
        var exhausted = false;

        while (cards.Count < count)
        {
            if (seed >= scanCap) { exhausted = true; break; }
            var s = (ulong)seed++;
            ct.ThrowIfCancellationRequested();

            ComposeRequest request;
            try { request = new ComposeRequest(players, 2, symmetry, s, cell); }
            catch (ArgumentException) { await Send.ResponseAsync(new { error = "invalid request parameters" }, 400, ct); return; }

            ComposedStages stages;
            try { stages = Composer.ComposeStages(request); }
            catch (ComposeException) { continue; }   // this seed produced no acceptable board — skip

            var summary = StructureSummary.Derive(stages.Unit);
            if (!StructuralPass(summary, woolReq, hubReq, frontReq)) continue;   // structural reject: no evaluate, no render

            var eval = LayoutEvaluator.Evaluate(stages.Plan, profile);
            var woolCount = stages.Plan.Placements.Wools.Count;
            if (maxScore is double mx && eval.Score > mx) continue;
            if (woolMin is int wmin && woolCount < wmin) continue;
            if (woolMax is int wmax && woolCount > wmax) continue;

            var hardTerms = eval.Terms
                .Where(t => t.Kind == TermKind.Hard && t.Violation is not null)
                .Select(t => t.TermId).ToList();
            var topSoft = eval.Terms
                .Where(t => t.Kind == TermKind.Soft && t.Distance > 0)
                .Select(t => new TermContribDto(t.TermId, t.Violation?.RuleId ?? "", profile.Weight(t.TermId) * t.Distance))
                .OrderByDescending(t => t.Contribution).Take(3).ToList();

            cards.Add(new ComposeCard(
                ToDto(ComposeDescriptor.For(request)), eval.Score, woolCount, ToDto(summary),
                hardTerms, topSoft, PlanBoardSvg.Render(stages.Plan)));
        }

        await Send.OkAsync(new ComposePage(cards, seed, exhausted, seed - seedStart), ct);
    }

    private List<string> Csv(string key) =>
        (Query<string?>(key, isRequired: false) ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(x => x.ToLowerInvariant()).ToList();

    // structural sieve: wools must-include (each named family present ≥ once), hub/front any-of the named forms
    private static bool StructuralPass(StructureSummary s, List<string> wools, List<string> hub, List<string> front)
    {
        if (wools.Count > 0)
        {
            var have = s.Wools.Select(StructureNames.Family).ToHashSet();
            if (!wools.All(have.Contains)) return false;
        }
        if (hub.Count > 0 && !hub.Contains(StructureNames.Form(s.Hub))) return false;
        if (front.Count > 0 && !front.Contains(StructureNames.Form(s.Frontline))) return false;
        return true;
    }

    internal static ComposeRequestDto ToDto(ComposeDescriptor d) =>
        new(d.PlayersPerTeam, d.Teams, d.Symmetry, d.Cell, d.Seed, d.ComposerVersion, d.Schema);

    internal static StructureSummaryDto ToDto(StructureSummary s) =>
        new(s.Wools.Select(StructureNames.Family).ToList(), StructureNames.Form(s.Hub), StructureNames.Form(s.Frontline));
}

/// <summary>
/// POST /api/compose/pin — keep a browse card. Re-composes the plan from its reproducible descriptor
/// (<see cref="Composer.ComposeStages"/>, so the structural bucket key comes for free) and saves it as a
/// generated row (<see cref="PlanStore.SaveGeneratedAsync"/>, idempotent by content hash) with its structure.
/// Returns the stored <see cref="PlanDetail"/>. The hold tray and unpin are the G119 endpoints.
/// </summary>
public sealed class ComposePinEndpoint(PlanStore store) : Endpoint<ComposeRequestDto>
{
    public override void Configure() { Post("/compose/pin"); AllowAnonymous(); }

    public override async Task HandleAsync(ComposeRequestDto req, CancellationToken ct)
    {
        ComposeRequest request;
        try { request = new ComposeRequest(req.Players, req.Teams, req.Symmetry, req.Seed, req.Cell); }
        catch (ArgumentException) { await Send.ResponseAsync(new { error = "invalid descriptor" }, 400, ct); return; }

        ComposedStages stages;
        try { stages = Composer.ComposeStages(request); }
        catch (ComposeException) { await Send.ResponseAsync(new { error = "composition failed for this descriptor" }, 422, ct); return; }

        var structure = StructureSummary.Derive(stages.Unit).Canonical();
        var row = await store.SaveGeneratedAsync(stages.Plan.ToJson(), ComposeDescriptor.For(request), structure, ct);
        await Send.OkAsync(PlanStoreMapping.ToDetail(row), ct);
    }
}

/// <summary>GET /api/plans/{id}/svg — render a stored plan to the same board SVG the browse feed uses, so the
/// hold tray can show a thumbnail of a persisted plan. 404 when the plan is missing.</summary>
public sealed class PlanSvgEndpoint(PlanStore store) : EndpointWithoutRequest
{
    public override void Configure() { Get("/plans/{id}/svg"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var row = await store.GetByIdAsync(Route<long>("id"), ct);
        if (row is null) { await Send.NotFoundAsync(ct); return; }
        var plan = PlanModel.Parse(row.PlanJson);
        if (plan is null) { await Send.ResponseAsync(new { error = "stored plan is unreadable" }, 422, ct); return; }
        await Send.OkAsync(new { svg = PlanBoardSvg.Render(plan) }, ct);
    }
}

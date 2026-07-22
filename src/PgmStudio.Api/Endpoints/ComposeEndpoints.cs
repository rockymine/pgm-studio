using FastEndpoints;
using PgmStudio.Contracts;
using PgmStudio.Data.Plan;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Evaluate;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Render;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// GET /api/compose — the browse feed. Composes boards ahead from a seed cursor, scores each with the
/// evaluator, keeps the ones passing the sieve (score threshold + wool count), renders each to an SVG, and
/// ships a page with the cursor to resume from (infinite scroll). The accepted plans are already gate-valid
/// (the composer only returns boards clearing the hard terms), so validity is not a filter axis. A card
/// carries its reproducible <see cref="ComposeDescriptor"/> rather than plan JSON — the plan is re-composed
/// server-side to pin or open it. Teams are fixed at 2 this pass; an unsupported symmetry is answered 400.
/// </summary>
public sealed class ComposeBrowseEndpoint : EndpointWithoutRequest
{
    // What the composer can actually produce today (2-team, laterally-flipping or z-mirror). rot_90/mirror_x
    // and the scythe are greyed out client-side and rejected here.
    private static readonly string[] Supported = ["rot_180", "mirror_z"];

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

        if (!Supported.Contains(symmetry))
        {
            await Send.ResponseAsync(new { error = $"unsupported symmetry '{symmetry}'" }, 400, ct);
            return;
        }

        var profile = EvaluationProfile.Default;
        var cards = new List<ComposeCard>();
        var seed = seedStart;
        var scanCap = seedStart + count * 4;   // bound the worst case when the sieve is strict
        var exhausted = false;

        while (cards.Count < count)
        {
            if (seed >= scanCap) { exhausted = true; break; }
            var s = (ulong)seed++;
            ct.ThrowIfCancellationRequested();

            ComposeRequest request;
            try { request = new ComposeRequest(players, 2, symmetry, s, cell); }
            catch (ArgumentException) { await Send.ResponseAsync(new { error = "invalid request parameters" }, 400, ct); return; }

            PlanModel plan;
            try { plan = Composer.Compose(request); }
            catch (ComposeException) { continue; }   // this seed produced no acceptable board — skip

            var eval = LayoutEvaluator.Evaluate(plan, profile);
            var woolCount = plan.Placements.Wools.Count;

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
                ToDto(ComposeDescriptor.For(request)), eval.Score, woolCount, hardTerms, topSoft, PlanBoardSvg.Render(plan)));
        }

        await Send.OkAsync(new ComposePage(cards, seed, exhausted), ct);
    }

    internal static ComposeRequestDto ToDto(ComposeDescriptor d) =>
        new(d.PlayersPerTeam, d.Teams, d.Symmetry, d.Cell, d.Seed, d.ComposerVersion, d.Schema);
}

/// <summary>
/// POST /api/compose/pin — keep a browse card. Re-composes the plan from its reproducible descriptor and
/// saves it as a generated row (<see cref="PlanStore.SaveGeneratedAsync"/>, idempotent by content hash), so
/// the hold tray survives reload. Returns the stored <see cref="PlanDetail"/>. The tray itself and unpin are
/// the G119 endpoints (<c>GET /api/plans?origin=generated</c>, <c>DELETE /api/plans/{id}</c>).
/// </summary>
public sealed class ComposePinEndpoint(PlanStore store) : Endpoint<ComposeRequestDto>
{
    public override void Configure() { Post("/compose/pin"); AllowAnonymous(); }

    public override async Task HandleAsync(ComposeRequestDto req, CancellationToken ct)
    {
        ComposeRequest request;
        try { request = new ComposeRequest(req.Players, req.Teams, req.Symmetry, req.Seed, req.Cell); }
        catch (ArgumentException) { await Send.ResponseAsync(new { error = "invalid descriptor" }, 400, ct); return; }

        PlanModel plan;
        try { plan = Composer.Compose(request); }
        catch (ComposeException) { await Send.ResponseAsync(new { error = "composition failed for this descriptor" }, 422, ct); return; }

        var row = await store.SaveGeneratedAsync(plan.ToJson(), ComposeDescriptor.For(request), ct);
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

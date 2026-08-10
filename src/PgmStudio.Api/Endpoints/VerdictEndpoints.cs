using System.Text;
using System.Text.Json;
using FastEndpoints;
using PgmStudio.Api.Http;
using PgmStudio.Contracts;
using PgmStudio.Data.Plan;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Evaluate;

namespace PgmStudio.Api.Endpoints;

/// <summary>The one serializer options instance for verdict snapshots and the export — web casing, so the
/// stored JSON and the wire read the same.</summary>
internal static class VerdictMapping
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
}

/// <summary>
/// POST /api/compose/verdict — cast or refine the up/down judgment of a browse board (G118). Voting is a
/// persistence act: the plan is re-composed from its descriptor and stored as a generated row (idempotent by
/// content hash, <b>unpinned</b> — a vote joins the labeled corpus without entering the hold tray; pinning
/// later promotes it). The row stores the vote, its tags and note, and the evaluator's reading at vote time —
/// score, per-term snapshot, evaluator version — so a downvote tagged with a rule whose term did not fire is
/// a ready-made evaluator bug report. One verdict per plan, refined in place on re-vote.
/// </summary>
public sealed class VerdictSaveEndpoint(PlanStore plans, VerdictStore verdicts) : Endpoint<VerdictSaveRequest>
{
    public override void Configure() { Post("/compose/verdict"); AllowAnonymous(); }

    public override async Task HandleAsync(VerdictSaveRequest req, CancellationToken ct)
    {
        if (req.Verdict is not ("up" or "down"))
        {
            await Send.ResponseAsync(new { error = "verdict must be 'up' or 'down'" }, 400, ct);
            return;
        }

        ComposeRequest request;
        try { request = new ComposeRequest(req.Descriptor.Players, req.Descriptor.Teams, req.Descriptor.Symmetry, req.Descriptor.Seed, req.Descriptor.Cell); }
        catch (ArgumentException) { await Send.ResponseAsync(new { error = "invalid descriptor" }, 400, ct); return; }

        ComposedStages stages;
        try { stages = Composer.ComposeStages(request); }
        catch (ComposeException) { await Send.ResponseAsync(new { error = "composition failed for this descriptor" }, 422, ct); return; }

        var structure = StructureSummary.Derive(stages.Unit).Canonical();
        // annotate exactly as the pin does — the stored document must be identical either way, or a pin and
        // a vote of the same board would hash to two rows and the judgment would miss the tray's copy
        PlanBoxAnnotation.Apply(stages.Plan, stages.Unit);
        var row = await plans.SaveGeneratedAsync(
            stages.Plan.ToJson(), ComposeDescriptor.For(request), structure, pinned: false, ct);

        var eval = LayoutEvaluator.Evaluate(stages.Plan, EvaluationProfile.Default);
        var stored = await verdicts.UpsertAsync(
            row.Id, req.Verdict,
            JsonSerializer.Serialize(req.Tags ?? [], VerdictMapping.Json),
            string.IsNullOrWhiteSpace(req.Note) ? null : req.Note,
            eval.Score, JsonSerializer.Serialize(eval.Terms, VerdictMapping.Json), EvaluatorVersion.Current, ct);

        await Send.OkAsync(VerdictListEndpoint.ToDto(stored, row), ct);
    }
}

/// <summary>GET /api/verdicts — every stored verdict, newest-refined first, each with its plan's descriptor
/// (the client keys its vote map by descriptor) and structural bucket key.</summary>
public sealed class VerdictListEndpoint(VerdictStore verdicts) : EndpointWithoutRequest<List<VerdictDto>>
{
    public override void Configure() { Get("/verdicts"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var rows = await verdicts.ListAsync(ct);
        await Send.OkAsync(rows.Select(x => ToDto(x.Verdict, x.Plan)).ToList(), ct);
    }

    internal static VerdictDto ToDto(PlanVerdictRow verdict, PlanRow plan) => new(
        verdict.PlanId, verdict.Verdict, ParseTags(verdict.TagsJson), verdict.Note,
        verdict.Score, verdict.EvaluatorVersion, plan.Structure,
        PlanStoreMapping.DescriptorOf(plan), verdict.UpdatedAt);

    private static IReadOnlyList<string> ParseTags(string tagsJson)
    {
        try { return JsonSerializer.Deserialize<List<string>>(tagsJson) ?? []; }
        catch (JsonException) { return []; }
    }
}

/// <summary>DELETE /api/verdicts/{planId} — retract the judgment of one plan (204). A plan that persisted
/// only for its vote (never pinned) goes with it; a pinned plan stays in the tray.</summary>
public sealed class VerdictDeleteEndpoint(PlanStore plans, VerdictStore verdicts) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/verdicts/{planId}"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var planId = Route<long>("planId");
        await verdicts.DeleteAsync(planId, ct);
        if (await plans.GetByIdAsync(planId, ct) is { Pinned: false }) await plans.DeleteAsync(planId, ct);
        await Send.NoContentAsync(ct);
    }
}

/// <summary>GET /api/verdicts/export — the labeled dataset as JSONL, one verdict per line: plan identity
/// (id, content hash, name, structure, composer version, descriptor), the vote (direction, tags, note), and
/// the evaluator snapshot (score, per-term reading, evaluator version). The export is what drives rule
/// refinement, envelope regeneration and offline analysis, so it carries everything the row knows.</summary>
public sealed class VerdictExportEndpoint(VerdictStore verdicts) : EndpointWithoutRequest
{
    public override void Configure() { Get("/verdicts/export"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var rows = await verdicts.ListAsync(ct);
        var lines = new StringBuilder();
        foreach (var (verdict, plan) in rows)
        {
            lines.Append(JsonSerializer.Serialize(new
            {
                planId = plan.Id,
                contentHash = plan.ContentHash,
                name = plan.Name,
                structure = plan.Structure,
                composerVersion = plan.ComposerVersion,
                descriptor = PlanStoreMapping.DescriptorOf(plan),
                verdict = verdict.Verdict,
                tags = JsonSerializer.Deserialize<List<string>>(verdict.TagsJson) ?? [],
                note = verdict.Note,
                score = verdict.Score,
                terms = JsonSerializer.Deserialize<JsonElement>(verdict.TermsJson),
                evaluatorVersion = verdict.EvaluatorVersion,
                createdAt = verdict.CreatedAt,
                updatedAt = verdict.UpdatedAt,
            }, VerdictMapping.Json));
            lines.Append('\n');
        }
        HttpContext.Response.ContentType = "application/x-ndjson; charset=utf-8";
        HttpContext.Response.Headers.ContentDisposition = ContentDispositionHeader.Attachment("verdicts.jsonl");
        await HttpContext.Response.WriteAsync(lines.ToString(), ct);
    }
}

using System.Text.Json;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Evaluate;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// POST /api/plan/inspect — the live derived-geometry feed for the plan editor's canvas overlays. The request
/// body is a plan wire document (<c>*.plan.json</c>); the response carries everything already resolved to block
/// coordinates so the canvas draws it directly: <c>interfaces</c> (land/narrow/corner contacts as segments),
/// <c>gapLinks</c> (zone-spanning connectors with the hop distance), <c>frontline</c> (piece edges facing a
/// zone) and <c>structures</c> (the boxes the world build will stamp — see <see cref="PlanStructurePreview"/> —
/// which the iso view draws). Rule findings/violations are the <c>/plan/evaluate</c> endpoint's job (the
/// evaluator is the single source). This is the derivation the Blazor client can't run itself, and the feed for
/// anything needing live geometry mid-edit: unlike <c>/plan/compile</c> it never withholds its answer over
/// structural errors. A malformed body is answered 400, never 500.
/// </summary>
public sealed class PlanInspectEndpoint : EndpointWithoutRequest
{
    public override void Configure() { Post("/plan/inspect"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(HttpContext.Request.Body);
        var body = await reader.ReadToEndAsync(ct);

        PlanModel? plan;
        try { plan = string.IsNullOrWhiteSpace(body) ? null : PlanModel.Parse(body); }
        catch (JsonException) { plan = null; }
        if (plan is null) { await Send.ResponseAsync(new { error = "Malformed plan JSON" }, 400, ct); return; }

        ContactGraph d;
        try
        {
            d = ContactGraph.Build(plan);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NullReferenceException or IndexOutOfRangeException)
        {
            await Send.ResponseAsync(new { error = "Invalid plan structure" }, 400, ct);
            return;
        }

        var interfaces = d.InterfaceSegments.Select(s => new
        {
            a = s.A, b = s.B, kind = s.Kind.ToString().ToLowerInvariant(),
            x1 = s.X1, z1 = s.Z1, x2 = s.X2, z2 = s.Z2, length = s.Length,
            woolRoom = s.WoolRoom, wall = s.Wall,
        });

        var gapLinks = d.GapLinks.Select(g =>
        {
            var (x1, z1, x2, z2) = ContactGraph.NearestSegment(d.Piece(g.A)!.Value.Rect, d.Piece(g.B)!.Value.Rect);
            return new { a = g.A, b = g.B, zone = g.Zone, hop = g.Hop, x1, z1, x2, z2 };
        });

        var frontline = d.FrontlineEdges.Select(f => new { piece = f.Piece, x1 = f.X1, z1 = f.Z1, x2 = f.X2, z2 = f.Z2 });

        // The boxes the world build will stamp (the iso view draws them). A plan mid-edit is routinely
        // incomplete, so a compile failure degrades to no structures rather than failing the whole feed —
        // the derived-geometry overlays above stay live either way.
        IReadOnlyList<StructureBox> structures;
        try { structures = PlanStructurePreview.Build(plan); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NullReferenceException or IndexOutOfRangeException)
        {
            structures = [];
        }

        await Send.OkAsync(new { interfaces, gapLinks, frontline, structures }, ct);
    }
}

/// <summary>
/// POST /api/plan/compile — compile a plan wire document one-way into the pair the draft pipeline consumes:
/// <c>{ layout: &lt;SketchLayout&gt;, intent: &lt;MapIntent&gt; }</c>. Structural validator <b>errors</b> block the
/// compile (422, carrying the error findings); lint alone never blocks. A malformed body is answered 400, never
/// 500. The two sub-objects are serialized with the exact options the downstream <c>PUT /map/{slug}/sketch</c>
/// and <c>PUT /map/{slug}/intent</c> endpoints read back, so the editor's walk-test loop can post them verbatim.
/// </summary>
public sealed class PlanCompileEndpoint : EndpointWithoutRequest
{
    public override void Configure() { Post("/plan/compile"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(HttpContext.Request.Body);
        var body = await reader.ReadToEndAsync(ct);

        PlanModel? plan;
        try { plan = string.IsNullOrWhiteSpace(body) ? null : PlanModel.Parse(body); }
        catch (JsonException) { plan = null; }
        if (plan is null) { await Send.ResponseAsync(new { error = "Malformed plan JSON" }, 400, ct); return; }

        SketchLayout layout;
        MapIntent intent;
        try
        {
            var errors = PlanValidator.Validate(plan).Where(f => f.Severity == PlanSeverity.Error).ToList();
            if (errors.Count > 0)
            {
                var findings = errors.Select(f => new
                {
                    severity = "error", rule = f.Rule, message = f.Message, subjects = f.SubjectIds,
                });
                await Send.ResponseAsync(new { findings }, 422, ct);
                return;
            }
            (layout, intent) = PlanCompiler.Compile(plan);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NullReferenceException or IndexOutOfRangeException)
        {
            await Send.ResponseAsync(new { error = "Invalid plan structure" }, 400, ct);
            return;
        }

        // Serialize each half with its own consumer's options (snake_case shape fields for the sketch blob;
        // Web camelCase for the intent) so the editor can post the raw sub-objects straight to the pipeline.
        var layoutEl = JsonSerializer.SerializeToElement(layout, SketchLayout.Json);
        var intentEl = JsonSerializer.SerializeToElement(intent, IntentStore.Json);
        await Send.OkAsync(new { layout = layoutEl, intent = intentEl }, ct);
    }
}

/// <summary>
/// POST /api/plan/evaluate — the plan editor's live rule-evaluator score + lint feed (the critic that scores a
/// <c>*.plan.json</c>). The request body is a plan wire document; the response is an <see cref="EvaluationDto"/>:
/// the summed <c>score</c> (lower is better, 0 perfect), a <c>valid</c> flag (no hard term fired), and every
/// fired term ordered hard-first — each carrying its <c>layout-rules.md</c> id, the pieces/zones it indicts, and
/// the cell-space <see cref="EvidenceDto"/> the canvas overlay paints. This is where soft "feel" terms and the
/// gate terms retired from the structural validator (e.g. WL2 spawn↔wool distance) surface in the editor, so it
/// complements — not replaces — <c>/plan/inspect</c>'s derived-structure geometry. A malformed body is answered
/// 400, never 500.
/// </summary>
public sealed class PlanEvaluateEndpoint : EndpointWithoutRequest
{
    public override void Configure() { Post("/plan/evaluate"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(HttpContext.Request.Body);
        var body = await reader.ReadToEndAsync(ct);

        PlanModel? plan;
        try { plan = string.IsNullOrWhiteSpace(body) ? null : PlanModel.Parse(body); }
        catch (JsonException) { plan = null; }
        if (plan is null) { await Send.ResponseAsync(new { error = "Malformed plan JSON" }, 400, ct); return; }

        Evaluation eval;
        try
        {
            eval = LayoutEvaluator.Evaluate(plan, EvaluationProfile.Default);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NullReferenceException or IndexOutOfRangeException)
        {
            await Send.ResponseAsync(new { error = "Invalid plan structure" }, 400, ct);
            return;
        }

        await Send.OkAsync(ToDto(eval), ct);
    }

    /// <summary>Map the derived <see cref="Evaluation"/> onto the wire DTO: every fired term (hard-first, the
    /// registration order the evaluation already carries) with its kind, soft distance and flattened evidence.</summary>
    internal static EvaluationDto ToDto(Evaluation eval)
    {
        var violations = eval.Terms
            .Where(t => t.Violation is not null)
            .Select(t => new ViolationDto(
                t.Violation!.TermId, t.Violation.RuleId, t.Kind == TermKind.Hard ? "hard" : "soft",
                t.Distance, t.Violation.Message, t.Violation.Subjects,
                (t.Violation.Evidence ?? []).Select(MapEvidence).ToList()))
            .ToList();
        return new EvaluationDto(eval.Score, eval.IsValid, violations);
    }

    private static EvidenceDto MapEvidence(Evidence e) => e switch
    {
        EvidenceRect r => new("rect", r.Tag, Rect: r.Rect),
        EvidenceSegment s => new("segment", s.Tag, X1: s.X1, Z1: s.Z1, X2: s.X2, Z2: s.Z2),
        EvidenceMarker m => new("marker", m.Tag, X: m.X, Z: m.Z),
        EvidenceMeasure m => new("measure", m.Tag, X1: m.X1, Z1: m.Z1, X2: m.X2, Z2: m.Z2, Label: m.Label),
        _ => new("unknown", e.Tag),
    };
}

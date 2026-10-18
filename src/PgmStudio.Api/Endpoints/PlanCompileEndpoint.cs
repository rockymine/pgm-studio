using System.Text.Json;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// POST /api/plan/compile — compile a plan wire document one-way into the pair a map is built from:
/// <c>{ layout: &lt;SketchLayout&gt;, intent: &lt;MapIntent&gt; }</c>. Structural validator <b>errors</b> block the
/// compile (422, carrying the error findings); lint alone never blocks. A malformed body is answered 400, never
/// 500. The two sub-objects are serialized with the exact options <c>PUT /map/{slug}/source</c>,
/// <c>PUT /map/{slug}/sketch</c> and <c>PUT /map/{slug}/intent</c> read back, so a caller posts them verbatim.
/// </summary>
public sealed class PlanCompileEndpoint : EndpointWithoutRequest<CompiledPlanDto>
{
    public override void Configure()
    {
        Post("/plan/compile");
        Description(b => b.Accepts<PlanModel>("application/json").Refuses(422));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var body = await RawBody.ReadAsync(HttpContext, ct);

        var plan = PlanModel.Stated(body);
        if (plan is null)
        {
            await Refusals.NotAPlanAsync(HttpContext, body, ct);
            return;
        }
        // The one-way gate is where a plan stops being editable text, so a field the compiler had nowhere to
        // read is said here rather than discovered as ground the built map does not have.
        Complaints.Unread(HttpContext, body, plan);

        var compiled = PlanCompile.Run(plan);
        if (compiled.Refusal is { } refused)
        {
            await Refusals.WriteAsync(HttpContext, refused, ct);
            return;
        }

        // Serialize each half with its own consumer's options (snake_case shape fields for the sketch blob;
        // Web camelCase for the intent) so the caller can post the raw sub-objects straight on.
        var layoutEl = JsonSerializer.SerializeToElement(compiled.Layout, SketchLayout.Json);
        var intentEl = JsonSerializer.SerializeToElement(compiled.Intent, MapArtifactStore.Json);

        // Completeness complaints that did not block (today: no objective), carried on the success response so a
        // compile that produced a playable-but-goalless map still says so rather than passing in silence.
        Complaints.Add(HttpContext, compiled.Complaints ?? Findings.None);
        await Send.OkAsync(new CompiledPlanDto(layoutEl, intentEl), ct);
    }
}

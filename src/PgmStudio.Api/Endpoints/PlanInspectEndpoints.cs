using System.Text.Json;
using FastEndpoints;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// POST /api/plan/inspect — the live derived-structure + lint feed for the plan editor. The request body is a
/// plan wire document (<c>*.plan.json</c>); the response carries everything already resolved to block
/// coordinates so the canvas draws it directly: <c>findings</c> (errors then rule lint, each with the
/// implicated subject ids), <c>interfaces</c> (land/sliver/corner contacts as segments), <c>gapLinks</c>
/// (zone-spanning connectors with the hop distance) and <c>frontline</c> (piece edges facing a zone). The
/// canonical validator/derivation runs server-side because the Blazor client can't reference the plan library.
/// A malformed body is answered 400, never 500.
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

        PlanDerived d;
        IReadOnlyList<PlanFinding> raw;
        try
        {
            d = PlanDerived.Build(plan);
            raw = PlanValidator.Validate(plan);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NullReferenceException or IndexOutOfRangeException)
        {
            await Send.ResponseAsync(new { error = "Invalid plan structure" }, 400, ct);
            return;
        }

        var findings = raw.Select(f => new
        {
            severity = f.Severity == PlanSeverity.Error ? "error" : "lint",
            rule = f.Rule,
            message = f.Message,
            subjects = f.SubjectIds,
        });

        var interfaces = d.InterfaceSegments.Select(s => new
        {
            a = s.A, b = s.B, kind = s.Kind.ToString().ToLowerInvariant(),
            x1 = s.X1, z1 = s.Z1, x2 = s.X2, z2 = s.Z2, length = s.Length,
        });

        var gapLinks = d.GapLinks.Select(g =>
        {
            var (x1, z1, x2, z2) = PlanDerived.NearestSegment(d.Piece(g.A)!.Value.Rect, d.Piece(g.B)!.Value.Rect);
            return new { a = g.A, b = g.B, zone = g.Zone, hop = g.Hop, x1, z1, x2, z2 };
        });

        var frontline = d.FrontlineEdges.Select(f => new { piece = f.Piece, x1 = f.X1, z1 = f.Z1, x2 = f.X2, z2 = f.Z2 });

        await Send.OkAsync(new { findings, interfaces, gapLinks, frontline }, ct);
    }
}

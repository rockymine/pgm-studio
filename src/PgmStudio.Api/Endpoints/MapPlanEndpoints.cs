using System.Text;
using System.Text.Json;
using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Render;

namespace PgmStudio.Api.Endpoints;

/// <summary>POST /api/plan — originate a blank authored plan: create a <c>map</c> row at <c>stage=plan</c>
/// with an empty <c>plan_json</c> artifact (no candidate provenance). Returns the slug; the client navigates
/// to <c>/maps/{slug}/plan</c>, where the editor keeps its default blank document until first save. Body:
/// optional {name}. The generator's <c>plan</c> candidate rows are a separate pool; authoring one forks it
/// into a map here.</summary>
public sealed class PlanCreateEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest
{
    public override void Configure() { Post("/plan"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var name = "Untitled plan";
        try
        {
            using var doc = await JsonDocument.ParseAsync(HttpContext.Request.Body, cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                && n.GetString() is { } s && !string.IsNullOrWhiteSpace(s)) name = s.Trim();
        }
        catch { /* empty / invalid body → default name */ }

        var slug = await SketchSlug.UniqueAsync(repo, SketchSlug.Slugify(name), ct);
        var now = DateTime.UtcNow;
        var mapId = await repo.InsertAsync(new MapRow
        {
            Slug = slug, Name = name, Gamemode = "ctw", Stage = MapStage.Plan, CreatedAt = now, UpdatedAt = now,
        });
        await artifacts.SaveAsync(mapId, ArtifactKind.PlanJson, "{}"u8.ToArray(), ct);
        await Send.OkAsync(new { slug }, ct);
    }
}

/// <summary>POST /api/plan/{planId}/author — commit a generator plan candidate to authoring: create a
/// <c>map</c> row at <c>stage=plan</c> seeded with the candidate's <c>plan_json</c> (a <c>plan_json</c>
/// artifact) and a <c>plan_source_id</c> back to the candidate. Returns the slug; the client navigates to
/// <c>/maps/{slug}/plan</c>. 404 if the candidate doesn't exist.</summary>
public sealed class AuthorPlanEndpoint(MapRepository repo, PgmDb db, MapArtifactStore artifacts) : EndpointWithoutRequest
{
    public override void Configure() { Post("/plan/{planId}/author"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var planId = Route<long>("planId");
        var candidate = await db.Plans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (candidate is null) { await Refusals.NotFoundAsync(HttpContext, "stored plan", ct); return; }

        var name = string.IsNullOrWhiteSpace(candidate.Name) ? "Untitled plan" : candidate.Name.Trim();
        var slug = await SketchSlug.UniqueAsync(repo, SketchSlug.Slugify(string.IsNullOrWhiteSpace(name) ? "plan" : name), ct);
        var now = DateTime.UtcNow;
        var mapId = await repo.InsertAsync(new MapRow
        {
            Slug = slug, Name = name, Gamemode = "ctw", Stage = MapStage.Plan,
            PlanSourceId = candidate.Id, CreatedAt = now, UpdatedAt = now,
        });
        await artifacts.SaveAsync(mapId, ArtifactKind.PlanJson, Encoding.UTF8.GetBytes(candidate.PlanJson), ct);
        await Send.OkAsync(new { slug }, ct);
    }
}

/// <summary>GET /api/map/{slug}/plan — the stored plan blob for a plan-stage map, or {} if none.</summary>
public sealed class MapPlanGetEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/plan"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }
        var data = await artifacts.LoadAsync(map.Id, ArtifactKind.PlanJson, ct);
        await Send.OkAsync(JsonSerializer.Deserialize<JsonElement>(data ?? "{}"u8.ToArray()), ct);
    }
}

/// <summary>GET /api/map/{slug}/plan/ascii — the stored plan as a grid of characters, one per proxy cell.
/// The map-scoped twin of <c>GET /plans/{id}/ascii</c>: the same render, reached by the slug an authoring
/// driver already holds rather than by a candidate id. <c>?every=N</c> downsamples. 404 when the map or its
/// plan is missing, 422 when the stored document cannot be read.</summary>
public sealed class MapPlanAsciiEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/map/{slug}/plan/ascii");
        AllowAnonymous();
        Description(b => b.PlainText());
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }
        var data = await artifacts.LoadAsync(map.Id, ArtifactKind.PlanJson, ct);
        if (data is null) { await Refusals.NotFoundAsync(HttpContext, "stored plan", ct); return; }

        var plan = PlanModel.Parse(Encoding.UTF8.GetString(data));
        if (plan is null) { await Refusals.StoredUnreadableAsync(HttpContext, "plan", ct); return; }

        HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await HttpContext.Response.WriteAsync(PlanBoardAscii.Render(plan, every: Query<int?>("every", false) ?? 1), ct);
    }
}

/// <summary>GET /api/map/{slug}/plan/flow — what the board asks of the two sides, and what that leaves
/// unused, in prose.
///
/// <para>The companion to the coverage picture and meant to be read <b>before</b> it: a render says where
/// ground is dead and only the flow says why. It answers off the plan alone, so it costs no build.</para>
///
/// <para>Routes are read for a capture board only — a wool is carried back, so the two sides meet somewhere
/// definite and a split, a merge and the defender's relation to them mean something. A destroy board gets
/// the ground read and no invented flow. 404 when the map or its plan is missing, 422 when the stored
/// document cannot be read.</para></summary>
public sealed class MapPlanFlowEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/map/{slug}/plan/flow");
        AllowAnonymous();
        Description(b => b.PlainText());
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }
        var data = await artifacts.LoadAsync(map.Id, ArtifactKind.PlanJson, ct);
        if (data is null) { await Refusals.NotFoundAsync(HttpContext, "stored plan", ct); return; }

        var plan = PlanModel.Parse(Encoding.UTF8.GetString(data));
        if (plan is null) { await Refusals.StoredUnreadableAsync(HttpContext, "plan", ct); return; }

        HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await HttpContext.Response.WriteAsync(PlanFlow.Describe(PlanFlow.Read(plan)), ct);
    }
}

/// <summary>PUT /api/map/{slug}/plan — replace the stored plan blob (the plan editor's saved state).</summary>
public sealed class MapPlanPutEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest
{
    public override void Configure() { Put("/map/{slug}/plan"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }

        using var ms = new MemoryStream();
        await HttpContext.Request.Body.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        try { using var _ = JsonDocument.Parse(bytes); }   // reject non-JSON; don't store garbage
        catch (JsonException fault)
        { await Refusals.UnreadableAsync(HttpContext, "invalid JSON", fault.Message, ct); return; }

        // The blob is stored verbatim, so a field the plan reader has nowhere to keep is stored too and
        // silently ignored by everything downstream. Said here, where the author can still act on it.
        var planJson = Encoding.UTF8.GetString(bytes);
        Complaints.Unread(HttpContext, planJson, PlanModel.Stated(planJson));

        await artifacts.SaveAsync(map.Id, ArtifactKind.PlanJson, bytes, ct);
        await Send.OkAsync(new { ok = true }, ct);
    }
}

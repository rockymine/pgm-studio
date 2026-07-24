using System.Text;
using System.Text.Json;
using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// Plan-as-a-map persistence (docs/contracts/plan-as-map.md): the <c>plan_json</c> artifact backing a
/// map at <c>stage=plan</c>. Mirrors <see cref="SketchStore"/> — it lives outside the entity-replace codec.
/// The generator's <c>plan</c> candidate rows are a separate pool; authoring one forks it into a map here.
/// </summary>
internal static class MapPlanStore
{
    public static async Task<byte[]?> LoadAsync(PgmDb db, long mapId, CancellationToken ct)
    {
        var art = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == mapId && a.Kind == ArtifactKind.PlanJson, ct);
        return art?.Data;
    }

    public static async Task SaveAsync(PgmDb db, long mapId, byte[] data, CancellationToken ct)
    {
        await db.Artifacts.Where(a => a.MapId == mapId && a.Kind == ArtifactKind.PlanJson).DeleteAsync(ct);
        await db.InsertAsync(new MapArtifactRow { MapId = mapId, Kind = ArtifactKind.PlanJson, Data = data }, token: ct);
    }
}

/// <summary>POST /api/plan — originate a blank authored plan: create a <c>map</c> row at <c>stage=plan</c>
/// with an empty <c>plan_json</c> artifact (no candidate provenance). Returns the slug; the client navigates
/// to <c>/maps/{slug}/plan</c>, where the editor keeps its default blank document until first save. Body:
/// optional {name}.</summary>
public sealed class PlanCreateEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
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
        await MapPlanStore.SaveAsync(db, mapId, "{}"u8.ToArray(), ct);
        await Send.OkAsync(new { slug }, ct);
    }
}

/// <summary>POST /api/plan/{planId}/author — commit a generator plan candidate to authoring: create a
/// <c>map</c> row at <c>stage=plan</c> seeded with the candidate's <c>plan_json</c> (a <c>plan_json</c>
/// artifact) and a <c>plan_source_id</c> back to the candidate. Returns the slug; the client navigates to
/// <c>/maps/{slug}/plan</c>. 404 if the candidate doesn't exist.</summary>
public sealed class AuthorPlanEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Post("/plan/{planId}/author"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var planId = Route<long>("planId");
        var candidate = await db.Plans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (candidate is null) { await Send.NotFoundAsync(ct); return; }

        var name = string.IsNullOrWhiteSpace(candidate.Name) ? "Untitled plan" : candidate.Name.Trim();
        var slug = await SketchSlug.UniqueAsync(repo, SketchSlug.Slugify(string.IsNullOrWhiteSpace(name) ? "plan" : name), ct);
        var now = DateTime.UtcNow;
        var mapId = await repo.InsertAsync(new MapRow
        {
            Slug = slug, Name = name, Gamemode = "ctw", Stage = MapStage.Plan,
            PlanSourceId = candidate.Id, CreatedAt = now, UpdatedAt = now,
        });
        await MapPlanStore.SaveAsync(db, mapId, Encoding.UTF8.GetBytes(candidate.PlanJson), ct);
        await Send.OkAsync(new { slug }, ct);
    }
}

/// <summary>GET /api/map/{slug}/plan — the stored plan blob for a plan-stage map, or {} if none.</summary>
public sealed class MapPlanGetEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/plan"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }
        var data = await MapPlanStore.LoadAsync(db, map.Id, ct);
        await Send.OkAsync(JsonSerializer.Deserialize<JsonElement>(data ?? "{}"u8.ToArray()), ct);
    }
}

/// <summary>PUT /api/map/{slug}/plan — replace the stored plan blob (the plan editor's saved state).</summary>
public sealed class MapPlanPutEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Put("/map/{slug}/plan"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        using var ms = new MemoryStream();
        await HttpContext.Request.Body.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        try { using var _ = JsonDocument.Parse(bytes); }   // reject non-JSON; don't store garbage
        catch { await Send.ResponseAsync(new { error = "invalid JSON" }, 400, ct); return; }

        await MapPlanStore.SaveAsync(db, map.Id, bytes, ct);
        await Send.OkAsync(new { ok = true }, ct);
    }
}

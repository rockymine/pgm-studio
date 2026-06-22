using System.Text.Json;
using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// Backend for the lane-decomposition surface: the human cuts each map's simplified island outline
/// (<c>island_sketch_json</c>) into lane polygons and saves them as <c>lane_decomposition_json</c>. The
/// original outline is kept untouched (the diff); the presence of a saved decomposition marks a map "done"
/// and drops it from the queue.
/// </summary>
/// <summary>GET /api/decompose/queue — the still-to-do two-team CTW maps (have a simplified island outline,
/// no saved decomposition yet), plus the done count for progress.</summary>
public sealed class DecomposeQueueEndpoint(PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/decompose/queue"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Two-team CTW maps with an island outline; the lane-decomposition subqueries are inlined so
        // LinqToDB can translate them to correlated SQL EXISTS clauses.
        var eligible = db.Maps.Where(m => m.Gamemode == "ctw"
            && db.Teams.Count(t => t.MapId == m.Id) == 2
            && db.Artifacts.Any(a => a.MapId == m.Id && a.Kind == ArtifactKind.IslandSketchJson));

        var todo = await eligible
            .Where(m => !db.Artifacts.Any(a => a.MapId == m.Id && a.Kind == ArtifactKind.LaneDecompositionJson))
            .OrderBy(m => m.Slug)
            // surface the island-sketch review flag (G9) alongside each queued map, so a reviewer can
            // triage the ones whose detected outline looks wrong before cutting lanes from it.
            .Select(m => new
            {
                slug = m.Slug,
                name = m.Name,
                review = db.Artifacts.Where(a => a.MapId == m.Id && a.Kind == ArtifactKind.IslandReviewJson)
                    .Select(a => a.Data).FirstOrDefault(),
            })
            .ToListAsync(ct);
        var done = await eligible.CountAsync(
            m => db.Artifacts.Any(a => a.MapId == m.Id && a.Kind == ArtifactKind.LaneDecompositionJson), ct);
        var items = todo.Select(t => new
        {
            t.slug, t.name,
            reviewStatus = ReviewStatus(t.review),
        }).ToList();
        await Send.OkAsync(new { todo = items, remaining = items.Count, done, flagged = items.Count(i => i.reviewStatus is not null) }, ct);
    }

    private static string? ReviewStatus(byte[]? data)
    {
        if (data is null || data.Length == 0) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(data);
            return doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
        }
        catch { return null; }
    }
}

/// <summary>GET /api/map/{slug}/island-sketch — the original Douglas-Peucker simplified island outlines
/// (the shape to cut), or {} if none.</summary>
public sealed class IslandSketchGetEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/island-sketch"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }
        var art = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == map.Id && a.Kind == ArtifactKind.IslandSketchJson, ct);
        await Send.OkAsync(JsonSerializer.Deserialize<JsonElement>(art?.Data ?? "{}"u8.ToArray()), ct);
    }
}

/// <summary>GET /api/map/{slug}/lane-decomposition — the saved lane decomposition (to resume), or {}.</summary>
public sealed class LaneDecompositionGetEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/lane-decomposition"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }
        var art = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == map.Id && a.Kind == ArtifactKind.LaneDecompositionJson, ct);
        await Send.OkAsync(JsonSerializer.Deserialize<JsonElement>(art?.Data ?? "{}"u8.ToArray()), ct);
    }
}

/// <summary>PUT /api/map/{slug}/lane-decomposition — save the human's cut lanes (the SketchLayout-format
/// blob). Marks the map done (drops it from the queue). Rejects non-JSON.</summary>
public sealed class LaneDecompositionPutEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Put("/map/{slug}/lane-decomposition"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        using var ms = new MemoryStream();
        await HttpContext.Request.Body.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        try { using var _ = JsonDocument.Parse(bytes); }
        catch { await Send.ResponseAsync(new { error = "invalid JSON" }, 400, ct); return; }

        await db.Artifacts.Where(a => a.MapId == map.Id && a.Kind == ArtifactKind.LaneDecompositionJson).DeleteAsync(ct);
        await db.InsertAsync(new MapArtifactRow { MapId = map.Id, Kind = ArtifactKind.LaneDecompositionJson, Data = bytes }, token: ct);
        await Send.OkAsync(new { ok = true }, ct);
    }
}

/// <summary>DELETE /api/map/{slug}/lane-decomposition — discard the saved cut lanes, returning the map to the
/// queue (the original island sketch is untouched, so the author can re-cut from scratch).</summary>
public sealed class LaneDecompositionDeleteEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/map/{slug}/lane-decomposition"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }
        await db.Artifacts.Where(a => a.MapId == map.Id && a.Kind == ArtifactKind.LaneDecompositionJson).DeleteAsync(ct);
        await Send.OkAsync(new { ok = true }, ct);
    }
}

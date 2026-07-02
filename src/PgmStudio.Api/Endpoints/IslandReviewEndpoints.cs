using System.Text.Json;
using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using NetTopologySuite.Geometries;
using PgmStudio.Analysis.Footprint;
using PgmStudio.Api.Services;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// Review flag for a map whose auto-detected island sketch looks wrong. The flag is human-set;
/// <see cref="IslandHealthEndpoint"/> offers an automatic second opinion for the reliably-detectable
/// failure mode (merged teams), so a reviewer can prioritise what to re-check.
/// </summary>
public static class IslandReview
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public sealed class Flag
    {
        /// <summary>"ok" · "suspect" · "fixed" — free-form but these three drive the queue badge.</summary>
        public string Status { get; set; } = "suspect";
        public string Note { get; set; } = "";
        public string? At { get; set; }
    }

    public static async Task<Flag?> LoadAsync(PgmDb db, long mapId, CancellationToken ct)
    {
        var art = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == mapId && a.Kind == ArtifactKind.IslandReviewJson, ct);
        return art is null ? null : JsonSerializer.Deserialize<Flag>(art.Data, Json);
    }
}

/// <summary>GET /api/map/{slug}/island-review — the reviewer flag, or {} when none is set.</summary>
public sealed class IslandReviewGetEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/island-review"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }
        var flag = await IslandReview.LoadAsync(db, map.Id, ct);
        await Send.OkAsync((object?)flag ?? new { }, ct);
    }
}

/// <summary>PUT /api/map/{slug}/island-review — set (or with status "ok"/empty, clear) the reviewer flag.</summary>
public sealed class IslandReviewPutEndpoint(MapRepository repo, PgmDb db) : Endpoint<IslandReview.Flag>
{
    public override void Configure() { Put("/map/{slug}/island-review"); AllowAnonymous(); }

    public override async Task HandleAsync(IslandReview.Flag req, CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        await db.Artifacts.Where(a => a.MapId == map.Id && a.Kind == ArtifactKind.IslandReviewJson).DeleteAsync(ct);
        // "ok"/blank status means "nothing to review" → leave the flag cleared (drops it from the queue).
        if (!string.IsNullOrWhiteSpace(req.Status) && req.Status != "ok")
        {
            req.At = DateTime.UtcNow.ToString("O");
            await db.InsertAsync(new MapArtifactRow
            {
                MapId = map.Id, Kind = ArtifactKind.IslandReviewJson,
                Data = JsonSerializer.SerializeToUtf8Bytes(req, IslandReview.Json),
            }, token: ct);
        }
        await Send.OkAsync(new { ok = true }, ct);
    }
}

/// <summary>
/// GET /api/map/{slug}/island-health — an automatic read of the detected islands. Reports each island's
/// <b>gameplay role</b> (team / objective / neutral / decorative) from the objective anchors it carries
/// (spawn + wool, never the monument) and the buildable region, with a size bucket (major/neutral/small) as
/// the fallback for anchorless maps, and whether the map looks <b>under-split</b> (a symmetric N-team map
/// that resolved into fewer than N major landmasses → likely merged teams).
/// </summary>
public sealed class IslandHealthEndpoint(MapRepository repo, MapReader reader, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/island-health"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        var map = await repo.GetBySlugAsync(slug, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var art = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == map.Id && a.Kind == ArtifactKind.IslandsJson, ct);
        var islands = IslandRoleData.ParseIslands(art?.Data);
        var teams = await db.Teams.CountAsync(t => t.MapId == map.Id, ct);

        // Size buckets (always available) + the under-split signal.
        var largest = islands.Count > 0 ? islands.Max(i => i.Blocks) : 0;
        var sizeRoles = islands.Select(i => IslandClassifier.Classify(i.Blocks, largest).ToString().ToLowerInvariant()).ToList();
        var majors = sizeRoles.Count(r => r == "major");

        // Gameplay roles from the map's objective anchors + build regions (best-effort: needs the doc).
        var gameplayRoles = await GameplayRolesAsync(slug, islands.Select(i => i.Geom).ToList(), ct);

        await Send.OkAsync(new
        {
            islands = islands.Count,
            teams,
            majors,
            neutrals = sizeRoles.Count(r => r == "neutral"),
            small = sizeRoles.Count(r => r == "small"),
            sizeRoles,
            roles = gameplayRoles,   // team / objective / neutral / decorative (null when the doc is unavailable)
            team = gameplayRoles?.Count(r => r == "team"),
            underSplit = IslandClassifier.LooksUnderSplit(majors, teams),
        }, ct);
    }

    private async Task<List<string>?> GameplayRolesAsync(string slug, IReadOnlyList<Geometry> islands, CancellationToken ct)
    {
        if (islands.Count == 0) return null;
        var doc = await reader.ReadDocAsync(slug, ct);
        if (doc is null) return null;

        var (anchors, buildRegion) = IslandRoleData.Context(doc, islands);
        return IslandRoleClassifier.Classify(islands, anchors, buildRegion)
            .Select(r => r.ToString().ToLowerInvariant()).ToList();
    }
}

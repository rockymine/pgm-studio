using System.Text.Json;
using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Analysis.Footprint;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// Review flag for a map whose auto-detected island sketch looks wrong (the decompose-queue triage from
/// G9). The flag is human-set; <see cref="IslandHealthEndpoint"/> offers an automatic second opinion for
/// the reliably-detectable failure mode (merged teams), so a reviewer can prioritise the queue.
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
/// GET /api/map/{slug}/island-health — an automatic read of the detected islands: each island's gameplay
/// role (major / neutral / small) by size, and whether the map looks <b>under-split</b> (a symmetric
/// N-team map that resolved into fewer than N major landmasses → likely merged teams). Reliable for the
/// merged case; the over-split case (raised features carved off a connected island) is not reliably
/// auto-detectable, so it is left to the manual review flag.
/// </summary>
public sealed class IslandHealthEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/island-health"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var art = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == map.Id && a.Kind == ArtifactKind.IslandsJson, ct);
        var counts = ParseBlockCounts(art?.Data);
        var teams = await db.Teams.CountAsync(t => t.MapId == map.Id, ct);

        var largest = counts.Count > 0 ? counts.Max() : 0;
        var roles = counts.Select(c => IslandClassifier.Classify(c, largest).ToString().ToLowerInvariant()).ToList();
        var majors = roles.Count(r => r == "major");
        await Send.OkAsync(new
        {
            islands = counts.Count,
            teams,
            majors,
            neutrals = roles.Count(r => r == "neutral"),
            small = roles.Count(r => r == "small"),
            roles,
            underSplit = IslandClassifier.LooksUnderSplit(majors, teams),
        }, ct);
    }

    // The islands_json artifact is the SerializeJson output: an array of {block_count, ...}.
    private static List<int> ParseBlockCounts(byte[]? data)
    {
        if (data is null || data.Length == 0) return [];
        try
        {
            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];
            return doc.RootElement.EnumerateArray()
                .Where(e => e.TryGetProperty("block_count", out _))
                .Select(e => e.GetProperty("block_count").GetInt32())
                .ToList();
        }
        catch { return []; }
    }
}

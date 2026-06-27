using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Analysis.Footprint;
using PgmStudio.Contracts;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// Sketch tool persistence (docs/contracts/sketch-authoring.md §1–§2): the <c>sketch_layout_json</c>
/// artifact that backs a draft map. Mirrors <see cref="IntentStore"/> — it lives outside the
/// entity-replace codec, so it survives <c>MapWriter.SaveDocAsync</c>. The blob is the browser's
/// JS-origin layout ({setup, layout:{shapes, islands}}), stored verbatim (authoring source, not the
/// canonical document), so it is kept as raw bytes here rather than parsed into typed C#.
/// </summary>
internal static class SketchStore
{
    public static async Task<byte[]?> LoadAsync(PgmDb db, long mapId, CancellationToken ct)
    {
        var art = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == mapId && a.Kind == ArtifactKind.SketchLayoutJson, ct);
        return art?.Data;
    }

    public static async Task SaveAsync(PgmDb db, long mapId, byte[] data, CancellationToken ct)
    {
        await db.Artifacts.Where(a => a.MapId == mapId && a.Kind == ArtifactKind.SketchLayoutJson).DeleteAsync(ct);
        await db.InsertAsync(new MapArtifactRow { MapId = mapId, Kind = ArtifactKind.SketchLayoutJson, Data = data }, token: ct);
    }
}

/// <summary>POST /api/sketch — originate a sketch: create a draft (geometry-less) map + an empty layout
/// artifact. Returns the slug; the client navigates to <c>/maps/{slug}/sketch</c>. Body: optional {name}.</summary>
public sealed class SketchCreateEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Post("/sketch"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var name = "Untitled sketch";
        try
        {
            using var doc = await JsonDocument.ParseAsync(HttpContext.Request.Body, cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                && n.GetString() is { } s && !string.IsNullOrWhiteSpace(s)) name = s.Trim();
        }
        catch { /* empty / invalid body → default name */ }

        var slug = await SketchSlug.UniqueAsync(repo, SketchSlug.Slugify(name), ct);
        var now = DateTime.UtcNow;
        var mapId = await repo.InsertAsync(new MapRow { Slug = slug, Name = name, Gamemode = "ctw", Stage = MapStage.Sketch, CreatedAt = now, UpdatedAt = now });
        await SketchStore.SaveAsync(db, mapId, "{}"u8.ToArray(), ct);   // seed so GET works immediately
        await Send.OkAsync(new { slug }, ct);
    }
}

/// <summary>Slug derivation shared by the sketch-origination endpoints (create blank / generate).</summary>
internal static class SketchSlug
{
    public static string Slugify(string s)
    {
        var slug = Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length > 0 ? slug : "sketch";
    }

    public static async Task<string> UniqueAsync(MapRepository repo, string baseSlug, CancellationToken ct)
    {
        if (await repo.GetBySlugAsync(baseSlug, ct) is null) return baseSlug;
        for (var i = 2; ; i++)
        {
            var s = $"{baseSlug}-{i}";
            if (await repo.GetBySlugAsync(s, ct) is null) return s;
        }
    }
}

/// <summary>POST /api/sketch/generate — originate a sketch from a generated starter layout. Runs the lane
/// generator for the requested archetype/seed, simplifies + frames it for the editor, creates a draft map,
/// and stores the layout as its <c>sketch_layout_json</c> artifact. Body: optional {name, archetype, seed}.
/// Returns the slug; the client navigates to <c>/maps/{slug}/sketch</c> to reshape it.</summary>
public sealed class SketchGenerateEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Post("/sketch/generate"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var name = "Generated sketch";
        var archetype = LaneArchetype.H;
        var seed = 1;
        try
        {
            using var doc = await JsonDocument.ParseAsync(HttpContext.Request.Body, cancellationToken: ct);
            var root = doc.RootElement;
            if (root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                && n.GetString() is { } s && !string.IsNullOrWhiteSpace(s)) name = s.Trim();
            if (root.TryGetProperty("archetype", out var a) && a.ValueKind == JsonValueKind.String
                && Enum.TryParse<LaneArchetype>(a.GetString(), ignoreCase: true, out var arch)) archetype = arch;
            if (root.TryGetProperty("seed", out var sd) && sd.ValueKind == JsonValueKind.Number) seed = sd.GetInt32();
        }
        catch { /* empty / invalid body → defaults */ }

        var result = LaneSketchGenerator.Build(new LaneLayoutOptions { Archetype = archetype, Seed = seed });
        var layout = SketchLayoutPrep.ForEditor(result.Layout);

        var slug = await SketchSlug.UniqueAsync(repo, SketchSlug.Slugify(name), ct);
        var now = DateTime.UtcNow;
        var mapId = await repo.InsertAsync(new MapRow { Slug = slug, Name = name, Gamemode = "ctw", Stage = MapStage.Sketch, CreatedAt = now, UpdatedAt = now });
        await SketchStore.SaveAsync(db, mapId, Encoding.UTF8.GetBytes(layout.ToJson()), ct);
        await Send.OkAsync(new { slug }, ct);
    }
}

/// <summary>POST /api/sketch/generate/stages — run the Organic generator and emit its per-stage intermediates
/// (value-noise field, sampled anchors, lane spines, ribbon shapes, placed objectives + mirror settings) for
/// the demonstration page at <c>/concepts/organic</c>. Pure compute — no map is created or stored. Body:
/// optional {seed, wools}.</summary>
public sealed class SketchGenerateStagesEndpoint : EndpointWithoutRequest
{
    public override void Configure() { Post("/sketch/generate/stages"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var seed = 1;
        var wools = 2;
        try
        {
            using var doc = await JsonDocument.ParseAsync(HttpContext.Request.Body, cancellationToken: ct);
            var root = doc.RootElement;
            if (root.TryGetProperty("seed", out var sd) && sd.ValueKind == JsonValueKind.Number) seed = sd.GetInt32();
            if (root.TryGetProperty("wools", out var wl) && wl.ValueKind == JsonValueKind.Number) wools = Math.Clamp(wl.GetInt32(), 1, 6);
        }
        catch { /* empty / invalid body → defaults */ }

        var o = LaneSketchGenerator.OrganicOptions(new LaneLayoutOptions { Archetype = LaneArchetype.Organic, Seed = seed, Wools = wools });
        var s = OrganicLane.GrowStages(o);
        static double[] P((double X, double Z) p) => [p.X, p.Z];

        await Send.OkAsync(new
        {
            seed = o.Seed,
            wools = o.Wools,
            width = s.Width,
            height = s.Height,
            laneWidth = s.LaneWidth,
            margin = s.Margin,
            mirrorMode = s.MirrorMode,
            cx = s.Cx,
            cz = s.Cz,
            noise = s.Noise,
            hub = s.Hub,
            trunkTips = s.TrunkTips.Select(P),
            woolTips = s.WoolTips.Select(P),
            spawn = P(s.Spawn),
            woolObjs = s.WoolObjs.Select(P),
            spines = s.Spines.Select(sp => new { kind = sp.Kind, points = sp.Points }),
            shapes = s.Shapes,
        }, ct);
    }
}

/// <summary>GET /api/map/{slug}/sketch — the stored sketch layout (the JS-origin blob), or {} if none.</summary>
public sealed class SketchGetEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/sketch"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }
        var data = await SketchStore.LoadAsync(db, map.Id, ct);
        await Send.OkAsync(JsonSerializer.Deserialize<JsonElement>(data ?? "{}"u8.ToArray()), ct);
    }
}

/// <summary>PUT /api/map/{slug}/sketch — replace the stored layout blob (the bridge's getState()).</summary>
public sealed class SketchPutEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Put("/map/{slug}/sketch"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        using var ms = new MemoryStream();
        await HttpContext.Request.Body.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        try { using var _ = JsonDocument.Parse(bytes); }   // reject non-JSON; don't store garbage
        catch { await Send.ResponseAsync(new { error = "invalid JSON" }, 400, ct); return; }

        await SketchStore.SaveAsync(db, map.Id, bytes, ct);
        await Send.OkAsync(new { ok = true }, ct);
    }
}

/// <summary>POST /api/map/{slug}/sketch/finish — rasterize the stored layout into the world geometry
/// artifacts (layer.parquet / islands.json / segments) so the draft flows into the Configure wizard.
/// 422 if the layout yields fewer than 2 islands (a CTW needs both sides).</summary>
public sealed class SketchFinishEndpoint(MapRepository repo, PgmDb db, WorldFeatureWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/sketch/finish"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var data = await SketchStore.LoadAsync(db, map.Id, ct);
        if (data is null) { await Send.ResponseAsync(new { error = "No sketch layout to finish." }, 422, ct); return; }

        var cells = SketchRasterizer.RasterizeColumns(Encoding.UTF8.GetString(data));
        var islands = IslandDetector.Detect(cells.Select(c => (c.X, c.Z)), minIslandSize: 1);
        if (islands.Count < 2)
        {
            await Send.ResponseAsync(new { error = $"A map needs at least 2 islands; got {islands.Count}. Draw both sides, or enable mirroring." }, 422, ct);
            return;
        }

        await writer.WriteSketchAsync(map.Id, cells, islands, ct);
        await repo.SetStageAsync(map.Id, MapStage.Configure, ct);   // the draft now has geometry → ready to configure
        await Send.OkAsync(new { slug = map.Slug, configureUrl = $"/maps/{map.Slug}/configure" }, ct);
    }
}

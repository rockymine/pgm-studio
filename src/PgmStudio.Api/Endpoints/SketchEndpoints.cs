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

        var slug = await SketchNaming.UniqueSlugAsync(repo, SketchNaming.Slugify(name), ct);
        var now = DateTime.UtcNow;
        var mapId = await repo.InsertAsync(new MapRow { Slug = slug, Name = name, Gamemode = "ctw", Stage = MapStage.Sketch, CreatedAt = now, UpdatedAt = now });
        await SketchStore.SaveAsync(db, mapId, "{}"u8.ToArray(), ct);   // seed so GET works immediately
        await Send.OkAsync(new { slug }, ct);
    }
}

/// <summary>Slug derivation + uniqueness for new draft maps, shared by the sketch originators.</summary>
internal static class SketchNaming
{
    public static string Slugify(string s)
    {
        var slug = Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length > 0 ? slug : "sketch";
    }

    public static async Task<string> UniqueSlugAsync(MapRepository repo, string baseSlug, CancellationToken ct)
    {
        if (await repo.GetBySlugAsync(baseSlug, ct) is null) return baseSlug;
        for (var i = 2; ; i++)
        {
            var s = $"{baseSlug}-{i}";
            if (await repo.GetBySlugAsync(s, ct) is null) return s;
        }
    }
}

/// <summary>POST /api/sketch/generate — originate a draft map pre-filled with a generated lane layout
/// (a two-team 'H' of lanes mirrored across the mid, plus a neutral mid island). Body (all optional):
/// {name, width, height, laneWidth, margin, curvedCrossbar, midIsland, mirrorMode}. Returns the slug and
/// the suggested objective placements; the layout is stored so <c>GET .../sketch</c> and finish work
/// immediately.</summary>
public sealed class SketchGenerateEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Post("/sketch/generate"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var name = "Generated sketch";
        var opts = new LaneLayoutOptions();
        try
        {
            using var doc = await JsonDocument.ParseAsync(HttpContext.Request.Body, cancellationToken: ct);
            var root = doc.RootElement;
            if (root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                && n.GetString() is { } s && !string.IsNullOrWhiteSpace(s)) name = s.Trim();
            opts = ReadOptions(root, opts);
        }
        catch { /* empty / invalid body → defaults */ }

        var result = LaneSketchGenerator.HLayout(opts);
        var slug = await SketchNaming.UniqueSlugAsync(repo, SketchNaming.Slugify(name), ct);
        var now = DateTime.UtcNow;
        var mapId = await repo.InsertAsync(new MapRow { Slug = slug, Name = name, Gamemode = "ctw", Stage = MapStage.Sketch, CreatedAt = now, UpdatedAt = now });
        await SketchStore.SaveAsync(db, mapId, Encoding.UTF8.GetBytes(result.Layout.ToJson()), ct);
        await Send.OkAsync(new { slug, objectives = result.Objectives }, ct);
    }

    private static LaneLayoutOptions ReadOptions(JsonElement root, LaneLayoutOptions d)
    {
        double Num(string k, double fallback) =>
            root.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : fallback;
        bool Flag(string k, bool fallback) =>
            root.TryGetProperty(k, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False) ? v.GetBoolean() : fallback;
        string Str(string k, string fallback) =>
            root.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : fallback;
        return d with
        {
            Width = Num("width", d.Width),
            Height = Num("height", d.Height),
            LaneWidth = Num("laneWidth", d.LaneWidth),
            Margin = Num("margin", d.Margin),
            CurvedCrossbar = Flag("curvedCrossbar", d.CurvedCrossbar),
            MidIsland = Flag("midIsland", d.MidIsland),
            MirrorMode = Str("mirrorMode", d.MirrorMode),
        };
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

        var cells = SketchRasterizer.Rasterize(Encoding.UTF8.GetString(data));
        var islands = IslandDetector.Detect(cells, minIslandSize: 1);
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

using System.Text.Json;
using System.Text.Json.Nodes;
using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data;
using PgmStudio.Data.Repositories;
using PgmStudio.Pgm.Editing;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Editor-only "draft" sidecar (E10): a per-map <c>{region_key: editor_step}</c> map for freshly drawn,
/// not-yet-wired regions. Stored as a <c>region_drafts_json</c> artifact so it survives the entity-replace
/// save path (MapWriter deletes+recreates region rows on every edit, so a per-region column would be lost),
/// and is never part of the PGM map document the codec/categorizer see. Pruned against live regions on read.
/// </summary>
internal static class RegionDraftStore
{
    public static async Task<Dictionary<string, string>> LoadAsync(PgmDb db, long mapId, CancellationToken ct)
    {
        var art = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == mapId && a.Kind == ArtifactKind.RegionDraftsJson, ct);
        return art is null ? new() : JsonSerializer.Deserialize<Dictionary<string, string>>(art.Data) ?? new();
    }

    public static async Task SaveAsync(PgmDb db, long mapId, Dictionary<string, string> drafts, CancellationToken ct)
    {
        await db.Artifacts.Where(a => a.MapId == mapId && a.Kind == ArtifactKind.RegionDraftsJson).DeleteAsync(ct);
        if (drafts.Count > 0)
            await db.InsertAsync(new MapArtifactRow { MapId = mapId, Kind = ArtifactKind.RegionDraftsJson, Data = JsonSerializer.SerializeToUtf8Bytes(drafts) }, token: ct);
    }

    /// <summary>Record that the given region keys were drawn in <paramref name="step"/> (teams/objective/build).</summary>
    public static async Task TagAsync(PgmDb db, long mapId, string step, IEnumerable<string> regionKeys, CancellationToken ct)
    {
        var drafts = await LoadAsync(db, mapId, ct);
        foreach (var k in regionKeys) if (!string.IsNullOrEmpty(k)) drafts[k] = step;
        await SaveAsync(db, mapId, drafts, ct);
    }
}

/// <summary>POST /api/map/{slug}/regions — create a primitive region (optionally tagged with the
/// editor <c>draft_step</c> so it shows in that activity until it's wired — E10).</summary>
public sealed class RegionCreateEndpoint(MapRepository repo, MapReader reader, MapWriter writer, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/regions"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, slug, doc => RegionEditor.CreateRegion(doc, p), ct);

        if (s == 200 && p.GetValueOrDefault("draft_step") is string step && step.Length > 0
            && (b as Dict)?.GetValueOrDefault("id") is string newId && await repo.GetBySlugAsync(slug, ct) is { } map)
            await RegionDraftStore.TagAsync(db, map.Id, step, [newId], ct);

        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>
/// POST /api/map/{slug}/regions/{regionId}/counterpart — create the symmetry counterpart(s) of a
/// region (F3). Body: {mode, center:{cx,cz}?}; centre falls back to the confirmed symmetry artifact.
/// </summary>
public sealed class RegionCounterpartEndpoint(MapRepository repo, MapReader reader, MapWriter writer, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/regions/{regionId}/counterpart"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        var regionId = Route<string>("regionId")!;
        var map = await repo.GetBySlugAsync(slug, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var mode = (p.GetValueOrDefault("mode") as string ?? "").Trim();

        double? cx = null, cz = null;
        if (p.GetValueOrDefault("center") is Dict center) { cx = Num(center.GetValueOrDefault("cx")); cz = Num(center.GetValueOrDefault("cz")); }
        if (cx is null || cz is null)   // fall back to the confirmed symmetry centre
        {
            var sym = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == map.Id && a.Kind == ArtifactKind.SymmetryJson, ct);
            if (sym is not null && JsonNode.Parse(sym.Data)?["center"] is JsonObject c)
            { cx ??= c["cx"]?.GetValue<double>(); cz ??= c["cz"]?.GetValue<double>(); }
        }
        if (cx is null || cz is null)
        { await Send.ResponseAsync(new Dict { ["error"] = "center {cx,cz} required (absent from body and symmetry)" }, 400, ct); return; }

        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, slug,
            doc => SymmetryAuthoring.CreateCounterpart(doc, regionId, mode, cx.Value, cz.Value), ct);
        await Send.ResponseAsync(b!, s, ct);
    }

    private static double? Num(object? v) => v switch { double d => d, long l => l, int i => i, float f => f, _ => null };
}

/// <summary>
/// POST /api/map/{slug}/regions/{regionId}/orbit — fill the symmetry orbit of a region (F3), i.e.
/// create every counterpart implied by the map's <b>confirmed</b> symmetry (rot_90 → 3 turns, mirror/
/// rot_180 → 1). Used right after a draw so an authored region appears in all symmetric positions.
/// Body: {category?}; mode + centre come from the confirmed symmetry artifact. If the map has no
/// confirmed symmetry this is a no-op (200 {created:[]}) so the draw flow works on asymmetric maps too.
/// </summary>
public sealed class RegionOrbitEndpoint(MapRepository repo, MapReader reader, MapWriter writer, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/regions/{regionId}/orbit"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        var regionId = Route<string>("regionId")!;
        var map = await repo.GetBySlugAsync(slug, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var category = p.GetValueOrDefault("category") as string ?? "other";
        var step = p.GetValueOrDefault("draft_step") as string;

        var sym = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == map.Id && a.Kind == ArtifactKind.SymmetryJson, ct);
        var node = sym is not null ? JsonNode.Parse(sym.Data) : null;
        var mode = node?["primary"]?["type"]?.GetValue<string>();
        var confirmed = node?["status"]?.GetValue<string>() == "confirmed";
        var cx = node?["center"]?["cx"]?.GetValue<double>();
        var cz = node?["center"]?["cz"]?.GetValue<double>();

        if (!confirmed || mode is null || cx is null || cz is null)
        { await Send.OkAsync(new Dict { ["created"] = new List<object?>() }, ct); return; }

        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, slug,
            doc => SymmetryAuthoring.CreateOrbit(doc, regionId, mode, cx.Value, cz.Value, category), ct);

        // the orbit counterparts are unwired too — tag them with the same draft step so they appear
        // alongside the source in the activity until wiring derives their real category (E10).
        if (s == 200 && !string.IsNullOrEmpty(step) && (b as Dict)?.GetValueOrDefault("created") is List<object?> created)
            await RegionDraftStore.TagAsync(db, map.Id, step, created.OfType<string>(), ct);

        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>POST /api/map/{slug}/regions/group — wrap regions in a compound.</summary>
public sealed class RegionGroupEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/regions/group"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => RegionEditor.GroupRegions(doc, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>POST /api/map/{slug}/regions/ungroup — dissolve a compound.</summary>
public sealed class RegionUngroupEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/regions/ungroup"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => RegionEditor.UngroupRegion(doc, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>POST /api/map/{slug}/regions/restore — restore a deleted region from its snapshot.</summary>
public sealed class RegionRestoreEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/regions/restore"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var snapshot = p.GetValueOrDefault("snapshot") as Dict ?? p;
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => RegionEditor.RestoreRegion(doc, snapshot), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>DELETE /api/map/{slug}/regions/{regionId} — delete a region (returns an undo snapshot).</summary>
public sealed class RegionDeleteEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/map/{slug}/regions/{regionId}"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("regionId")!;
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => RegionEditor.DeleteRegion(doc, id), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>PATCH /api/map/{slug}/regions/{regionId} — rename / bounds / coords.</summary>
public sealed class RegionPatchEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Patch("/map/{slug}/regions/{regionId}"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("regionId")!;
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => RegionEditor.PatchRegion(doc, id, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>POST /api/map/{slug}/regions/{regionId}/change-type — change a compound's type.</summary>
public sealed class RegionChangeTypeEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/regions/{regionId}/change-type"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("regionId")!;
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => RegionEditor.ChangeRegionType(doc, id, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>POST /api/map/{slug}/regions/{regionId}/remove-from-group — detach a child.</summary>
public sealed class RegionRemoveFromGroupEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/regions/{regionId}/remove-from-group"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("regionId")!;
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => RegionEditor.RemoveFromGroup(doc, id, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>POST /api/map/{slug}/regions/{regionId}/set-base-child — set a complement's base child.</summary>
public sealed class RegionSetBaseChildEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/regions/{regionId}/set-base-child"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("regionId")!;
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => RegionEditor.SetBaseChild(doc, id, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

using FastEndpoints;
using PgmStudio.Data.Repositories;
using PgmStudio.Pgm.Editing;

namespace PgmStudio.Api.Endpoints;

// ── wools ───────────────────────────────────────────────────────────────────────────

/// <summary>POST /api/map/{slug}/wools — add a wool objective.</summary>
public sealed class WoolCreateEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/wools"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => WoolEditor.AddWool(doc, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>PATCH /api/map/{slug}/wools/{woolId} — update a wool.</summary>
public sealed class WoolUpdateEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Patch("/map/{slug}/wools/{woolId}"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("woolId")!;
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => WoolEditor.UpdateWool(doc, id, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>DELETE /api/map/{slug}/wools/{woolId} — remove a wool.</summary>
public sealed class WoolDeleteEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/map/{slug}/wools/{woolId}"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("woolId")!;
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => WoolEditor.DeleteWool(doc, id), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>POST /api/map/{slug}/wools/{woolId}/monuments — add a monument to a wool.</summary>
public sealed class MonumentCreateEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/wools/{woolId}/monuments"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var wid = Route<string>("woolId")!;
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => WoolEditor.AddMonument(doc, wid, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>PATCH /api/map/{slug}/wools/{woolId}/monuments/{monId} — update a monument.</summary>
public sealed class MonumentUpdateEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Patch("/map/{slug}/wools/{woolId}/monuments/{monId}"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var wid = Route<string>("woolId")!; var mid = Route<string>("monId")!;
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => WoolEditor.UpdateMonument(doc, wid, mid, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>DELETE /api/map/{slug}/wools/{woolId}/monuments/{monId} — remove a monument.</summary>
public sealed class MonumentDeleteEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/map/{slug}/wools/{woolId}/monuments/{monId}"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var wid = Route<string>("woolId")!; var mid = Route<string>("monId")!;
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => WoolEditor.DeleteMonument(doc, wid, mid), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

// ── filters ─────────────────────────────────────────────────────────────────────────

/// <summary>GET /api/map/{slug}/filters — the registry + a per-filter usage map.</summary>
public sealed class FiltersListEndpoint(MapRepository repo, MapReader reader) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/filters"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var loaded = await AnalysisLoad.LoadAsync(repo, reader, Route<string>("slug")!, ct);
        if (loaded is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(FilterEditor.ListFilters(loaded.Value.doc), ct);
    }
}

/// <summary>POST /api/map/{slug}/filters — create a filter.</summary>
public sealed class FilterCreateEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/filters"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => FilterEditor.CreateFilter(doc, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>PATCH /api/map/{slug}/filter/{fid} — replace a filter definition.</summary>
public sealed class FilterUpdateEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Patch("/map/{slug}/filter/{fid}"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var fid = Route<string>("fid")!;
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => FilterEditor.UpdateFilter(doc, fid, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>DELETE /api/map/{slug}/filter/{fid} — delete a filter (must be unreferenced).</summary>
public sealed class FilterDeleteEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/map/{slug}/filter/{fid}"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var fid = Route<string>("fid")!;
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => FilterEditor.DeleteFilter(doc, fid), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

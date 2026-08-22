using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Pgm.Editing;

namespace PgmStudio.Api.Endpoints;

// ── wools ───────────────────────────────────────────────────────────────────────────

/// <summary>POST /api/map/{slug}/wools — add a wool objective.</summary>
public sealed class WoolCreateEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/map/{slug}/wools");
        AllowAnonymous();
        Description(b => b.Accepts<WoolCreateRequest>("application/json").Produces<WoolWrittenDto>(200, "application/json").Refuses(404, 409, 422));
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var applied = await MapEdit.RunAsync(repo, reader, writer, Route<string>("slug")!, doc => WoolEditor.AddWool(doc, p), Revisions.Expected(HttpContext), ct);
        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }
}

/// <summary>PATCH /api/map/{slug}/wools/{woolId} — update a wool.</summary>
public sealed class WoolUpdateEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Patch("/map/{slug}/wools/{woolId}");
        AllowAnonymous();
        Description(b => b.Accepts<WoolUpdateRequest>("application/json").Produces<WoolWrittenDto>(200, "application/json").Refuses(404, 409, 422));
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("woolId")!;
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var applied = await MapEdit.RunAsync(repo, reader, writer, Route<string>("slug")!, doc => WoolEditor.UpdateWool(doc, id, p), Revisions.Expected(HttpContext), ct);
        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }
}

/// <summary>DELETE /api/map/{slug}/wools/{woolId} — remove a wool.</summary>
public sealed class WoolDeleteEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/map/{slug}/wools/{woolId}");
        AllowAnonymous();
        Description(b => b.Produces<AppliedDto>(200, "application/json").Refuses(404, 409, 422));
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("woolId")!;
        var applied = await MapEdit.RunAsync(repo, reader, writer, Route<string>("slug")!, doc => WoolEditor.DeleteWool(doc, id), Revisions.Expected(HttpContext), ct);
        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }
}

/// <summary>POST /api/map/{slug}/wools/{woolId}/monuments — add a monument to a wool.</summary>
public sealed class MonumentCreateEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/map/{slug}/wools/{woolId}/monuments");
        AllowAnonymous();
        Description(b => b.Accepts<MonumentWriteRequest>("application/json").Produces<MonumentWrittenDto>(200, "application/json").Refuses(404, 409, 422));
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var wid = Route<string>("woolId")!;
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var applied = await MapEdit.RunAsync(repo, reader, writer, Route<string>("slug")!, doc => WoolEditor.AddMonument(doc, wid, p), Revisions.Expected(HttpContext), ct);
        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }
}

/// <summary>PATCH /api/map/{slug}/wools/{woolId}/monuments/{monId} — update a monument.</summary>
public sealed class MonumentUpdateEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Patch("/map/{slug}/wools/{woolId}/monuments/{monId}");
        AllowAnonymous();
        Description(b => b.Accepts<MonumentWriteRequest>("application/json").Produces<MonumentWrittenDto>(200, "application/json").Refuses(404, 409, 422));
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var wid = Route<string>("woolId")!; var mid = Route<string>("monId")!;
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var applied = await MapEdit.RunAsync(repo, reader, writer, Route<string>("slug")!, doc => WoolEditor.UpdateMonument(doc, wid, mid, p), Revisions.Expected(HttpContext), ct);
        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }
}

/// <summary>DELETE /api/map/{slug}/wools/{woolId}/monuments/{monId} — remove a monument.</summary>
public sealed class MonumentDeleteEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/map/{slug}/wools/{woolId}/monuments/{monId}");
        AllowAnonymous();
        Description(b => b.Produces<AppliedDto>(200, "application/json").Refuses(404, 409, 422));
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var wid = Route<string>("woolId")!; var mid = Route<string>("monId")!;
        var applied = await MapEdit.RunAsync(repo, reader, writer, Route<string>("slug")!, doc => WoolEditor.DeleteMonument(doc, wid, mid), Revisions.Expected(HttpContext), ct);
        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }
}

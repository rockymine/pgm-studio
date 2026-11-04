using System.Text.Json;
using System.Text.Json.Nodes;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Editing;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>POST /api/map/{slug}/regions — create a primitive region.</summary>
public sealed class RegionCreateEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/map/{slug}/regions");
        Description(b => b.Accepts<RegionCreateRequest>("application/json").Produces<RegionCreatedDto>(200, "application/json").Refuses(404, 409, 422));
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var applied = await MapEdit.RunAsync(repo, reader, writer, slug, doc => RegionEditor.CreateRegion(doc, p), Revisions.Expected(HttpContext), ct);

        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }
}

/// <summary>
/// POST /api/map/{slug}/regions/{regionId}/counterpart — create the symmetry counterpart(s) of a
/// region (F3). Body: {mode, center:{cx,cz}?}; centre falls back to the confirmed symmetry artifact.
/// </summary>
public sealed class RegionCounterpartEndpoint(MapRepository repo, MapReader reader, MapWriter writer, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/map/{slug}/regions/{regionId}/counterpart");
        Description(b => b.Accepts<RegionCounterpartRequest>("application/json").Produces<RegionOrbitDto>(200, "application/json").Refuses(404, 409, 422));
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        var regionId = Route<string>("regionId")!;
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var mode = (p.GetValueOrDefault("mode") as string ?? "").Trim();

        double? cx = null, cz = null;
        if (p.GetValueOrDefault("center") is Dict center) { cx = Num(center.GetValueOrDefault("cx")); cz = Num(center.GetValueOrDefault("cz")); }
        if (cx is null || cz is null)   // fall back to the confirmed symmetry centre
        {
            var symRow = await SymmetryStore.LoadAsync(db, map.Id, ct);
            if (symRow is not null) { cx ??= symRow.CenterX; cz ??= symRow.CenterZ; }
        }
        if (cx is null || cz is null)
        {
            await Refusals.UnreadableAsync(HttpContext, "no centre to mirror about",
                "the centre is taken from the body as {cx, cz} or from the map's confirmed symmetry, and "
                + "neither carries one", ct, field: "center");
            return;
        }

        var applied = await MapEdit.RunAsync(repo, reader, writer, slug,
            doc => SymmetryAuthoring.CreateCounterpart(doc, regionId, mode, cx.Value, cz.Value), Revisions.Expected(HttpContext), ct);
        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
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
    public override void Configure()
    {
        Post("/map/{slug}/regions/{regionId}/orbit");
        Description(b => b.Accepts<RegionOrbitRequest>("application/json").Produces<RegionOrbitDto>(200, "application/json").Refuses(404, 409, 422));
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        var regionId = Route<string>("regionId")!;
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var category = p.GetValueOrDefault("category") as string ?? "other";

        var symRow = await SymmetryStore.LoadAsync(db, map.Id, ct);
        var mode = symRow?.PrimaryType;
        var confirmed = symRow?.Status == "confirmed";
        var cx = symRow?.CenterX;
        var cz = symRow?.CenterZ;

        if (!confirmed || mode is null || cx is null || cz is null)
        { await Send.OkAsync(new Dict { ["created"] = new List<object?>() }, ct); return; }

        var applied = await MapEdit.RunAsync(repo, reader, writer, slug,
            doc => SymmetryAuthoring.CreateOrbit(doc, regionId, mode, cx.Value, cz.Value, category), Revisions.Expected(HttpContext), ct);

        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }
}

/// <summary>DELETE /api/map/{slug}/regions/{regionId} — delete a region (returns an undo snapshot).</summary>
public sealed class RegionDeleteEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/map/{slug}/regions/{regionId}");
        Description(b => b.Produces<AppliedDto>(200, "application/json").Refuses(404, 409, 422));
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("regionId")!;
        var applied = await MapEdit.RunAsync(repo, reader, writer, Route<string>("slug")!, doc => RegionEditor.DeleteRegion(doc, id), Revisions.Expected(HttpContext), ct);
        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }
}

/// <summary>PATCH /api/map/{slug}/regions/{regionId} — rename / bounds / coords.</summary>
public sealed class RegionPatchEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Patch("/map/{slug}/regions/{regionId}");
        Description(b => b.Accepts<RegionPatchRequest>("application/json").Produces<RegionPatchedDto>(200, "application/json").Refuses(404, 409, 422));
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("regionId")!;
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var applied = await MapEdit.RunAsync(repo, reader, writer, Route<string>("slug")!, doc => RegionEditor.PatchRegion(doc, id, p), Revisions.Expected(HttpContext), ct);
        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }
}

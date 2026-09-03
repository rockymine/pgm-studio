using System.Text.Json;
using System.Text.Json.Nodes;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Endpoints;

// ── the relief, one group's interior at a time ──────────────────────────────────────
//
// The third part of the sketch to get an address. A relief is already typed on the wire — it is the one
// finish key the layout declares a model for — so what these add is not a schema but the ability to state
// one island's terrain without restating the board's.

/// <summary>GET /api/map/{slug}/sketch/relief — every group's relief, by the group id it is solved over.
///
/// <para>The key is a group rather than a shape because a relief solved per shape leaves a seam wherever two
/// of them meet and disagree about the height they share.</para></summary>
public sealed class SketchReliefRegistryEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<Dictionary<string, SketchReliefJson>>
{
    public override void Configure()
    {
        Get("/map/{slug}/sketch/relief"); AllowAnonymous();
        Description(b => b.Produces<Dictionary<string, SketchReliefJson>>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var layoutJson = await SketchPartWrite.LayoutOf(artifacts, map.Id, ct) ?? "{}";
        if (await artifacts.RevisionAsync(map.Id, ArtifactKind.SketchLayoutJson, ct) is { } revision)
            Revisions.Answer(HttpContext, revision);
        await Send.OkAsync(SketchLayout.Stated(layoutJson)?.Relief ?? [], ct);
    }
}

/// <summary>GET /api/map/{slug}/sketch/relief/{groupId} — one group's relief. 404 where the layout states
/// none for that group, which is every group whose ground is as flat as its shapes drew it.</summary>
public sealed class SketchReliefOfGroupEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<SketchReliefJson>
{
    public override void Configure()
    {
        Get("/map/{slug}/sketch/relief/{groupId}"); AllowAnonymous();
        Description(b => b.Produces<SketchReliefJson>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var layoutJson = await SketchPartWrite.LayoutOf(artifacts, map.Id, ct) ?? "{}";
        var relief = SketchLayout.Stated(layoutJson)?.Relief;
        if (relief is null || !relief.TryGetValue(Route<string>("groupId")!, out var found))
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(found, ct);
    }
}

/// <summary>PUT /api/map/{slug}/sketch/relief/{groupId} — state one group's interior elevation, replacing
/// whatever that group carried.
///
/// <para>It does not check that the group exists. A relief is authored against a fusion the compiler
/// produces, and whether the id still names one is the question <c>SK1</c> answers on the compile path —
/// where losing hand-authored terrain is the risk worth refusing over. Answering it here would refuse a
/// relief written before the geometry it belongs to, which is an order an author works in.</para></summary>
public sealed class SketchReliefWriteEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PartWrittenDto>
{
    public override void Configure()
    {
        Put("/map/{slug}/sketch/relief/{groupId}"); AllowAnonymous();
        Description(b => b.Accepts<SketchReliefJson>("application/json")
                          .Produces<PartWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var id = Route<string>("groupId")!;

        var body = await RawBody.ReadAsync(HttpContext, ct);
        if (SketchReliefWrite.Stated(body) is null)
        {
            await Refusals.UnreadableAsync(HttpContext, "malformed relief",
                "the body is not a relief: it states `base`, `reach`, `step` and the marks the surface is "
                + "solved from.", ct, field: "relief");
            return;
        }

        var layoutJson = await SketchPartWrite.LayoutOf(artifacts, map.Id, ct);
        var written = await SketchPartWrite.StoreAsync(HttpContext, artifacts, map.Id,
            SketchReliefWrite.With(layoutJson, id, JsonNode.Parse(body)), id, ct);
        if (await SketchPartWrite.RefusedAsync(HttpContext, written, ct)) return;
        await Send.OkAsync(new PartWrittenDto(written.Id), ct);
    }
}

/// <summary>DELETE /api/map/{slug}/sketch/relief/{groupId} — take one group's relief off the board, which
/// leaves its ground as flat as the shapes drew it.</summary>
public sealed class SketchReliefDeleteEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PartWrittenDto>
{
    public override void Configure()
    {
        Delete("/map/{slug}/sketch/relief/{groupId}"); AllowAnonymous();
        Description(b => b.Produces<PartWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var id = Route<string>("groupId")!;
        var layoutJson = await SketchPartWrite.LayoutOf(artifacts, map.Id, ct);
        if (!SketchReliefWrite.Carries(layoutJson, id)) { await Send.NotFoundAsync(ct); return; }

        var written = await SketchPartWrite.StoreAsync(HttpContext, artifacts, map.Id,
            SketchReliefWrite.With(layoutJson, id, null), id, ct);
        if (await SketchPartWrite.RefusedAsync(HttpContext, written, ct)) return;
        await Send.OkAsync(new PartWrittenDto(written.Id), ct);
    }
}

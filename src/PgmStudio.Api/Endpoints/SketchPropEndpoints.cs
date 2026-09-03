using System.Text.Json;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Minecraft.Dressing;

namespace PgmStudio.Api.Endpoints;

// ── the dressing, one placement at a time ───────────────────────────────────────────
//
// The finish's half of what the objectives already have: a prop is a resource with an id, so a caller adds,
// edits or removes one without holding the board it stands on. Every route answers the placement's own type,
// which is what puts the six prop kinds and their knobs in the published schema — a shape an agent reads off
// the document rather than out of prose.

/// <summary>GET /api/map/{slug}/sketch/props — every placement the map carries, with the recipes they name.
///
/// <para>The recipes ride with the placements because a placement referencing a key nobody can resolve is not
/// readable on its own: a tree states <c>styleKey</c> and the registry states what that key is made of.</para></summary>
public sealed class SketchPropListEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<DressingDoc>
{
    public override void Configure()
    {
        Get("/map/{slug}/sketch/props"); AllowAnonymous();
        Description(b => b.Produces<DressingDoc>(200, "application/json").Refuses(400, 404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var layoutJson = await SketchPartWrite.LayoutOf(artifacts, map.Id, ct);
        DressingDoc doc;
        try { doc = SketchDressingWrite.Read(layoutJson); }
        catch (Exception fault) when (fault is JsonException or DressingParseException)
        {
            await Refusals.UnreadableAsync(HttpContext, "unreadable dressing", fault, ct);
            return;
        }
        if (await artifacts.RevisionAsync(map.Id, ArtifactKind.SketchLayoutJson, ct) is { } revision)
            Revisions.Answer(HttpContext, revision);
        await Send.OkAsync(doc, ct);
    }
}

/// <summary>POST /api/map/{slug}/sketch/props — place one prop, answering the id it was given.
///
/// <para>A body stating a free id keeps it; one stating none, or one already taken, is minted
/// <c>{kind}-{n}</c>. The placement goes on the end, because the pass runs in the order props were placed
/// and an addition has not been placed before anything.</para></summary>
public sealed class SketchPropCreateEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PropWrittenDto>
{
    public override void Configure()
    {
        Post("/map/{slug}/sketch/props"); AllowAnonymous();
        Description(b => b.Accepts<PlacedProp>("application/json")
                          .Produces<PropWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var outcome = await SketchPropWrite.RunAsync(repo, artifacts, HttpContext, ct,
            (doc, prop) => DressingEdit.Add(doc, prop!), needsBody: true);
        if (outcome.IsAnswered) return;
        if (outcome.IsMissing) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(new PropWrittenDto(outcome.Id), ct);
    }
}

/// <summary>PATCH /api/map/{slug}/sketch/props/{propId} — replace one placement, keeping its position in the
/// pass's order. 404 where the id names no placement.</summary>
public sealed class SketchPropUpdateEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PropWrittenDto>
{
    public override void Configure()
    {
        Patch("/map/{slug}/sketch/props/{propId}"); AllowAnonymous();
        Description(b => b.Accepts<PlacedProp>("application/json")
                          .Produces<PropWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var outcome = await SketchPropWrite.RunAsync(repo, artifacts, HttpContext, ct,
            (doc, prop) => DressingEdit.Replace(doc, Route<string>("propId")!, prop!), needsBody: true);
        if (outcome.IsAnswered) return;
        if (outcome.IsMissing) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(new PropWrittenDto(outcome.Id), ct);
    }
}

/// <summary>DELETE /api/map/{slug}/sketch/props/{propId} — take one placement off the board. The recipe it
/// named stays in the registry, since a key is shared by every placement wearing it.</summary>
public sealed class SketchPropDeleteEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PropWrittenDto>
{
    public override void Configure()
    {
        Delete("/map/{slug}/sketch/props/{propId}"); AllowAnonymous();
        Description(b => b.Produces<PropWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var outcome = await SketchPropWrite.RunAsync(repo, artifacts, HttpContext, ct,
            (doc, _) => DressingEdit.Remove(doc, Route<string>("propId")!), needsBody: false);
        if (outcome.IsAnswered) return;
        if (outcome.IsMissing) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(new PropWrittenDto(outcome.Id), ct);
    }
}

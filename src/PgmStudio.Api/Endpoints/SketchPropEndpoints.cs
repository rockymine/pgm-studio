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

/// <summary>GET /api/map/{slug}/sketch/props — the whole dressing document: every placement the map carries
/// with the recipes they name, and the biome patches drawn beside them.
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
        if (await SketchPropWrite.PropBodyAsync(HttpContext, ct) is not { } prop) return;
        var outcome = await SketchPropWrite.RunAsync(repo, artifacts, HttpContext, ct,
            doc => DressingEdit.Add(doc, prop));
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
        if (await SketchPropWrite.PropBodyAsync(HttpContext, ct) is not { } prop) return;
        var outcome = await SketchPropWrite.RunAsync(repo, artifacts, HttpContext, ct,
            doc => DressingEdit.Replace(doc, Route<string>("propId")!, prop));
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
            doc => DressingEdit.Remove(doc, Route<string>("propId")!));
        if (outcome.IsAnswered) return;
        if (outcome.IsMissing) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(new PropWrittenDto(outcome.Id), ct);
    }
}

// ── the biome patches, one drawn area at a time ─────────────────────────────────────
//
// A patch places no block, so it is not a prop — but it is a shape an author drew on the same canvas and it
// rides in the same document, so it is addressed the same way. What a map states beside them is the map-wide
// field at `sketch/biome`, which the patches sit on top of.

/// <summary>POST /api/map/{slug}/sketch/biome-patches — draw one patch, answering the id it was given.
///
/// <para>A body stating a free id keeps it; one stating none, or one already taken, is minted
/// <c>biome-{n}</c>. The patch goes on the end, which is the one drawn over everything already
/// there.</para></summary>
public sealed class SketchBiomePatchCreateEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PropWrittenDto>
{
    public override void Configure()
    {
        Post("/map/{slug}/sketch/biome-patches"); AllowAnonymous();
        Description(b => b.Accepts<BiomePatch>("application/json")
                          .Produces<PropWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await SketchPropWrite.BiomeBodyAsync(HttpContext, ct) is not { } patch) return;
        var outcome = await SketchPropWrite.RunAsync(repo, artifacts, HttpContext, ct,
            doc => DressingEdit.AddBiome(doc, patch));
        if (outcome.IsAnswered) return;
        if (outcome.IsMissing) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(new PropWrittenDto(outcome.Id), ct);
    }
}

/// <summary>PATCH /api/map/{slug}/sketch/biome-patches/{patchId} — replace one patch, keeping its place in
/// the drawing order. 404 where the id names no patch.</summary>
public sealed class SketchBiomePatchUpdateEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PropWrittenDto>
{
    public override void Configure()
    {
        Patch("/map/{slug}/sketch/biome-patches/{patchId}"); AllowAnonymous();
        Description(b => b.Accepts<BiomePatch>("application/json")
                          .Produces<PropWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await SketchPropWrite.BiomeBodyAsync(HttpContext, ct) is not { } patch) return;
        var outcome = await SketchPropWrite.RunAsync(repo, artifacts, HttpContext, ct,
            doc => DressingEdit.ReplaceBiome(doc, Route<string>("patchId")!, patch));
        if (outcome.IsAnswered) return;
        if (outcome.IsMissing) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(new PropWrittenDto(outcome.Id), ct);
    }
}

/// <summary>DELETE /api/map/{slug}/sketch/biome-patches/{patchId} — take one patch off the board. The columns
/// it held fall to whatever is under it: an earlier patch, else the map's own field.</summary>
public sealed class SketchBiomePatchDeleteEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PropWrittenDto>
{
    public override void Configure()
    {
        Delete("/map/{slug}/sketch/biome-patches/{patchId}"); AllowAnonymous();
        Description(b => b.Produces<PropWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var outcome = await SketchPropWrite.RunAsync(repo, artifacts, HttpContext, ct,
            doc => DressingEdit.RemoveBiome(doc, Route<string>("patchId")!));
        if (outcome.IsAnswered) return;
        if (outcome.IsMissing) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(new PropWrittenDto(outcome.Id), ct);
    }
}

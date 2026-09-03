using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Endpoints;

// ── the drawing, one layer, one group and one shape at a time ───────────────────────
//
// The last part of the sketch to get an address, and the largest: a board's geometry is where the shapes are,
// which stack they are on and which ground they share. Every route answers the drawing's own types, which is
// what puts a shape's 28 fields, a layer's stacking words and a group's orbit flag in the published schema.
//
// Ids are the address. A shape id is unique across the whole document — the relief is keyed by group id, a
// group lists shape ids, and a placement names one — so a shape answers to its id wherever it is drawn, and a
// group answers under the layer that carries it.

/// <summary>One group of one layer, with the layer it is on and whether a relief is stated over it — the
/// registry a caller reads before keying a relief or naming a group to draw into.</summary>
/// <param name="Layer">The layer that carries the group. A group belongs to one layer, because the fan and
/// the relief are both solved over the shapes of one slab.</param>
/// <param name="Id">What the relief is keyed under and what a shape names to join it.</param>
/// <param name="Name">What the group is called on screen, or null where it states none.</param>
/// <param name="Mirrors">Whether the group's shapes are fanned onto the symmetry orbit. A group that does not
/// mirror is built once, where it was drawn.</param>
/// <param name="ShapeIds">The shapes that share this ground, in the order they were listed.</param>
/// <param name="HasRelief">Whether the layout states an interior elevation for this group — what tells a flat
/// island from one whose surface is solved.</param>
public sealed record SketchGroupAt(
    string Layer, string Id, string? Name, bool Mirrors, IReadOnlyList<string> ShapeIds, bool HasRelief);

/// <summary>GET /api/map/{slug}/sketch/layers — the stack in draw order, each layer with the shapes and
/// groups drawn on it. A layer that named itself keeps its id; one that did not is answered under its
/// position, which is the id every other route addresses it by.</summary>
public sealed class SketchLayerListEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<IReadOnlyList<SketchLayer>>
{
    public override void Configure()
    {
        Get("/map/{slug}/sketch/layers"); AllowAnonymous();
        Description(b => b.Produces<IReadOnlyList<SketchLayer>>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var layout = await SketchGeometryWrite.ReadAsync(artifacts, map.Id, ct);
        if (await artifacts.RevisionAsync(map.Id, ArtifactKind.SketchLayoutJson, ct) is { } revision)
            Revisions.Answer(HttpContext, revision);
        await Send.OkAsync(SketchLayout.Stack(layout), ct);
    }
}

/// <summary>GET /api/map/{slug}/sketch/layers/{layerId} — one layer of the stack, with its shapes and
/// groups. 404 where the id names none.</summary>
public sealed class SketchLayerOfIdEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<SketchLayer>
{
    public override void Configure()
    {
        Get("/map/{slug}/sketch/layers/{layerId}"); AllowAnonymous();
        Description(b => b.Produces<SketchLayer>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var layout = await SketchGeometryWrite.ReadAsync(artifacts, map.Id, ct);
        var id = Route<string>("layerId")!;
        if (SketchLayout.Stack(layout).FirstOrDefault(layer => layer.Id == id) is not { } found)
        {
            await Refusals.NotFoundAsync(HttpContext, "layer", ct, id);
            return;
        }
        await Send.OkAsync(found, ct);
    }
}

/// <summary>PUT /api/map/{slug}/sketch/layers/{layerId} — state one layer of the stack: the height its ground
/// starts at, whether it is terrain or a made thing, and how its floors meet the ground. Creates the layer at
/// the end of the stack where the id names none.
///
/// <para>Stating <c>layout</c> replaces the layer's shapes and groups outright; leaving it out keeps them,
/// because a shape has a route of its own and renaming a layer is not asking to rub its drawing out.</para>
///
/// <para>The layer's own faults ride back as complaints on the 200 rather than refusing: two layers driven
/// into each other (<c>SK10</c>), a made thing seated on nothing (<c>SK16</c>) and a stack out of order
/// (<c>SK20</c>) are all things a board can be saved carrying and finished without.</para></summary>
public sealed class SketchLayerWriteEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PartWrittenDto>
{
    public override void Configure()
    {
        Put("/map/{slug}/sketch/layers/{layerId}"); AllowAnonymous();
        Description(b => b.Accepts<SketchLayer>("application/json")
                          .Produces<PartWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("layerId")!;
        var outcome = await SketchGeometryWrite.RunAsync(repo, artifacts, HttpContext, ct,
            (layoutJson, stated) => SketchGeometryEdit.PutLayer(layoutJson, id, stated), needsBody: true);
        if (outcome.IsAnswered) return;
        if (outcome.IsMissing) { await Refusals.NotFoundAsync(HttpContext, "layer", ct, id); return; }
        await Send.OkAsync(new PartWrittenDto(outcome.Id), ct);
    }
}

/// <summary>DELETE /api/map/{slug}/sketch/layers/{layerId} — take one layer, everything drawn on it and the
/// relief of every group that lived only there off the board.</summary>
public sealed class SketchLayerDeleteEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PartWrittenDto>
{
    public override void Configure()
    {
        Delete("/map/{slug}/sketch/layers/{layerId}"); AllowAnonymous();
        Description(b => b.Produces<PartWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("layerId")!;
        var outcome = await SketchGeometryWrite.RunAsync(repo, artifacts, HttpContext, ct,
            (layoutJson, _) => SketchGeometryEdit.RemoveLayer(layoutJson, id), needsBody: false);
        if (outcome.IsAnswered) return;
        if (outcome.IsMissing) { await Refusals.NotFoundAsync(HttpContext, "layer", ct, id); return; }
        await Send.OkAsync(new PartWrittenDto(outcome.Id), ct);
    }
}

/// <summary>GET /api/map/{slug}/sketch/groups — every group the board carries, across all its layers, with
/// whether a relief is keyed over it. The list a caller reads before naming a group to draw into or to state
/// terrain for.</summary>
public sealed class SketchGroupListEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<IReadOnlyList<SketchGroupAt>>
{
    public override void Configure()
    {
        Get("/map/{slug}/sketch/groups"); AllowAnonymous();
        Description(b => b.Produces<IReadOnlyList<SketchGroupAt>>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var layout = await SketchGeometryWrite.ReadAsync(artifacts, map.Id, ct);
        var relief = layout?.Relief ?? [];
        var groups = SketchLayout.Stack(layout)
            .SelectMany(layer => layer.Groups.Select(group => new SketchGroupAt(
                layer.Id ?? "", group.Id ?? "", group.Name, group.Mirrors, group.ShapeIds,
                group.Id is { Length: > 0 } id && relief.ContainsKey(id))))
            .ToList();
        if (await artifacts.RevisionAsync(map.Id, ArtifactKind.SketchLayoutJson, ct) is { } revision)
            Revisions.Answer(HttpContext, revision);
        await Send.OkAsync(groups, ct);
    }
}

/// <summary>PUT /api/map/{slug}/sketch/layers/{layerId}/groups/{groupId} — state one group whole: what it is
/// called, whether it is fanned onto the symmetry orbit, and which shapes share its ground. Creates it where
/// the layer carries none under that id.
///
/// <para>The group is the unit the ground is decided over: the orbit fan is read off each mirroring group's
/// list, and so is the relief. A shape listed in no group is built once, where it was drawn, on flat
/// ground.</para></summary>
public sealed class SketchGroupWriteEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PartWrittenDto>
{
    public override void Configure()
    {
        Put("/map/{slug}/sketch/layers/{layerId}/groups/{groupId}"); AllowAnonymous();
        Description(b => b.Accepts<SketchGroup>("application/json")
                          .Produces<PartWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var layerId = Route<string>("layerId")!;
        var groupId = Route<string>("groupId")!;
        var outcome = await SketchGeometryWrite.RunAsync(repo, artifacts, HttpContext, ct,
            (layoutJson, stated) => SketchGeometryEdit.PutGroup(layoutJson, layerId, groupId, stated),
            needsBody: true);
        if (outcome.IsAnswered) return;
        if (outcome.IsMissing) { await Refusals.NotFoundAsync(HttpContext, "layer", ct, layerId); return; }
        await Send.OkAsync(new PartWrittenDto(outcome.Id), ct);
    }
}

/// <summary>DELETE /api/map/{slug}/sketch/layers/{layerId}/groups/{groupId} — ungroup. The shapes stay on the
/// layer and are drawn where they were drawn; what goes with the group is the orbit fan and the relief keyed
/// under its id.</summary>
public sealed class SketchGroupDeleteEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PartWrittenDto>
{
    public override void Configure()
    {
        Delete("/map/{slug}/sketch/layers/{layerId}/groups/{groupId}"); AllowAnonymous();
        Description(b => b.Produces<PartWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var layerId = Route<string>("layerId")!;
        var groupId = Route<string>("groupId")!;
        var outcome = await SketchGeometryWrite.RunAsync(repo, artifacts, HttpContext, ct,
            (layoutJson, _) => SketchGeometryEdit.RemoveGroup(layoutJson, layerId, groupId), needsBody: false);
        if (outcome.IsAnswered) return;
        if (outcome.IsMissing) { await Refusals.NotFoundAsync(HttpContext, "group", ct, groupId); return; }
        await Send.OkAsync(new PartWrittenDto(outcome.Id), ct);
    }
}

/// <summary>GET /api/map/{slug}/sketch/layers/{layerId}/shapes — the shapes drawn on one layer, in draw
/// order.</summary>
public sealed class SketchShapeListEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<IReadOnlyList<SketchShape>>
{
    public override void Configure()
    {
        Get("/map/{slug}/sketch/layers/{layerId}/shapes"); AllowAnonymous();
        Description(b => b.Produces<IReadOnlyList<SketchShape>>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var layout = await SketchGeometryWrite.ReadAsync(artifacts, map.Id, ct);
        var id = Route<string>("layerId")!;
        if (SketchLayout.Stack(layout).FirstOrDefault(layer => layer.Id == id) is not { } found)
        {
            await Refusals.NotFoundAsync(HttpContext, "layer", ct, id);
            return;
        }
        await Send.OkAsync(found.Shapes, ct);
    }
}

/// <summary>POST /api/map/{slug}/sketch/layers/{layerId}/shapes — draw one shape on a layer, answering the id
/// it was given.
///
/// <para><c>?group=</c> names the ground it joins, and a name the layer does not carry yet opens a group. A
/// layer that already groups its shapes takes no shape that names none: the orbit fan and the relief are both
/// read off a group's list, so an ungrouped shape on a grouped layer is built once, where it was drawn, on
/// flat ground — which is <c>SK17</c>, and a refusal here is cheaper than a complaint after the save.</para>
///
/// <para>A body stating a free id keeps it; one stating none, or one already drawn, is minted
/// <c>{type}-{n}</c>. The shape goes on the end of the layer, which is the order the rasterizer draws
/// in.</para></summary>
public sealed class SketchShapeCreateEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PartWrittenDto>
{
    public override void Configure()
    {
        Post("/map/{slug}/sketch/layers/{layerId}/shapes"); AllowAnonymous();
        Description(b => b.Accepts<SketchShape>("application/json")
                          .Produces<PartWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var layerId = Route<string>("layerId")!;
        var group = Query<string>("group", isRequired: false);
        var outcome = await SketchGeometryWrite.RunAsync(repo, artifacts, HttpContext, ct,
            (layoutJson, stated) => SketchGeometryEdit.AddShape(layoutJson, layerId, stated, group),
            needsBody: true);
        if (outcome.IsAnswered) return;
        if (outcome.IsMissing) { await Refusals.NotFoundAsync(HttpContext, "layer", ct, layerId); return; }
        await Send.OkAsync(new PartWrittenDto(outcome.Id), ct);
    }
}

/// <summary>GET /api/map/{slug}/sketch/shapes/{shapeId} — one shape, wherever on the stack it is drawn. 404
/// where the id names none.</summary>
public sealed class SketchShapeOfIdEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<SketchShape>
{
    public override void Configure()
    {
        Get("/map/{slug}/sketch/shapes/{shapeId}"); AllowAnonymous();
        Description(b => b.Produces<SketchShape>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var layout = await SketchGeometryWrite.ReadAsync(artifacts, map.Id, ct);
        var id = Route<string>("shapeId")!;
        var shape = SketchLayout.Stack(layout).SelectMany(layer => layer.Shapes)
                                              .FirstOrDefault(drawn => drawn.Id == id);
        if (shape is null) { await Refusals.NotFoundAsync(HttpContext, "shape", ct, id); return; }
        await Send.OkAsync(shape, ct);
    }
}

/// <summary>PATCH /api/map/{slug}/sketch/shapes/{shapeId} — change one shape without restating the board.
///
/// <para>A stated field replaces what the shape carried and a stated <c>null</c> takes the field off, so the
/// one call both writes a height and clears a relief scope. <c>id</c> is the address and is kept whatever the
/// body says.</para>
///
/// <para><c>role</c>, <c>intentRef</c> and <c>height_authored</c> are refused: the first two are the identity
/// a plan recompile matches a shape by, and the third is the mark that a floor was corrected by hand. All
/// three are the compiler's to write, and a caller has no way to state a coherent value for any of
/// them.</para></summary>
public sealed class SketchShapeUpdateEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PartWrittenDto>
{
    public override void Configure()
    {
        Patch("/map/{slug}/sketch/shapes/{shapeId}"); AllowAnonymous();
        Description(b => b.Accepts<SketchShape>("application/json")
                          .Produces<PartWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("shapeId")!;
        var outcome = await SketchGeometryWrite.RunAsync(repo, artifacts, HttpContext, ct,
            (layoutJson, stated) => SketchGeometryEdit.PatchShape(layoutJson, id, stated), needsBody: true);
        if (outcome.IsAnswered) return;
        if (outcome.IsMissing) { await Refusals.NotFoundAsync(HttpContext, "shape", ct, id); return; }
        await Send.OkAsync(new PartWrittenDto(outcome.Id), ct);
    }
}

/// <summary>DELETE /api/map/{slug}/sketch/shapes/{shapeId} — rub one shape out, and take it out of every
/// group that listed it.</summary>
public sealed class SketchShapeDeleteEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PartWrittenDto>
{
    public override void Configure()
    {
        Delete("/map/{slug}/sketch/shapes/{shapeId}"); AllowAnonymous();
        Description(b => b.Produces<PartWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("shapeId")!;
        var outcome = await SketchGeometryWrite.RunAsync(repo, artifacts, HttpContext, ct,
            (layoutJson, _) => SketchGeometryEdit.RemoveShape(layoutJson, id), needsBody: false);
        if (outcome.IsAnswered) return;
        if (outcome.IsMissing) { await Refusals.NotFoundAsync(HttpContext, "shape", ct, id); return; }
        await Send.OkAsync(new PartWrittenDto(outcome.Id), ct);
    }
}

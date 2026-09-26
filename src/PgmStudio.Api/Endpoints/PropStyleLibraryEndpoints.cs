using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Theme;
using PgmStudio.Minecraft.Dressing;

namespace PgmStudio.Api.Endpoints;

// ── trees ─────────────────────────────────────────────────────────────────────────────────────────────
/// <summary>GET /api/tree-styles — the tree library, newest first, each drawn through the grower the export
/// runs. A tree is picked by what it looks like: six woods differ in colour and six species in shape, and
/// neither reads off a number.</summary>
public sealed class TreeStyleListEndpoint(PropStyleLibrary library) : EndpointWithoutRequest<List<TreeStyleSummary>>
{
    public override void Configure() { Get("/tree-styles"); }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync((await library.ListTreesAsync(ct))
            .Select(entry => new TreeStyleSummary(entry.Row.Id, entry.Row.Name, entry.Card)).ToList(), ct);
}

public sealed class TreeStyleGetEndpoint(PropStyleStore store) : EndpointWithoutRequest<TreeStyleDetail>
{
    public override void Configure() { Get("/tree-styles/{id}"); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var row = await store.GetTreeAsync(Route<long>("id"), ct);
        if (row is null) { await Refusals.NotFoundAsync(HttpContext, "tree style", ct); return; }
        await Send.OkAsync(PropStyleLibrary.ToDetail(row), ct);
    }
}

/// <summary>GET /api/tree-styles/{id}/json — the recipe as the dressing document states it, which is what a
/// pull copies into a map's registry under a key.</summary>
public sealed class TreeStyleDocumentEndpoint(PropStyleStore store) : EndpointWithoutRequest<StyleJsonDto>
{
    public override void Configure() { Get("/tree-styles/{id}/json"); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var row = await store.GetTreeAsync(Route<long>("id"), ct);
        if (row is null) { await Refusals.NotFoundAsync(HttpContext, "tree style", ct); return; }
        await Send.OkAsync(new StyleJsonDto(
            DressingJson.SerializeStyle(PropStyleLibrary.TreeOf(row))), ct);
    }
}

/// <summary>POST /api/tree-styles — file a tree recipe. 400 `DR-COPY` when a <c>copied</c> recipe states no
/// cut: a copied tree is one cut out of a world, and nothing but the cutter records one.</summary>
public sealed class TreeStyleCreateEndpoint(PropStyleStore store) : Endpoint<TreeStyleSaveRequest, TreeStyleDetail>
{
    public override void Configure() { Post("/tree-styles"); }

    public override async Task HandleAsync(TreeStyleSaveRequest req, CancellationToken ct)
    {
        if (await Refusals.StopAsync(HttpContext, 400, "invalid tree style", PropStyleLibrary.Check(req), ct)) return;
        var row = PropStyleLibrary.RowOf(req);
        row.Id = await store.CreateTreeAsync(row, ct);
        await Send.OkAsync(PropStyleLibrary.ToDetail(row), ct);
    }
}

/// <summary>PUT /api/tree-styles/{id} — replace a tree recipe. Refuses the way
/// <see cref="TreeStyleCreateEndpoint"/> does, so a re-save of a copied recipe carries the cut its GET
/// answered.</summary>
public sealed class TreeStyleUpdateEndpoint(PropStyleStore store) : Endpoint<TreeStyleSaveRequest, TreeStyleDetail>
{
    public override void Configure() { Put("/tree-styles/{id}"); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(TreeStyleSaveRequest req, CancellationToken ct)
    {
        if (await Refusals.StopAsync(HttpContext, 400, "invalid tree style", PropStyleLibrary.Check(req), ct)) return;
        var id = Route<long>("id");
        var row = PropStyleLibrary.RowOf(req);
        if (!await store.UpdateTreeAsync(id, row, ct))
        { await Refusals.NotFoundAsync(HttpContext, "tree style", ct); return; }
        row.Id = id;
        await Send.OkAsync(PropStyleLibrary.ToDetail(row), ct);
    }
}

public sealed class TreeStyleDraftPreviewEndpoint : Endpoint<TreeStyleSaveRequest, StyleCardDto>
{
    public override void Configure() { Post("/tree-styles/preview"); }

    public override async Task HandleAsync(TreeStyleSaveRequest req, CancellationToken ct)
        => await Send.OkAsync(new StyleCardDto(PropStyleLibrary.CardOf(req)), ct);
}

/// <summary>DELETE /api/tree-styles/{id}. No question is asked, because nothing binds a recipe: a placement
/// names a key in its own document's registry, which the pull copied, so a map keeps its trees when the row
/// they were pulled from goes.</summary>
public sealed class TreeStyleDeleteEndpoint(PropStyleStore store) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/tree-styles/{id}"); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await store.DeleteTreeAsync(Route<long>("id"), ct);
        await Send.NoContentAsync(ct);
    }
}

// ── boulders ──────────────────────────────────────────────────────────────────────────────────────────
public sealed class BoulderStyleListEndpoint(PropStyleLibrary library)
    : EndpointWithoutRequest<List<BoulderStyleSummary>>
{
    public override void Configure() { Get("/boulder-styles"); }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync((await library.ListBouldersAsync(ct))
            .Select(entry => new BoulderStyleSummary(entry.Row.Id, entry.Row.Name, entry.Card)).ToList(), ct);
}

public sealed class BoulderStyleGetEndpoint(PropStyleStore store) : EndpointWithoutRequest<BoulderStyleDetail>
{
    public override void Configure() { Get("/boulder-styles/{id}"); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var row = await store.GetBoulderAsync(Route<long>("id"), ct);
        if (row is null) { await Refusals.NotFoundAsync(HttpContext, "boulder style", ct); return; }
        await Send.OkAsync(PropStyleLibrary.ToDetail(row), ct);
    }
}

public sealed class BoulderStyleDocumentEndpoint(PropStyleStore store) : EndpointWithoutRequest<StyleJsonDto>
{
    public override void Configure() { Get("/boulder-styles/{id}/json"); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var row = await store.GetBoulderAsync(Route<long>("id"), ct);
        if (row is null) { await Refusals.NotFoundAsync(HttpContext, "boulder style", ct); return; }
        await Send.OkAsync(new StyleJsonDto(
            DressingJson.SerializeStyle(PropStyleLibrary.BoulderOf(row))), ct);
    }
}

public sealed class BoulderStyleCreateEndpoint(PropStyleStore store)
    : Endpoint<BoulderStyleSaveRequest, BoulderStyleDetail>
{
    public override void Configure() { Post("/boulder-styles"); }

    public override async Task HandleAsync(BoulderStyleSaveRequest req, CancellationToken ct)
    {
        var row = PropStyleLibrary.RowOf(req);
        row.Id = await store.CreateBoulderAsync(row, ct);
        await Send.OkAsync(PropStyleLibrary.ToDetail(row), ct);
    }
}

public sealed class BoulderStyleUpdateEndpoint(PropStyleStore store)
    : Endpoint<BoulderStyleSaveRequest, BoulderStyleDetail>
{
    public override void Configure() { Put("/boulder-styles/{id}"); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(BoulderStyleSaveRequest req, CancellationToken ct)
    {
        var id = Route<long>("id");
        var row = PropStyleLibrary.RowOf(req);
        if (!await store.UpdateBoulderAsync(id, row, ct))
        { await Refusals.NotFoundAsync(HttpContext, "boulder style", ct); return; }
        row.Id = id;
        await Send.OkAsync(PropStyleLibrary.ToDetail(row), ct);
    }
}

public sealed class BoulderStyleDraftPreviewEndpoint : Endpoint<BoulderStyleSaveRequest, StyleCardDto>
{
    public override void Configure() { Post("/boulder-styles/preview"); }

    public override async Task HandleAsync(BoulderStyleSaveRequest req, CancellationToken ct)
        => await Send.OkAsync(new StyleCardDto(PropStyleLibrary.CardOf(req)), ct);
}

public sealed class BoulderStyleDeleteEndpoint(PropStyleStore store) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/boulder-styles/{id}"); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await store.DeleteBoulderAsync(Route<long>("id"), ct);
        await Send.NoContentAsync(ct);
    }
}

using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Houses;

namespace PgmStudio.Api.Endpoints;

/// <summary>Row ↔ wire-DTO mapping for the house-part library (B71), <see cref="RoomStyleMapping"/>'s sibling
/// one level down.</summary>
internal static class HousePartMapping
{
    public static RoofStyleDetail ToDetail(RoofStyleRow row, IReadOnlyList<RoofStyleCourseRow> courses) =>
        new(row.Id, row.Name, row.Form, row.Pitch, row.Overhang, row.RoofHole, row.RidgeCap,
            [.. courses.Select(c => new RoomCourseDto(c.Part, c.Ordinal, c.StyleId, c.Height))],
            row.RoofSlab, row.RoofSlabData);

    /// <summary>What a saved roof comes back as — read off the <em>row</em> it composes to rather than off the
    /// request, so the clamps the row applies are the numbers the editor is handed back.</summary>
    public static RoofStyleDetail ToDetail(long id, RoofStyleSaveRequest req)
    {
        var row = HousePartLibrary.RowOf(req);
        row.Id = id;
        return ToDetail(row, [.. HousePartLibrary.RoofCourseRowsOf(req)]);
    }

    public static StoreyStyleDetail ToDetail(StoreyStyleRow row, IReadOnlyList<StoreyStyleCourseRow> courses) =>
        new(row.Id, row.Name, row.Clear, row.BorderWidth, row.InlayInset,
            new RoomWindowDto(row.WindowForm, row.WindowBlock, row.WindowData, row.WindowSill,
                row.WindowWidth, row.WindowHeight, row.WindowSpacing),
            [.. courses.Select(c => new RoomCourseDto(c.Part, c.Ordinal, c.StyleId, c.Height))]);

    public static StoreyStyleDetail ToDetail(long id, StoreyStyleSaveRequest req)
    {
        var row = HousePartLibrary.RowOf(req);
        row.Id = id;
        return ToDetail(row, [.. HousePartLibrary.StoreyCourseRowsOf(req)]);
    }

    public static PorchStyleDetail ToDetail(PorchStyleRow row) =>
        new(row.Id, row.Name, row.Depth, row.Inset, row.Edge, row.RoofForm, row.RailBlock);

    public static PorchStyleDetail ToDetail(long id, PorchStyleSaveRequest req)
    {
        var row = HousePartLibrary.RowOf(req);
        row.Id = id;
        return ToDetail(row);
    }
}

// ── roofs ─────────────────────────────────────────────────────────────────────────────────────────────
/// <summary>GET /api/roof-styles — the roof library, newest first, each on the sample building it tops.</summary>
public sealed class RoofStyleListEndpoint(HousePartLibrary library) : EndpointWithoutRequest<List<RoofStyleSummary>>
{
    public override void Configure() { Get("/roof-styles"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync((await library.ComposeRoofsAsync(ct))
            .Select(entry => new RoofStyleSummary(entry.Row.Id, entry.Row.Name, RoomStylePreview.Card(entry.Style)))
            .ToList(), ct);
}

/// <summary>GET /api/roof-styles/{id} — one roof with its per-part courses.</summary>
public sealed class RoofStyleGetEndpoint(HousePartStore store) : EndpointWithoutRequest<RoofStyleDetail>
{
    public override void Configure() { Get("/roof-styles/{id}"); AllowAnonymous(); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var row = await store.GetRoofAsync(id, ct);
        if (row is null) { await Refusals.NotFoundAsync(HttpContext, "roof style", ct); return; }
        await Send.OkAsync(HousePartMapping.ToDetail(row, await store.GetRoofCoursesAsync(id, ct)), ct);
    }
}

/// <summary>POST /api/roof-styles. 400 `{error, findings}` on anything
/// <see cref="HouseStyleValidation.CheckRoof"/> names — a roof or a verge that is a log or a ground material,
/// a slab block that is not a slab, and a slab named as a whole-block roof. The whole roof gate rather than
/// half of it: a roof part states its own <c>roofSlab</c>, so the slab/pitch pairing has both numbers here
/// and a roof saved on its own is held to what a roof bound onto a house is held to.</summary>
public sealed class RoofStyleCreateEndpoint(HousePartStore store, HousePartLibrary library)
    : Endpoint<RoofStyleSaveRequest, RoofStyleDetail>
{
    public override void Configure() { Post("/roof-styles"); AllowAnonymous(); }

    public override async Task HandleAsync(RoofStyleSaveRequest req, CancellationToken ct)
    {
        var composed = await library.ComposeRoofDraftAsync(req, ct);
        var findings = HouseStyleValidation.CheckRoof(composed.Roof);
        if (await Refusals.StopAsync(HttpContext, 400, "invalid house style", findings, ct)) return;
        var id = await store.CreateRoofAsync(
            HousePartLibrary.RowOf(req), HousePartLibrary.RoofCourseRowsOf(req), ct);
        await Send.OkAsync(HousePartMapping.ToDetail(id, req), ct);
    }
}

/// <summary>PUT /api/roof-styles/{id}. Refuses the same way <see cref="RoofStyleCreateEndpoint"/> does.</summary>
public sealed class RoofStyleUpdateEndpoint(HousePartStore store, HousePartLibrary library)
    : Endpoint<RoofStyleSaveRequest, RoofStyleDetail>
{
    public override void Configure() { Put("/roof-styles/{id}"); AllowAnonymous(); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(RoofStyleSaveRequest req, CancellationToken ct)
    {
        var composed = await library.ComposeRoofDraftAsync(req, ct);
        var findings = HouseStyleValidation.CheckRoof(composed.Roof);
        if (await Refusals.StopAsync(HttpContext, 400, "invalid house style", findings, ct)) return;
        var id = Route<long>("id");
        var updated = await store.UpdateRoofAsync(
            id, HousePartLibrary.RowOf(req), HousePartLibrary.RoofCourseRowsOf(req), ct);
        if (!updated) { await Refusals.NotFoundAsync(HttpContext, "roof style", ct); return; }
        await Send.OkAsync(HousePartMapping.ToDetail(id, req), ct);
    }
}

/// <summary>POST /api/roof-styles/preview — the building a draft roof tops, previewed without saving it.</summary>
public sealed class RoofStyleDraftPreviewEndpoint(HousePartLibrary library)
    : Endpoint<RoofStyleSaveRequest, RoomStylePreviewDto>
{
    public override void Configure() { Post("/roof-styles/preview"); AllowAnonymous(); }

    public override async Task HandleAsync(RoofStyleSaveRequest req, CancellationToken ct)
        => await Send.OkAsync(
            RoomStylePreview.Views(await library.ComposeRoofDraftAsync(req, ct), footprint: HttpContext.Footprint()), ct);
}

/// <summary>DELETE /api/roof-styles/{id} — forget a roof, unless a house still wears it. A part bound by a
/// building is one the library must refuse to forget, the answer a style already gives a theme.</summary>
public sealed class RoofStyleDeleteEndpoint(HousePartStore store) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/roof-styles/{id}"); AllowAnonymous(); Description(b => b.Refuses(409)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var used = await store.UsingRoofAsync(id, ct);
        if (used.Count > 0)
        {
            await Refusals.ConflictAsync(HttpContext, "roof style in use",
                $"{used.Count} room style(s) still bind this roof style — unbind them before forgetting it", ct,
                holding: [.. used.Select(name => name.ToString()!)]);
            return;
        }
        await store.DeleteRoofAsync(id, ct);
        await Send.NoContentAsync(ct);
    }
}

// ── storeys ───────────────────────────────────────────────────────────────────────────────────────────
/// <summary>GET /api/storey-styles — the storey library, newest first, each as the one-storey building it
/// makes.</summary>
public sealed class StoreyStyleListEndpoint(HousePartLibrary library)
    : EndpointWithoutRequest<List<StoreyStyleSummary>>
{
    public override void Configure() { Get("/storey-styles"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync((await library.ComposeStoreysAsync(ct))
            .Select(entry => new StoreyStyleSummary(
                entry.Row.Id, entry.Row.Name, entry.Row.Clear, RoomStylePreview.Card(entry.Style)))
            .ToList(), ct);
}

public sealed class StoreyStyleGetEndpoint(HousePartStore store) : EndpointWithoutRequest<StoreyStyleDetail>
{
    public override void Configure() { Get("/storey-styles/{id}"); AllowAnonymous(); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var row = await store.GetStoreyAsync(id, ct);
        if (row is null) { await Refusals.NotFoundAsync(HttpContext, "storey style", ct); return; }
        await Send.OkAsync(HousePartMapping.ToDetail(row, await store.GetStoreyCoursesAsync(id, ct)), ct);
    }
}

/// <summary>POST /api/storey-styles. 400 `{error, findings}` when the storey's window names a block that is not
/// the kind its form needs — a stair lattice or an arched window whose block is not a stair, or a slab band
/// whose block is not a single slab.</summary>
public sealed class StoreyStyleCreateEndpoint(HousePartStore store)
    : Endpoint<StoreyStyleSaveRequest, StoreyStyleDetail>
{
    public override void Configure() { Post("/storey-styles"); AllowAnonymous(); }

    public override async Task HandleAsync(StoreyStyleSaveRequest req, CancellationToken ct)
    {
        var findings = HouseStyleValidation.CheckWindow("windows", HousePartLibrary.WindowOf(HousePartLibrary.RowOf(req)));
        if (await Refusals.StopAsync(HttpContext, 400, "invalid house style", findings, ct)) return;
        var id = await store.CreateStoreyAsync(
            HousePartLibrary.RowOf(req), HousePartLibrary.StoreyCourseRowsOf(req), ct);
        await Send.OkAsync(HousePartMapping.ToDetail(id, req), ct);
    }
}

/// <summary>PUT /api/storey-styles/{id}. Refuses the same way <see cref="StoreyStyleCreateEndpoint"/> does.</summary>
public sealed class StoreyStyleUpdateEndpoint(HousePartStore store)
    : Endpoint<StoreyStyleSaveRequest, StoreyStyleDetail>
{
    public override void Configure() { Put("/storey-styles/{id}"); AllowAnonymous(); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(StoreyStyleSaveRequest req, CancellationToken ct)
    {
        var findings = HouseStyleValidation.CheckWindow("windows", HousePartLibrary.WindowOf(HousePartLibrary.RowOf(req)));
        if (await Refusals.StopAsync(HttpContext, 400, "invalid house style", findings, ct)) return;
        var id = Route<long>("id");
        var updated = await store.UpdateStoreyAsync(
            id, HousePartLibrary.RowOf(req), HousePartLibrary.StoreyCourseRowsOf(req), ct);
        if (!updated) { await Refusals.NotFoundAsync(HttpContext, "storey style", ct); return; }
        await Send.OkAsync(HousePartMapping.ToDetail(id, req), ct);
    }
}

public sealed class StoreyStyleDraftPreviewEndpoint(HousePartLibrary library)
    : Endpoint<StoreyStyleSaveRequest, RoomStylePreviewDto>
{
    public override void Configure() { Post("/storey-styles/preview"); AllowAnonymous(); }

    public override async Task HandleAsync(StoreyStyleSaveRequest req, CancellationToken ct)
        => await Send.OkAsync(
            RoomStylePreview.Views(await library.ComposeStoreyDraftAsync(req, ct), footprint: HttpContext.Footprint()), ct);
}

public sealed class StoreyStyleDeleteEndpoint(HousePartStore store) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/storey-styles/{id}"); AllowAnonymous(); Description(b => b.Refuses(409)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var used = await store.UsingStoreyAsync(id, ct);
        if (used.Count > 0)
        {
            await Refusals.ConflictAsync(HttpContext, "storey style in use",
                $"{used.Count} room style(s) still bind this storey style — unbind them before forgetting it", ct,
                holding: [.. used.Select(name => name.ToString()!)]);
            return;
        }
        await store.DeleteStoreyAsync(id, ct);
        await Send.NoContentAsync(ct);
    }
}

// ── porches ───────────────────────────────────────────────────────────────────────────────────────────
/// <summary>GET /api/porch-styles — the porch library, newest first, each fronting the sample building.</summary>
public sealed class PorchStyleListEndpoint(HousePartLibrary library)
    : EndpointWithoutRequest<List<PorchStyleSummary>>
{
    public override void Configure() { Get("/porch-styles"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync((await library.ComposePorchesAsync(ct))
            .Select(entry => new PorchStyleSummary(entry.Row.Id, entry.Row.Name, RoomStylePreview.Card(entry.Style)))
            .ToList(), ct);
}

public sealed class PorchStyleGetEndpoint(HousePartStore store) : EndpointWithoutRequest<PorchStyleDetail>
{
    public override void Configure() { Get("/porch-styles/{id}"); AllowAnonymous(); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var row = await store.GetPorchAsync(Route<long>("id"), ct);
        if (row is null) { await Refusals.NotFoundAsync(HttpContext, "porch style", ct); return; }
        await Send.OkAsync(HousePartMapping.ToDetail(row), ct);
    }
}

public sealed class PorchStyleCreateEndpoint(HousePartStore store) : Endpoint<PorchStyleSaveRequest, PorchStyleDetail>
{
    public override void Configure() { Post("/porch-styles"); AllowAnonymous(); }

    public override async Task HandleAsync(PorchStyleSaveRequest req, CancellationToken ct)
        => await Send.OkAsync(
            HousePartMapping.ToDetail(await store.CreatePorchAsync(HousePartLibrary.RowOf(req), ct), req), ct);
}

public sealed class PorchStyleUpdateEndpoint(HousePartStore store) : Endpoint<PorchStyleSaveRequest, PorchStyleDetail>
{
    public override void Configure() { Put("/porch-styles/{id}"); AllowAnonymous(); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(PorchStyleSaveRequest req, CancellationToken ct)
    {
        var id = Route<long>("id");
        if (!await store.UpdatePorchAsync(id, HousePartLibrary.RowOf(req), ct))
        { await Refusals.NotFoundAsync(HttpContext, "porch style", ct); return; }
        await Send.OkAsync(HousePartMapping.ToDetail(id, req), ct);
    }
}

public sealed class PorchStyleDraftPreviewEndpoint : Endpoint<PorchStyleSaveRequest, RoomStylePreviewDto>
{
    public override void Configure() { Post("/porch-styles/preview"); AllowAnonymous(); }

    public override async Task HandleAsync(PorchStyleSaveRequest req, CancellationToken ct)
        => await Send.OkAsync(
            RoomStylePreview.Views(HousePartLibrary.ComposePorchDraft(req), footprint: HttpContext.Footprint()), ct);
}

public sealed class PorchStyleDeleteEndpoint(HousePartStore store) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/porch-styles/{id}"); AllowAnonymous(); Description(b => b.Refuses(409)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var used = await store.UsingPorchAsync(id, ct);
        if (used.Count > 0)
        {
            await Refusals.ConflictAsync(HttpContext, "porch style in use",
                $"{used.Count} room style(s) still bind this porch style — unbind them before forgetting it", ct,
                holding: [.. used.Select(name => name.ToString()!)]);
            return;
        }
        await store.DeletePorchAsync(id, ct);
        await Send.NoContentAsync(ct);
    }
}

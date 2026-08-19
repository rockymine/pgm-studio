using System.Text.Json;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Houses;

namespace PgmStudio.Api.Endpoints;

/// <summary>The <c>?format=png&amp;view=…</c> half the two room-style previews share, over
/// <see cref="PngAnswer"/> — written once because both endpoints answer the same picture from a style they
/// reached two different ways.</summary>
internal static class StylePngAnswer
{
    /// <summary>Answer the request as a PNG if one was asked for, and say whether it did. A view name the
    /// preview cannot raster is a 400 naming the ones it can, the same contract the terrain previews keep.
    /// </summary>
    public static async Task<bool> SendStylePngAsync<TEndpoint>(
        this TEndpoint endpoint, HouseStyle style, CancellationToken ct)
        where TEndpoint : IEndpoint
    {
        var http = endpoint.HttpContext;
        if (!PngAnswer.Wanted(http)) return false;
        if (RoomStylePreview.Png(style, PngAnswer.View(http, "section")) is { } png)
        {
            await PngAnswer.WriteAsync(http, png, ct);
            return true;
        }
        http.Response.StatusCode = 400;
        await http.Response.WriteAsJsonAsync(
            new { errors = new[] { RoomStylePreview.PngViews } }, ct);
        return true;
    }
}

/// <summary>Row ↔ wire-DTO mapping for the room-style library (G34b), the sibling of
/// <see cref="ThemeLibraryMapping"/>.</summary>
internal static class RoomStyleMapping
{
    public static RoomStyleDetail ToDetail(
        RoomStyleRow row, IReadOnlyList<RoomStyleCourseRow> courses,
        IReadOnlyList<RoomStyleStoreyRow>? stack = null) =>
        new(row.Id, row.Name, row.FloorDepth, row.WallHeight,
            row.RoofForm, row.Pitch, row.Overhang, row.RoofHole, row.RidgeCap,
            row.BorderWidth, row.InlayInset, row.Storeys, row.StoreyClear,
            new RoomWindowDto(row.WindowForm, row.WindowBlock, row.WindowData, row.WindowSill,
                row.WindowWidth, row.WindowHeight, row.WindowSpacing),
            // A depth of nothing is the row's way of saying no porch, so it comes back as an absent one rather
            // than as a porch nought blocks deep — the editor reads the absence, not the number.
            row.PorchDepth <= 0
                ? null
                : new RoomPorchDto(row.PorchDepth, row.PorchInset, row.PorchEdge, row.PorchRoof, row.PorchRailBlock),
            row.Door, row.DoorHeight,
            row.RoofStyleId, row.PorchStyleId,
            [.. (stack ?? []).OrderBy(s => s.Ordinal)
                .Select(s => new RoomStoreyDto(s.StoreyStyleId, s.Clear))],
            courses.Select(c => new RoomCourseDto(c.Part, c.Ordinal, c.StyleId, c.Height)).ToList());

    /// <summary>What a saved request comes back as — read off the <em>row</em> it composes to rather than off
    /// the request, so the clamps the row applies are the numbers the editor is handed back.</summary>
    public static RoomStyleDetail ToDetail(long id, RoomStyleSaveRequest req)
    {
        var row = RoomStyleLibrary.RowOf(req);
        row.Id = id;
        return ToDetail(row, [.. RoomStyleLibrary.CourseRowsOf(req)], RoomStyleLibrary.StoreyRowsOf(req));
    }
}

/// <summary>GET /api/room-styles — the room-style library, newest first, each with the shell it stamps.</summary>
public sealed class RoomStyleListEndpoint(RoomStyleLibrary library) : EndpointWithoutRequest<List<RoomStyleSummary>>
{
    public override void Configure() { Get("/room-styles"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync((await library.ComposeAllAsync(ct))
            .Select(entry => new RoomStyleSummary(
                entry.Row.Id, entry.Row.Name, RoomStylePreview.Card(entry.Style)))
            .ToList(), ct);
}

/// <summary>GET /api/room-styles/doors — the doors a room may be stamped with. Served rather than restated in
/// the client: the authoritative list is the same <see cref="DoorMaterials"/> table the wool-room block filter
/// is built from, so a door offered here is always one an attacker can break.</summary>
public sealed class RoomDoorListEndpoint : EndpointWithoutRequest<List<DoorOptionDto>>
{
    public override void Configure() { Get("/room-styles/doors"); AllowAnonymous(); }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(DoorMaterials.All.Select(c => new DoorOptionDto(c.Slug, c.Label)).ToList(), ct);
}

/// <summary>GET /api/room-styles/{id} — one room style with its per-part courses.</summary>
public sealed class RoomStyleGetEndpoint(RoomStyleStore store) : EndpointWithoutRequest<RoomStyleDetail>
{
    public override void Configure() { Get("/room-styles/{id}"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var row = await store.GetAsync(id, ct);
        if (row is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(RoomStyleMapping.ToDetail(
            row, await store.GetCoursesAsync(id, ct), await store.GetStoreysAsync(id, ct)), ct);
    }
}

/// <summary>POST /api/room-styles — compose a room style from existing styles. 400 `{error, findings}` when the
/// composed shell fails <see cref="HouseStyleValidation.Check"/> — a block named for a geometric role that is
/// not that kind of block, a doorway that does not clear the least height a door may, or a roof whose own
/// materials are wrong for its pitch or its family. Nothing is corrected silently; the request is refused and
/// the findings say what was wrong.</summary>
public sealed class RoomStyleCreateEndpoint(RoomStyleStore store, RoomStyleLibrary library)
    : Endpoint<RoomStyleSaveRequest, RoomStyleDetail>
{
    public override void Configure() { Post("/room-styles"); AllowAnonymous(); }

    public override async Task HandleAsync(RoomStyleSaveRequest req, CancellationToken ct)
    {
        var findings = HouseStyleValidation.Check(await library.ComposeDraftAsync(req, ct));
        if (await Refusals.StopAsync(HttpContext, 400, "invalid house style", findings, ct)) return;
        var id = await store.CreateAsync(
            RoomStyleLibrary.RowOf(req), RoomStyleLibrary.CourseRowsOf(req),
            RoomStyleLibrary.StoreyRowsOf(req), ct);
        await Send.OkAsync(RoomStyleMapping.ToDetail(id, req), ct);
    }
}

/// <summary>PUT /api/room-styles/{id} — replace a room style's knobs and its whole set of courses. Refuses the
/// same way <see cref="RoomStyleCreateEndpoint"/> does.</summary>
public sealed class RoomStyleUpdateEndpoint(RoomStyleStore store, RoomStyleLibrary library)
    : Endpoint<RoomStyleSaveRequest, RoomStyleDetail>
{
    public override void Configure() { Put("/room-styles/{id}"); AllowAnonymous(); }

    public override async Task HandleAsync(RoomStyleSaveRequest req, CancellationToken ct)
    {
        var findings = HouseStyleValidation.Check(await library.ComposeDraftAsync(req, ct));
        if (await Refusals.StopAsync(HttpContext, 400, "invalid house style", findings, ct)) return;
        var id = Route<long>("id");
        var updated = await store.UpdateAsync(
            id, RoomStyleLibrary.RowOf(req), RoomStyleLibrary.CourseRowsOf(req),
            RoomStyleLibrary.StoreyRowsOf(req), ct);
        if (!updated) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(RoomStyleMapping.ToDetail(id, req), ct);
    }
}

/// <summary>POST /api/room-styles/preview — the shell a set of courses composes to, previewed without saving
/// any of it. What the editor re-renders as courses are bound and knobs are turned.</summary>
public sealed class RoomStyleDraftPreviewEndpoint(RoomStyleLibrary library)
    : Endpoint<RoomStyleSaveRequest, RoomStylePreviewDto>
{
    public override void Configure() { Post("/room-styles/preview"); AllowAnonymous(); }

    public override async Task HandleAsync(RoomStyleSaveRequest req, CancellationToken ct)
    {
        var style = await library.ComposeDraftAsync(req, ct);
        if (await this.SendStylePngAsync(style, ct)) return;
        await Send.OkAsync(RoomStylePreview.Views(style), ct);
    }
}

/// <summary>GET /api/room-styles/{id}/json — the room style assembled into the stamper's own JSON: the form
/// the export consumes and the form a map snapshots when it binds one.</summary>
public sealed class RoomStyleJsonEndpoint(RoomStyleLibrary library) : EndpointWithoutRequest
{
    public override void Configure() { Get("/room-styles/{id}/json"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var style = await library.ComposeAsync(Route<long>("id"), ct);
        if (style is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(new { styleJson = HouseStyleJson.Serialize(style) }, ct);
    }
}

/// <summary>POST /api/room-styles/preview-snapshot — body is a serialized <c>HouseStyle</c>; returns the shell
/// it stamps. What a map's <b>bound</b> style is previewed through: the binding is a snapshot rather than a
/// library id (structures.md §9), so the picture has to come from the snapshot too — reading the library row
/// would show what that row looks like now, which is exactly the drift the snapshot exists to prevent.</summary>
public sealed class RoomStyleSnapshotPreviewEndpoint : EndpointWithoutRequest
{
    public override void Configure() { Post("/room-styles/preview-snapshot"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var json = await RawBody.ReadAsync(HttpContext, ct);
        try
        {
            var style = HouseStyleJson.Deserialize(json, out var unread);
            if (await this.SendStylePngAsync(style, ct)) return;
            await Send.OkAsync(RoomStylePreview.Views(style) with { Warnings = Refusals.Unread(unread) }, ct);
        }
        catch (JsonException ex) { await Refusals.UnreadableAsync(HttpContext, "invalid room style JSON", ex, ct); }
    }
}

/// <summary>DELETE /api/room-styles/{id} — forget a room style (its courses cascade; the styles stay).</summary>
public sealed class RoomStyleDeleteEndpoint(RoomStyleStore store) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/room-styles/{id}"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await store.DeleteAsync(Route<long>("id"), ct);
        await Send.NoContentAsync(ct);
    }
}

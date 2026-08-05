using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Domain;

namespace PgmStudio.Api.Endpoints;

/// <summary>Row ↔ wire-DTO mapping for the room-style library (G34b), the sibling of
/// <see cref="ThemeLibraryMapping"/>.</summary>
internal static class RoomStyleMapping
{
    public static RoomStyleDetail ToDetail(RoomStyleRow row, IReadOnlyList<RoomStyleCourseRow> courses) =>
        new(row.Id, row.Name, row.FloorDepth, row.WallHeight, row.RoofThickness,
            row.Eave, row.RoofHole, row.Door, row.DoorHeight,
            courses.Select(c => new RoomCourseDto(c.Part, c.Ordinal, c.StyleId, c.Height)).ToList());

    public static RoomStyleDetail ToDetail(long id, RoomStyleSaveRequest req) =>
        new(id, req.Name, req.FloorDepth, req.WallHeight, req.RoofThickness,
            req.Eave, req.RoofHole, req.Door, req.DoorHeight, req.Courses);
}

/// <summary>GET /api/room-styles — the room-style library, newest first, each with the shell it stamps.</summary>
public sealed class RoomStyleListEndpoint(RoomStyleLibrary library) : EndpointWithoutRequest<List<RoomStyleSummary>>
{
    public override void Configure() { Get("/room-styles"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync((await library.ComposeAllAsync(ct))
            .Select(entry => new RoomStyleSummary(
                entry.Row.Id, entry.Row.Name, RoomStylePreview.Views(entry.Style).Section))
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
        await Send.OkAsync(RoomStyleMapping.ToDetail(row, await store.GetCoursesAsync(id, ct)), ct);
    }
}

/// <summary>POST /api/room-styles — compose a room style from existing styles.</summary>
public sealed class RoomStyleCreateEndpoint(RoomStyleStore store) : Endpoint<RoomStyleSaveRequest, RoomStyleDetail>
{
    public override void Configure() { Post("/room-styles"); AllowAnonymous(); }

    public override async Task HandleAsync(RoomStyleSaveRequest req, CancellationToken ct)
    {
        var id = await store.CreateAsync(
            RoomStyleLibrary.RowOf(req), RoomStyleLibrary.CourseRowsOf(req), ct);
        await Send.OkAsync(RoomStyleMapping.ToDetail(id, req), ct);
    }
}

/// <summary>PUT /api/room-styles/{id} — replace a room style's knobs and its whole set of courses.</summary>
public sealed class RoomStyleUpdateEndpoint(RoomStyleStore store) : Endpoint<RoomStyleSaveRequest, RoomStyleDetail>
{
    public override void Configure() { Put("/room-styles/{id}"); AllowAnonymous(); }

    public override async Task HandleAsync(RoomStyleSaveRequest req, CancellationToken ct)
    {
        var id = Route<long>("id");
        var updated = await store.UpdateAsync(
            id, RoomStyleLibrary.RowOf(req), RoomStyleLibrary.CourseRowsOf(req), ct);
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
        => await Send.OkAsync(RoomStylePreview.Views(await library.ComposeDraftAsync(req, ct)), ct);
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

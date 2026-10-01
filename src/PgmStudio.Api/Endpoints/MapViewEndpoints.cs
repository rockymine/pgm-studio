using System.Globalization;
using FastEndpoints;
using Microsoft.AspNetCore.WebUtilities;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Api.Endpoints;

// ── the pictures of a board from a player's eye ─────────────────────────────────────
//
// What the Sketch tool's In game phase shows: the board's own straight-down view, the views the studio
// suggests from the built board, and the ones an author kept. A view is only where to stand and what to look
// at; `render/eye` draws it, so the picture always shows the board as it stands now.

/// <summary>The views an author kept, stored beside the layout rather than in it.</summary>
internal static class KeptViews
{
    private const int Reach = 30_000;

    /// <summary>The longest name a view keeps.</summary>
    public const int LongestName = 80;

    public static Task<List<WorldView>> LoadAsync(MapArtifactStore artifacts, long mapId, CancellationToken ct) =>
        artifacts.LoadJsonOrEmptyAsync<List<WorldView>>(mapId, ArtifactKind.MapViewsJson, ct);

    public static Task SaveAsync(MapArtifactStore artifacts, long mapId, List<WorldView> views, CancellationToken ct) =>
        views.Count == 0
            ? artifacts.DeleteAsync(mapId, ArtifactKind.MapViewsJson, ct)
            : artifacts.SaveJsonAsync(mapId, ArtifactKind.MapViewsJson, views, ct);

    public static MapViewDto Dto(WorldView view, bool kept, EyeCamera? eye = null, bool picture = false) =>
        new(view.Id, view.Name, kept, view.LookX, view.LookZ, view.FromX, view.FromZ, view.Y, view.Pitch, view.Query,
            eye is { } camera ? new EyeCameraDto(camera.X, camera.Y, camera.Z, camera.Yaw, camera.Pitch, camera.Fov) : null,
            view.Id == WorldViews.StraightDownId, view.Yaw, picture);

    /// <summary>The kept views with none of them marked as the map's picture, for the one a request marks.</summary>
    public static void Unmark(List<WorldView> kept)
    {
        for (var index = 0; index < kept.Count; index++)
            if (kept[index].Picture) kept[index] = kept[index] with { Picture = false };
    }

    /// <summary>Every view the board keeps: its own straight-down view first — the author's adjustment of it
    /// where one is stored, else the one framed from the built board — then the others kept, in the order
    /// they were kept.</summary>
    public static List<WorldView> Of(List<WorldView> stored, BuiltWorld? built)
    {
        var straightDown = stored.FirstOrDefault(view => view.Id == WorldViews.StraightDownId)
                           ?? (built is null ? null : WorldViews.StraightDown(built));
        return [.. straightDown is null ? [] : new[] { straightDown },
                .. stored.Where(view => view.Id != WorldViews.StraightDownId)];
    }

    /// <summary>What is wrong with a view as a request states it, or null.</summary>
    public static (string Field, string Message)? Fault(MapViewKeepRequest req)
    {
        if (req.FromX.HasValue != req.FromZ.HasValue)
            return ("fromX", "`fromX` and `fromZ` are stated together or not at all");
        foreach (var (field, value) in new (string, int?)[]
                     { ("lookX", req.LookX), ("lookZ", req.LookZ), ("fromX", req.FromX), ("fromZ", req.FromZ) })
            if (value is { } coordinate && Math.Abs(coordinate) > Reach)
                return (field, $"`{field}` is {coordinate}, which is off any board — within ±{Reach}");
        if (req.Y is { } y && (double.IsNaN(y) || y < 0 || y > 320))
            return ("y", "`y` is the eye's height, from 0 to 320");
        if (req.Pitch is { } pitch && (double.IsNaN(pitch) || pitch < -90 || pitch > 90))
            return ("pitch", "`pitch` is degrees below the horizon, from −90 to 90");
        if (req.Yaw is { } yaw && !double.IsFinite(yaw))
            return ("yaw", "`yaw` is a number of degrees");
        if (req.Yaw is not null && (req.FromX is null || req.Y is null))
            return ("yaw", "a view that states its `yaw` states the camera whole: `fromX`, `fromZ` and `y` with it");
        return null;
    }

    /// <summary>The name a request states, cut to <see cref="LongestName"/>, or null where it states none.</summary>
    public static string? Named(MapViewKeepRequest req) =>
        req.Name?.Trim() is { Length: > 0 } stated ? stated[..Math.Min(stated.Length, LongestName)] : null;
}

/// <summary>GET /api/map/{slug}/views — every picture of the board worth drawing: the board's own
/// straight-down view, kept by default; the studio's suggestions from the built board
/// (<see cref="WorldViews"/>); then the other views kept. A map with no sketch layout has no world to frame
/// or suggest from and answers only what it kept. Each view carries the camera it resolves to on the board as
/// built, which is where an eye left to find its own place ends up; <c>undrawable</c> says why no picture can
/// be drawn here, where the server has no block textures.</summary>
[Queued]
public sealed class MapViewListEndpoint(
    MapRepository repo, MapReader reader, MapArtifactStore artifacts, BlockTextureStore textures)
    : EndpointWithoutRequest<MapViewsDto>
{
    public override void Configure()
    {
        Get("/map/{slug}/views");
        Description(b => b.Produces<MapViewsDto>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var read = await WorldReads.LoadAsync(map, reader, artifacts, ct);
        var kept = KeptViews.Of(await KeptViews.LoadAsync(artifacts, map.Id, ct), read?.Built);
        var suggested = read is null ? [] : WorldViews.Suggested(read.Built);
        var (set, reason) = await textures.GetAsync(ct);
        var eyes = read is null || set is null ? [] : await ResolveAsync(read.Built, set, [.. kept, .. suggested], ct);
        var picture = (read is null ? kept.FirstOrDefault(view => view.Picture) : WorldViews.PictureOf(read.Built, kept))?.Id;

        await Send.OkAsync(new MapViewsDto(
            [.. kept.Take(1).Select(view => KeptViews.Dto(view, kept: true, eyes.GetValueOrDefault(view.Id), view.Id == picture)),
             .. suggested.Select(view => KeptViews.Dto(view, kept: false, eyes.GetValueOrDefault(view.Id), view.Id == picture)),
             .. kept.Skip(1).Select(view => KeptViews.Dto(view, kept: true, eyes.GetValueOrDefault(view.Id), view.Id == picture))],
            set is null ? reason ?? "no block textures" : null), ct);
    }

    /// <summary>The camera each view resolves to on the scene its pictures are drawn from, by id.</summary>
    private static async Task<Dictionary<string, EyeCamera>> ResolveAsync(
        BuiltWorld built, BlockTextureSet set, IReadOnlyList<WorldView> views, CancellationToken ct)
    {
        var eyes = new Dictionary<string, EyeCamera>();
        using (await EyeRenders.TurnAsync(ct))
        {
            var scene = EyeRenders.Scene(built, set, flat: false);
            foreach (var view in views)
            {
                var words = QueryHelpers.ParseQuery(view.Query);
                if (EyeAim.Read(word => words.TryGetValue(word, out var value) ? value.ToString() : null).Resolve(scene)
                    is ({ } camera, _))
                    eyes[view.Id] = camera;
            }
        }
        return eyes;
    }
}

/// <summary>POST /api/map/{slug}/views — keep a view, answering it with the id it was given. 400 where a
/// stand point states one coordinate without the other, a coordinate is off any board, the eye's height is
/// outside the world, or its pitch is past straight up or down.</summary>
public sealed class MapViewKeepEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : Endpoint<MapViewKeepRequest, MapViewDto>
{
    public override void Configure()
    {
        Post("/map/{slug}/views");
        Description(b => b.Produces<MapViewDto>(200, "application/json").Refuses(400, 404));
    }

    public override async Task HandleAsync(MapViewKeepRequest req, CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        if (KeptViews.Fault(req) is { } fault)
        {
            await Refusals.UnreadableAsync(HttpContext, "not a view", fault.Message, ct, fault.Field);
            return;
        }

        var kept = await KeptViews.LoadAsync(artifacts, map.Id, ct);
        var number = kept.Select(view => view.Id.StartsWith("view-", StringComparison.Ordinal)
                                         && int.TryParse(view.Id.AsSpan(5), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
                         .DefaultIfEmpty(0).Max() + 1;
        var name = KeptViews.Named(req) ?? string.Create(CultureInfo.InvariantCulture, $"View {number}");
        var view = new WorldView(string.Create(CultureInfo.InvariantCulture, $"view-{number}"), name,
                                 req.LookX, req.LookZ, req.FromX, req.FromZ, req.Y, req.Pitch, req.Yaw is { } yaw ? Heading.Wrap(yaw) : null,
                                 req.Picture == true);
        if (view.Picture) KeptViews.Unmark(kept);
        kept.Add(view);
        await KeptViews.SaveAsync(artifacts, map.Id, kept, ct);
        await Send.OkAsync(KeptViews.Dto(view, kept: true, picture: view.Picture), ct);
    }
}

/// <summary>PUT /api/map/{slug}/views/{viewId} — change a kept view: where it stands, what it looks at, how
/// high and how far tipped, and its name, which a blank keeps. The board's own straight-down view is changed
/// the same way, and from then on the change is what is kept in place of the framed one. 404 where no kept
/// view has that id; 400 as for keeping one.</summary>
public sealed class MapViewChangeEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : Endpoint<MapViewKeepRequest, MapViewDto>
{
    public override void Configure()
    {
        Put("/map/{slug}/views/{viewId}");
        Description(b => b.Produces<MapViewDto>(200, "application/json").Refuses(400, 404));
    }

    public override async Task HandleAsync(MapViewKeepRequest req, CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var id = Route<string>("viewId")!;
        if (KeptViews.Fault(req) is { } fault)
        {
            await Refusals.UnreadableAsync(HttpContext, "not a view", fault.Message, ct, fault.Field);
            return;
        }

        var kept = await KeptViews.LoadAsync(artifacts, map.Id, ct);
        var was = kept.FindIndex(view => view.Id == id);
        if (was < 0 && id != WorldViews.StraightDownId)
        {
            await Refusals.NotFoundAsync(HttpContext, "kept view", ct, named: id);
            return;
        }
        var name = KeptViews.Named(req) ?? (was >= 0 ? kept[was].Name : "Straight down");
        var view = new WorldView(id, name, req.LookX, req.LookZ, req.FromX, req.FromZ, req.Y, req.Pitch, req.Yaw is { } yaw ? Heading.Wrap(yaw) : null,
                                 req.Picture ?? (was >= 0 && kept[was].Picture));
        if (view.Picture) KeptViews.Unmark(kept);
        if (was >= 0) kept[was] = view;
        else kept.Insert(0, view);
        await KeptViews.SaveAsync(artifacts, map.Id, kept, ct);
        await Send.OkAsync(KeptViews.Dto(view, kept: true, picture: view.Picture), ct);
    }
}

/// <summary>DELETE /api/map/{slug}/views/{viewId} — stop keeping a view, answering the view let go. 404 where
/// no kept view has that id; a suggestion is not kept, so it cannot be deleted. The board's own straight-down
/// view is never let go: deleting an adjusted one puts the framed one back, and deleting the framed one is a
/// 409.</summary>
public sealed class MapViewDeleteEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<MapViewDto>
{
    public override void Configure()
    {
        Delete("/map/{slug}/views/{viewId}");
        Description(b => b.Produces<MapViewDto>(200, "application/json").Refuses(404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var id = Route<string>("viewId")!;
        var kept = await KeptViews.LoadAsync(artifacts, map.Id, ct);
        if (kept.FirstOrDefault(view => view.Id == id) is not { } gone)
        {
            if (id == WorldViews.StraightDownId)
            {
                await Refusals.ConflictAsync(HttpContext, "kept by every board",
                    "the straight-down view is every board's own and is not let go — change it with PUT", ct);
                return;
            }
            await Refusals.NotFoundAsync(HttpContext, "kept view", ct, named: id);
            return;
        }
        kept.Remove(gone);
        await KeptViews.SaveAsync(artifacts, map.Id, kept, ct);
        await Send.OkAsync(KeptViews.Dto(gone, kept: true), ct);
    }
}

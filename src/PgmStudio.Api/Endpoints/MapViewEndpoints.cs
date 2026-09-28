using System.Globalization;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Export;

namespace PgmStudio.Api.Endpoints;

// ── the pictures of a board from a player's eye ─────────────────────────────────────
//
// What the Sketch tool's In game phase shows: the views the studio suggests from the built board, and the
// ones an author kept. A view is only where to stand and what to look at; `render/eye` draws it, so the
// picture always shows the board as it stands now.

/// <summary>The views an author kept, stored beside the layout rather than in it.</summary>
internal static class KeptViews
{
    public static Task<List<WorldView>> LoadAsync(MapArtifactStore artifacts, long mapId, CancellationToken ct) =>
        artifacts.LoadJsonOrEmptyAsync<List<WorldView>>(mapId, ArtifactKind.MapViewsJson, ct);

    public static Task SaveAsync(MapArtifactStore artifacts, long mapId, List<WorldView> views, CancellationToken ct) =>
        views.Count == 0
            ? artifacts.DeleteAsync(mapId, ArtifactKind.MapViewsJson, ct)
            : artifacts.SaveJsonAsync(mapId, ArtifactKind.MapViewsJson, views, ct);

    public static MapViewDto Dto(WorldView view, bool kept) =>
        new(view.Id, view.Name, kept, view.LookX, view.LookZ, view.FromX, view.FromZ, view.Y, view.Pitch, view.Query);
}

/// <summary>GET /api/map/{slug}/views — every picture of the board worth drawing: the studio's suggestions
/// from the built board first (<see cref="WorldViews"/>), then the views kept. A map with no sketch layout has
/// no world to suggest from and answers only what it kept. <c>undrawable</c> says why no picture can be drawn
/// here, where the server has no block textures.</summary>
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
        var suggested = read is null ? [] : WorldViews.Suggested(read.Built);
        var kept = await KeptViews.LoadAsync(artifacts, map.Id, ct);
        var (set, reason) = await textures.GetAsync(ct);
        await Send.OkAsync(new MapViewsDto(
            [.. suggested.Select(view => KeptViews.Dto(view, kept: false)), .. kept.Select(view => KeptViews.Dto(view, kept: true))],
            set is null ? reason ?? "no block textures" : null), ct);
    }
}

/// <summary>POST /api/map/{slug}/views — keep a view, answering it with the id it was given. 400 where a
/// stand point states one coordinate without the other, a coordinate is off any board, the eye's height is
/// outside the world, or its pitch is past straight up or down.</summary>
public sealed class MapViewKeepEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : Endpoint<MapViewKeepRequest, MapViewDto>
{
    private const int Reach = 30_000, LongestName = 80;

    public override void Configure()
    {
        Post("/map/{slug}/views");
        Description(b => b.Produces<MapViewDto>(200, "application/json").Refuses(400, 404));
    }

    public override async Task HandleAsync(MapViewKeepRequest req, CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        if (Fault(req) is { } fault)
        {
            await Refusals.UnreadableAsync(HttpContext, "not a view", fault.Message, ct, fault.Field);
            return;
        }

        var kept = await KeptViews.LoadAsync(artifacts, map.Id, ct);
        var number = kept.Select(view => view.Id.StartsWith("view-", StringComparison.Ordinal)
                                         && int.TryParse(view.Id.AsSpan(5), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
                         .DefaultIfEmpty(0).Max() + 1;
        var name = req.Name?.Trim() is { Length: > 0 } stated
            ? stated[..Math.Min(stated.Length, LongestName)]
            : string.Create(CultureInfo.InvariantCulture, $"View {number}");
        var view = new WorldView(string.Create(CultureInfo.InvariantCulture, $"view-{number}"), name,
                                 req.LookX, req.LookZ, req.FromX, req.FromZ, req.Y, req.Pitch);
        kept.Add(view);
        await KeptViews.SaveAsync(artifacts, map.Id, kept, ct);
        await Send.OkAsync(KeptViews.Dto(view, kept: true), ct);
    }

    private static (string Field, string Message)? Fault(MapViewKeepRequest req)
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
        return null;
    }
}

/// <summary>DELETE /api/map/{slug}/views/{viewId} — stop keeping a view, answering the view let go. 404 where
/// no kept view has that id; a suggestion is not kept, so it cannot be deleted.</summary>
public sealed class MapViewDeleteEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<MapViewDto>
{
    public override void Configure()
    {
        Delete("/map/{slug}/views/{viewId}");
        Description(b => b.Produces<MapViewDto>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var id = Route<string>("viewId")!;
        var kept = await KeptViews.LoadAsync(artifacts, map.Id, ct);
        if (kept.FirstOrDefault(view => view.Id == id) is not { } gone)
        {
            await Refusals.NotFoundAsync(HttpContext, "kept view", ct, named: id);
            return;
        }
        kept.Remove(gone);
        await KeptViews.SaveAsync(artifacts, map.Id, kept, ct);
        await Send.OkAsync(KeptViews.Dto(gone, kept: true), ct);
    }
}

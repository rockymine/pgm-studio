using Microsoft.AspNetCore.Http;
using PgmStudio.Analysis.Playability;
using PgmStudio.Api.Services;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The map a route is about, loaded from the <c>{slug}</c> in its path.
///
/// <para>Nearly every route under <c>/map/{slug}</c> opens by loading that map and answering <c>404</c> if it
/// is not stored, and there is exactly one right way to do it: the subject the path names is not there, which
/// is <c>RQ4</c> at 404 in the one envelope. Written out at each route it is two lines that say nothing about
/// what the route does, and thirty-seven chances for one of them to answer something else.</para>
///
/// <para>The slug is read off the request rather than passed in, because the route parameter is the whole
/// input: a route that loads a map by anything other than its own <c>{slug}</c> is doing something else and
/// should say so in its own words.</para>
/// </summary>
internal static class MapOfRoute
{
    /// <summary>The map the route's <c>{slug}</c> names, or <c>null</c> — in which case the 404 has already
    /// been written and the caller's only job is to return. The whole prologue is
    /// <c>if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;</c>.</summary>
    public static async Task<MapRow?> OfRouteAsync(
        this MapRepository repo, HttpContext http, CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(http.Request.RouteValues["slug"] as string ?? "", ct);
        if (map is null) await Refusals.NotFoundAsync(http, "map", ct);
        return map;
    }

    /// <summary>The same map with its parsed document, for a route that reads what is in it rather than the
    /// row. Ten analysis reads open this way, and the document is always the next thing they ask for.
    /// <c>if (await repo.WithDocOfRouteAsync(reader, HttpContext, ct) is not ({ } map, { } doc)) return;</c>.
    /// </summary>
    public static async Task<(MapRow Map, Dict Doc)?> WithDocOfRouteAsync(
        this MapRepository repo, MapReader reader, HttpContext http, CancellationToken ct)
    {
        if (await repo.OfRouteAsync(http, ct) is not { } map) return null;
        return (map, await reader.ReadDocAsync(map, ct));
    }

    /// <summary>The same map and document, plus the goals the author has stated — for the reads that ask
    /// where a match is played between rather than what the document holds. A map whose goals are not placed
    /// yet carries none of them in its document, and a read that took the document alone would answer over
    /// its spawns and call the rest of the board dead.
    /// <c>if (await repo.WithGoalsOfRouteAsync(reader, artifacts, HttpContext, ct) is not ({ } map, { } doc,
    /// { } goals)) return;</c>.</summary>
    public static async Task<(MapRow Map, Dict Doc, List<NavPoint> Goals)?> WithGoalsOfRouteAsync(
        this MapRepository repo, MapReader reader, MapArtifactStore artifacts, HttpContext http,
        CancellationToken ct)
    {
        if (await repo.WithDocOfRouteAsync(reader, http, ct) is not ({ } map, { } doc)) return null;
        var intent = await artifacts.LoadJsonOrEmptyAsync<MapIntent>(map.Id, ArtifactKind.MapIntentJson, ct);
        return (map, doc, DeclaredGoals.Of(intent));
    }
}

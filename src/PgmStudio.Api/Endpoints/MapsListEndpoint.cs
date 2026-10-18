using FastEndpoints;
using PgmStudio.Api.Access;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// GET /api/maps[?stage=sketch|configure|edit] — the dashboard map list.
///
/// <para><c>stage</c> selects a collection, and the three collections are not all the same kind of thing,
/// because a map's <b>layers</b> and its <b>progress</b> are not the same thing. <c>sketch</c> names an
/// authoring layer, so it lists every map <em>holding</em> a sketch wherever it has since got to — a sketch
/// that was built into a world is still a sketch. <c>configure</c> and <c>edit</c> name lifecycle positions,
/// so they list the maps standing there.</para>
///
/// <para>Every row carries its credited authors and when it was last written, which is what the Maps page
/// filters and sorts by, and when the caller last wrote it themselves, which is what the landing page's
/// continue card picks from.</para>
///
/// <para>Every row carries its layers either way (<see cref="MapSummary.HasSketch"/> /
/// <see cref="MapSummary.HasSurface"/>), so the list can offer each
/// tool the map has been through directly rather than walking a map back one stage at a time. One artifact
/// query serves the flags and the layer filter alike.</para>
///
/// <para>A map an agent made is listed only to the admins and its owner (<see cref="Callers.MaySee"/>).</para>
/// </summary>
public sealed class MapsListEndpoint(MapRepository repo, MapArtifactStore artifacts, MapChangeLog changes, Callers callers)
    : EndpointWithoutRequest<List<MapSummary>>
{
    public override void Configure()
    {
        Get("/maps");
        Description(b => b.Reads(new QueryWord("stage", "The collection to list: `sketch` lists every map holding a "
            + "sketch, `configure` and `edit` the maps standing at that stage. Absent lists them all.",
            [.. Words.Of(typeof(MapStage))])));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var stage = Query<string?>("stage", isRequired: false);
        var layer = stage is MapStage.Sketch;
        var maps = !MapStage.IsValid(stage) || layer
            ? await repo.ListAsync(ct)
            : await repo.ListByStageAsync(stage!, ct);

        var owners = await artifacts.HoldersAsync(
            [ArtifactKind.SurfaceParquet, ArtifactKind.SketchLayoutJson], ct);
        var withSurface = owners[ArtifactKind.SurfaceParquet];
        var withSketch = owners[ArtifactKind.SketchLayoutJson];

        if (layer) maps = [.. maps.Where(m => withSketch.Contains(m.Id))];

        var caller = await callers.OfAsync(HttpContext, ct);
        maps = [.. maps.Where(m => Callers.MaySee(caller, m))];
        var gamemodes = await repo.GamemodesAsync(ct);
        var credited = await repo.AuthorsAsync(ct);
        var wrote = caller.SignedIn
            ? await changes.LastWrittenByAsync(caller.Uuid, caller.Uuid is null ? caller.Name : null, ct)
            : new Dictionary<string, DateTime>();
        await Send.OkAsync(
            maps.Select(m => new MapSummary(
                m.Slug, m.Name, gamemodes.GetValueOrDefault(m.Id, []),
                m.Version, m.Objective, m.Stage,
                [.. credited.GetValueOrDefault(m.Id, []).Select(a => new MapAuthorDto(a.Uuid, a.Role, a.Contribution, a.Name))],
                DateTime.SpecifyKind(m.UpdatedAt, DateTimeKind.Utc),
                withSurface.Contains(m.Id), withSketch.Contains(m.Id),
                wrote.TryGetValue(m.Slug, out var at) ? at : null, m.AgentMade)).ToList(), ct);
    }
}

/// <summary>GET /api/maps/stage-counts — the landing's map count, each counting exactly what the Maps page
/// shows when its card opens it: the maps standing at Sketch, at Configure — which lists a map the corpus import
/// stood at <c>edit</c> too — and every map, each over the maps the caller may see.</summary>
public sealed class MapStageCountsEndpoint(MapRepository repo, Callers callers) : EndpointWithoutRequest<MapStageCounts>
{
    public override void Configure()
    {
        Get("/maps/stage-counts");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var caller = await callers.OfAsync(HttpContext, ct);
        var c = (await repo.ListAsync(ct)).Where(m => Callers.MaySee(caller, m))
            .GroupBy(m => m.Stage).ToDictionary(g => g.Key, g => g.Count());
        await Send.OkAsync(new MapStageCounts(
            c.GetValueOrDefault(MapStage.Sketch),
            c.GetValueOrDefault(MapStage.Configure) + c.GetValueOrDefault(MapStage.Edit),
            c.Values.Sum()), ct);
    }
}

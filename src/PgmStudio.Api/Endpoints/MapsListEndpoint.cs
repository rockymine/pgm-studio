using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// GET /api/maps[?stage=plan|sketch|configure|edit] — the dashboard map list.
///
/// <para><c>stage</c> selects a collection, and the four collections are not all the same kind of thing,
/// because a map's <b>layers</b> and its <b>progress</b> are not the same thing. <c>plan</c> and
/// <c>sketch</c> name authoring layers, so they list every map <em>holding</em> that layer wherever it has
/// since got to — a plan that was built into a world is still a plan, and hiding it once its stage moved on
/// is what made a map's own plan unreachable from the Plans list. <c>configure</c> and <c>edit</c> name
/// lifecycle positions, so they list the maps standing there.</para>
///
/// <para>Every row carries its layers either way (<see cref="MapSummary.HasPlan"/> /
/// <see cref="MapSummary.HasSketch"/> / <see cref="MapSummary.HasSurface"/>), so the list can offer each
/// tool the map has been through directly rather than walking a map back one stage at a time. One artifact
/// query serves the flags and the layer filter alike.</para>
/// </summary>
public sealed class MapsListEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest<List<MapSummary>>
{
    public override void Configure()
    {
        Get("/maps");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var stage = Query<string?>("stage", isRequired: false);
        var layer = stage is MapStage.Plan or MapStage.Sketch;
        var maps = !MapStage.IsValid(stage) || layer
            ? await repo.ListAsync(ct)
            : await repo.ListByStageAsync(stage!, ct);

        var owned = await db.Artifacts
            .Where(a => a.Kind == ArtifactKind.LayerParquet
                        || a.Kind == ArtifactKind.SketchLayoutJson
                        || a.Kind == ArtifactKind.PlanJson)
            .Select(a => new { a.MapId, a.Kind }).Distinct().ToListAsync(ct);
        var owners = owned.GroupBy(a => a.Kind)
            .ToDictionary(g => g.Key, g => g.Select(a => a.MapId).ToHashSet());
        HashSet<long> Owners(string kind) => owners.GetValueOrDefault(kind, []);
        var withSurface = Owners(ArtifactKind.LayerParquet);
        var withSketch = Owners(ArtifactKind.SketchLayoutJson);
        var withPlan = Owners(ArtifactKind.PlanJson);

        if (layer)
        {
            var holders = stage == MapStage.Plan ? withPlan : withSketch;
            maps = [.. maps.Where(m => holders.Contains(m.Id))];
        }

        var gamemodes = await repo.GamemodesAsync(ct);
        await Send.OkAsync(
            maps.Select(m => new MapSummary(
                m.Slug, m.Name, gamemodes.GetValueOrDefault(m.Id, []),
                m.Version, m.Objective, m.Stage,
                withSurface.Contains(m.Id), withPlan.Contains(m.Id), withSketch.Contains(m.Id))).ToList(), ct);
    }
}

/// <summary>GET /api/maps/stage-counts — the landing cards' tallies, each counting exactly what its list
/// shows: sketches by the layer a map holds, the other two by the stage a map stands at.</summary>
public sealed class MapStageCountsEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest<MapStageCounts>
{
    public override void Configure()
    {
        Get("/maps/stage-counts");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var c = await repo.StageCountsAsync(ct);
        var sketched = await db.Artifacts
            .Where(a => a.Kind == ArtifactKind.SketchLayoutJson)
            .Select(a => a.MapId).Distinct().CountAsync(ct);
        await Send.OkAsync(new MapStageCounts(
            sketched,
            c.GetValueOrDefault(MapStage.Configure),
            c.GetValueOrDefault(MapStage.Edit)), ct);
    }
}

using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Endpoints;

/// <summary>GET /api/maps[?stage=sketch|configure|edit] — the dashboard map list, optionally one stage.
/// Each entry carries <see cref="MapSummary.HasSurface"/>, set from the maps that own a cached surface-layer
/// artifact (a top-down block render is available for those), <see cref="MapSummary.Gamemodes"/>,
/// derived from the objective rows rather than the <c>&lt;gamemode&gt;</c> label, and
/// <see cref="MapSummary.ReopenStage"/>, the authoring stage the map can be sent back to
/// (see <see cref="MapReopen"/>). One artifact query serves all three.</summary>
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
        var maps = MapStage.IsValid(stage)
            ? await repo.ListByStageAsync(stage!, ct)
            : await repo.ListAsync(ct);
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

        var gamemodes = await repo.GamemodesAsync(ct);
        await Send.OkAsync(
            maps.Select(m => new MapSummary(
                m.Slug, m.Name, gamemodes.GetValueOrDefault(m.Id, []),
                m.Version, m.Objective, m.Stage, withSurface.Contains(m.Id),
                MapReopen.TargetFor(withSketch.Contains(m.Id), withPlan.Contains(m.Id), m.Stage))).ToList(), ct);
    }
}

/// <summary>GET /api/maps/stage-counts — map count per lifecycle stage (the landing cards).</summary>
public sealed class MapStageCountsEndpoint(MapRepository repo) : EndpointWithoutRequest<MapStageCounts>
{
    public override void Configure()
    {
        Get("/maps/stage-counts");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var c = await repo.StageCountsAsync(ct);
        await Send.OkAsync(new MapStageCounts(
            c.GetValueOrDefault(MapStage.Sketch),
            c.GetValueOrDefault(MapStage.Configure),
            c.GetValueOrDefault(MapStage.Edit)), ct);
    }
}

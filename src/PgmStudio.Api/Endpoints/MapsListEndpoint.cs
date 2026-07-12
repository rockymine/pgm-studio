using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Endpoints;

/// <summary>GET /api/maps[?stage=sketch|configure|edit] — the dashboard map list, optionally one stage.
/// Each entry carries <see cref="MapSummary.HasSurface"/>, set from the maps that own a cached surface-layer
/// artifact (a top-down block render is available for those).</summary>
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
        var withSurface = (await db.Artifacts
            .Where(a => a.Kind == ArtifactKind.LayerParquet)
            .Select(a => a.MapId).Distinct().ToListAsync(ct)).ToHashSet();
        await Send.OkAsync(
            maps.Select(m => new MapSummary(m.Slug, m.Name, m.Gamemode, m.Version, m.Objective, m.Stage, withSurface.Contains(m.Id))).ToList(), ct);
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

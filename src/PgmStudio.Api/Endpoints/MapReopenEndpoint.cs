using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// Which authoring stage a map can be sent back to. <c>map.stage</c> is a single pointer that only ever
/// moves forward (sketch-finish advances it to <c>configure</c>), so the stage itself cannot say where a
/// map came from; the durable record of that is the authoring artifact it kept. A map holding a
/// <see cref="ArtifactKind.SketchLayoutJson"/> was drawn in the Sketch tool and can go back to it; one
/// holding a <see cref="ArtifactKind.PlanJson"/> was authored in the Plan editor. An imported world has
/// neither and can only move forward — its <see cref="ArtifactKind.IslandSketchJson"/> is a derived
/// outline, not an authored source, and deliberately does not count.
/// </summary>
internal static class MapReopen
{
    /// <summary>The stage a map with these artifacts reopens into, or null when it has no authored source.
    /// A map that was planned and then sketched holds both blobs and reopens into the Sketch tool: the
    /// sketch is the later record, and the plan is reachable from its own stage once there.</summary>
    public static string? TargetFor(bool hasSketchLayout, bool hasPlan) =>
        hasSketchLayout ? MapStage.Sketch
        : hasPlan ? MapStage.Plan
        : null;

    /// <summary>The reopen target of one map, read from its artifact rows.</summary>
    public static async Task<string?> TargetForMapAsync(PgmDb db, long mapId, CancellationToken ct)
    {
        var kinds = await db.Artifacts
            .Where(a => a.MapId == mapId
                        && (a.Kind == ArtifactKind.SketchLayoutJson || a.Kind == ArtifactKind.PlanJson))
            .Select(a => a.Kind).Distinct().ToListAsync(ct);
        return TargetFor(kinds.Contains(ArtifactKind.SketchLayoutJson), kinds.Contains(ArtifactKind.PlanJson));
    }
}

/// <summary>POST /api/map/{slug}/reopen — send a map back to the authoring stage it was drawn in, so it
/// lists (and opens) there again. Only a map that kept an authoring source can go back: a sketch layout
/// reopens into <c>sketch</c>, a plan blob into <c>plan</c>, and an imported world into neither (422).
/// The stage pointer is all that moves — the geometry, intent and document rows stay as they are, and
/// finishing the sketch again advances it back to <c>configure</c>. Returns
/// <c>{slug, stage, url, reopened}</c>, with <c>reopened=false</c> for a map already at its target stage.</summary>
public sealed class MapReopenEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/reopen"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var target = await MapReopen.TargetForMapAsync(db, map.Id, ct);
        if (target is null)
        {
            await Send.ResponseAsync(
                new { error = "This map has no sketch or plan to reopen — it was imported, not drawn." }, 422, ct);
            return;
        }

        var reopened = map.Stage != target;
        if (reopened) await repo.SetStageAsync(map.Id, target, ct);
        await Send.OkAsync(new { slug = map.Slug, stage = target, url = $"/maps/{map.Slug}/{target}", reopened }, ct);
    }
}

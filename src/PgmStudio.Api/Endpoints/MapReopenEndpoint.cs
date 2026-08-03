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
    /// <summary>The stage a map with these artifacts reopens into, or null when there is nowhere to go.
    /// The sketch layout is the later record, so it is offered first; a map already sitting in that stage
    /// reopens into the other source it kept. A plan built onto its own map row holds both blobs, and that
    /// is what lets it reach both tools — from Configuring back to the sketch, and from the sketch back to
    /// the plan it was compiled from.</summary>
    public static string? TargetFor(bool hasSketchLayout, bool hasPlan, string? currentStage)
    {
        string?[] authored = [hasSketchLayout ? MapStage.Sketch : null, hasPlan ? MapStage.Plan : null];
        return authored.FirstOrDefault(stage => stage is not null && stage != currentStage);
    }

    /// <summary>The reopen target of one map, read from its artifact rows.</summary>
    public static async Task<string?> TargetForMapAsync(PgmDb db, long mapId, string currentStage, CancellationToken ct)
    {
        var kinds = await db.Artifacts
            .Where(a => a.MapId == mapId
                        && (a.Kind == ArtifactKind.SketchLayoutJson || a.Kind == ArtifactKind.PlanJson))
            .Select(a => a.Kind).Distinct().ToListAsync(ct);
        return TargetFor(kinds.Contains(ArtifactKind.SketchLayoutJson), kinds.Contains(ArtifactKind.PlanJson), currentStage);
    }
}

/// <summary>POST /api/map/{slug}/reopen — send a map back to an authoring stage it was drawn in, so it
/// lists (and opens) there again. Only a map that kept an authoring source can go: a sketch layout
/// reopens into <c>sketch</c>, a plan blob into <c>plan</c>, and a map with neither left to move to — an
/// imported world, or one already sitting in its only authored stage — is refused (422). The stage
/// pointer is all that moves: the geometry, intent and document rows stay as they are, and finishing the
/// sketch again advances it back to <c>configure</c>. Returns <c>{slug, stage, url}</c>.</summary>
public sealed class MapReopenEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/reopen"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var target = await MapReopen.TargetForMapAsync(db, map.Id, map.Stage, ct);
        if (target is null)
        {
            await Send.ResponseAsync(
                new { error = "This map has no sketch or plan to reopen into." }, 422, ct);
            return;
        }

        await repo.SetStageAsync(map.Id, target, ct);
        await Send.OkAsync(new { slug = map.Slug, stage = target, url = $"/maps/{map.Slug}/{target}" }, ct);
    }
}

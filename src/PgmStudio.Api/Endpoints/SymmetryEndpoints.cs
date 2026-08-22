using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FastEndpoints;
using PgmStudio.Api.Services;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Analysis.Footprint;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;
using PgmStudio.Contracts;

/// <summary>
/// GET /api/map/{slug}/symmetry — global symmetry of the map's islands (B7). Returns the cached
/// symmetry_json artifact, or computes it on demand from the islands_json artifact (excluding the
/// Configure-excluded islands) and caches it with status "unconfirmed".
/// </summary>
public sealed class SymmetryGetEndpoint(MapRepository repo, PgmDb db, MapArtifactStore artifacts) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/map/{slug}/symmetry");
        AllowAnonymous();
        // Declared rather than sent as the record: the answer is the stored row rebuilt into the
        // symmetry.json shape by SymmetryStore.ToJson, and a second builder here would be free to
        // disagree with it. SymmetryShapeTests holds the record to what that writes.
        Description(b => b.Produces<SymmetryDto>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var existing = await SymmetryStore.LoadAsync(db, map.Id, ct);
        if (existing is not null) { await Send.OkAsync(SymmetryStore.ToJson(existing), ct); return; }

        var islandsJson = await artifacts.LoadAsync(map.Id, ArtifactKind.IslandsJson, ct);
        if (islandsJson is null) { await Refusals.NotFoundAsync(HttpContext, "island decomposition", ct); return; }

        var exclude = await ScanConfig.ExcludedIslandsAsync(artifacts, map.Id, ct);
        var islands = SymmetrySupport.ParseIslands(islandsJson, exclude);
        var result = SymmetryDetector.Detect(islands);
        var row = SymmetryStore.FromDetection(map.Id, result, "unconfirmed");
        await SymmetryStore.SaveAsync(db, row, ct);
        await Send.OkAsync(SymmetryStore.ToJson(row), ct);
    }

}

/// <summary>
/// PATCH /api/map/{slug}/symmetry — confirm/reject the detected symmetry (B7). Updates status
/// ("confirmed"/"none"), an optional user-override confirmed_type, and an optional centre override.
/// Mirrors the reference patch_symmetry.
/// </summary>
public sealed class SymmetryPatchEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest<AppliedDto>
{
    public override void Configure()
    {
        Patch("/map/{slug}/symmetry"); AllowAnonymous();
        Description(b => b.Accepts<SymmetryPatchRequest>("application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var stated = await SymmetryConfirm.StateAsync(
            db, map.Id, await RawBody.ReadAsync(HttpContext, ct), ct);
        if (stated.Refusal is { } refusal) { await Refusals.WriteAsync(HttpContext, refusal, ct); return; }

        await Send.OkAsync(new AppliedDto(), ct);
    }
}

using System.Text;
using System.Text.Json;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>GET /api/map/{slug}/intent — the map's declarative authoring intent
/// (docs/pgm/new-map-authoring.md), empty if none yet.</summary>
public sealed class IntentGetEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest<MapIntent>
{
    public override void Configure() { Get("/map/{slug}/intent"); AllowAnonymous(); Description(b => b.Refuses(404)); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        if (await artifacts.RevisionAsync(map.Id, ArtifactKind.MapIntentJson, ct) is { } revision)
            Revisions.Answer(HttpContext, revision);
        await Send.OkAsync(await artifacts.LoadJsonOrEmptyAsync<MapIntent>(map.Id, ArtifactKind.MapIntentJson, ct), ct);
    }
}

/// <summary>PUT /api/map/{slug}/intent — store the intent the author edited and regenerate the map from
/// it. Replaces the stored intent wholesale, which is what makes a deletion in Configure stick.</summary>
public sealed class IntentPutEndpoint(MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts, PlayerLookup players) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Put("/map/{slug}/intent");
        AllowAnonymous();
        Description(b => b.Accepts<MapIntent>("application/json").Produces<AppliedDto>(200, "application/json").Refuses(404, 409, 422));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var body = await RawBody.ReadAsync(HttpContext, ct);
        Complaints.Unread(HttpContext, body, IntentWrite.Stated(body));

        var applied = await IntentWrite.StoreAndProjectAsync(repo, reader, writer, artifacts, players, slug,
            map.Id, body, Revisions.Expected(HttpContext), ct);
        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }
}

/// <summary>
/// PUT /api/map/{slug}/intent/from-plan — store a freshly compiled intent, carrying the map's authored
/// slices onto it first (<see cref="IntentCarry"/>), then project it exactly as a normal PUT does.
///
/// <para>The plan owns the map's structure, so a rebuild is meant to replace its teams, spawns, wools and
/// build zones. What it does not own is who wrote the map, and two separate things keep that true. The
/// projection leaves the map's people alone where the intent names none, which is the rule for every intent
/// write and not only this one. And <see cref="IntentCarry.CarryAuthored"/> copies <c>meta.authors</c> and
/// <c>meta.contributors</c> off the stored intent onto the compiled one, so the artifact this route stores
/// still says who the map is by — the projection would keep them either way, and an intent that disagreed
/// with the map about its own credits is the thing being avoided. <b>The confirmed symmetry and the island-team tags are deliberately NOT carried</b>
/// — see <see cref="IntentCarry"/> for why each would do harm rather than preserve an answer — so a rebuild
/// clears both and Configure's World and Teams phases are walked again. The layout write is the same shape for
/// a different reason (<c>…/sketch/from-plan</c>), where the finish does ride across.</para>
/// </summary>
public sealed class IntentFromPlanEndpoint(MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts, PlayerLookup players) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Put("/map/{slug}/intent/from-plan");
        AllowAnonymous();
        Description(b => b.Accepts<MapIntent>("application/json").Produces<AppliedDto>(200, "application/json").Refuses(404, 409, 422));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var compiled = await RawBody.ReadAsync(HttpContext, ct);
        // Over the posted document rather than the merged one: the carry only adds back this map's own
        // credits, so a field named here is one the caller wrote and can correct.
        Complaints.Unread(HttpContext, compiled, IntentWrite.Stated(compiled));

        var stored = await artifacts.LoadAsync(map.Id, ArtifactKind.MapIntentJson, ct);
        var merged = IntentCarry.CarryAuthored(compiled, stored is null ? null : Encoding.UTF8.GetString(stored));

        var applied = await IntentWrite.StoreAndProjectAsync(repo, reader, writer, artifacts, players, slug,
            map.Id, merged, Revisions.Expected(HttpContext), ct);
        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }
}

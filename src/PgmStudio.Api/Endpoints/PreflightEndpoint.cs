using FastEndpoints;
using PgmStudio.Analysis.Playability;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// GET /api/map/{slug}/preflight — the Review phase's pre-flight gate (new-map-authoring.md §6). Runs the
/// four generated-map checks and reports the export verdict:
/// <list type="number">
/// <item><b>Round-trip</b> + <b>Mirror</b> — pure codec/categorizer checks (<see cref="Preflight"/>).</item>
/// <item><b>Buildability</b> — every spawn / wool / monument placement must sit over solid ground (not
/// open void), reusing <see cref="Editability"/>'s Y=0 read.</item>
/// <item><b>Traversability</b> — the spawn↔wool chain must be connected, reusing <see cref="Traversability"/>
/// — the same check <c>GET /xml</c> enforces as its 409 gate.</item>
/// </list>
/// <c>ExportReady</c> is true iff round-trip passes (else <c>GET /xml</c> would throw) and traversability is
/// connected (else it returns 409); mirror + buildability are advisory. Scoped to intent-authored maps —
/// a corpus map (no intent blob) has nothing to pre-flight and reports <c>IntentMap=false</c>.
/// </summary>
[Queued]
public sealed class PreflightEndpoint(MapRepository repo, MapReader reader, FeatureData feature, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PreflightDto>
{

    public override void Configure() { Get("/map/{slug}/preflight"); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        await Send.OkAsync(await Preflights.OfAsync(map, reader, feature, artifacts, ct), ct);
    }
}

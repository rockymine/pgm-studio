using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Data.Repositories;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// POST /api/map/{slug}/wiring/apply — apply one wiring template (F1). Body: {template, params}.
/// The caller chooses template + region (e.g. after grouping in R1); emits standard Filter + ApplyRule
/// (+ compound) entries via the editors and saves. (No suggestion endpoint — see <see cref="FilterWiring"/>.)
/// </summary>
public sealed class WiringApplyEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/wiring/apply"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var body = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var template = body.GetValueOrDefault("template") as string ?? "";
        var p = body.GetValueOrDefault("params") as Dict ?? new Dict();
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!,
            doc => FilterWiring.ApplyTemplate(doc, template, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

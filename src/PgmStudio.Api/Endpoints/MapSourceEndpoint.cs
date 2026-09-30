using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// PUT /api/map/{slug}/source — store a map from its source: a plan, which is compiled here, or a layout and an
/// intent drawn without one, with the refinement stating everything the base cannot. One request compiles,
/// applies the refinement — the outlines reshaped and bent in the document — gates, stores the documents as one
/// change, rasterizes the drawing and projects the intent. A map already stored under the slug is replaced, by
/// someone who may edit it, and keeps its notes, its history and its kept views.
///
/// <para><c>?dry=true</c> decides everything the source can be refused for, answers what it would change in the
/// documents the map holds and the layout and intent it would store, and stores nothing. The work itself is
/// <see cref="MapSource"/> — the order the steps run in is the operation's, not this route's.</para>
/// </summary>
public sealed class MapSourceEndpoint(
    MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts,
    WorldFeatureWriter features, PgmDb db, PlayerLookup players, MapChangeLog log)
    : EndpointWithoutRequest<MapSourceDto>
{
    public override void Configure()
    {
        Put("/map/{slug}/source");
        Description(b => b.Accepts<MapSourceRequest>("application/json").Refuses(422).Reads(
            new QueryWord("dry", "Decide everything the source can be refused for and answer what it would change "
                + "and the layout and intent it would store, storing nothing. Absent stores it.", ["true", "false"])));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var applied = await MapSource.ApplyAsync(
            HttpContext, Route<string>("slug")!, await RawBody.ReadAsync(HttpContext, ct),
            string.Equals(Query<string?>("dry", isRequired: false), "true", StringComparison.OrdinalIgnoreCase),
            repo, reader, writer, artifacts, features, db, players, log, ct);
        if (applied.Refusal is { } refusal)
        {
            await Refusals.WriteAsync(HttpContext, refusal, ct);
            return;
        }
        await Send.OkAsync(applied.Answer!, ct);
    }
}

using System.Text.Json;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// A map's source (<c>PUT /map/{slug}/source</c>): the base it is built from — a plan, which the studio compiles,
/// or a drawn layout and intent — the refinement stating everything the base cannot, and what the source says
/// about itself.
///
/// <para>Each document is read as it was written, by the reader that takes it, so what binds to this record is
/// the rest; the documents' types are here to name them in the schema.</para>
/// </summary>
/// <param name="Plan">The board as cell rectangles. Stated alone it is the base, compiled into the layout and the
/// intent; stated beside a drawn layout and intent it is kept as the plan they were drawn from, and not
/// compiled.</param>
/// <param name="Layout">The drawing, where the board is its drawing rather than its plan's compile; stated with its
/// intent.</param>
/// <param name="Intent">What the drawn board is played for; stated with its layout.</param>
/// <param name="Refinement">Everything the base cannot state, applied onto it before anything is judged, and kept
/// as the map's refinement.</param>
/// <param name="After">The change the source was built against. Every later change is one it has not seen, and a
/// map made from a refinement refuses a source over one (<c>SR1</c>). Absent takes the change the map's source was
/// last applied as.</param>
/// <param name="Name">What to call the map. Falls back to the intent's own <c>meta.name</c>.</param>
/// <param name="Origin">Where the documents were built from, kept on the change the source lands as.</param>
/// <param name="Note">What this source is, in a sentence — a pass, the notes it answers — kept on the change it
/// lands as. At most 1,000 characters.</param>
public sealed record MapSourceRequest(
    PlanModel? Plan = null,
    SketchLayout? Layout = null,
    MapIntent? Intent = null,
    Refinement? Refinement = null,
    long? After = null,
    string? Name = null,
    ChangeOrigin? Origin = null,
    string? Note = null);

/// <summary>
/// PUT /api/map/{slug}/source — store a map from its source: a plan, which is compiled here, or a layout and an
/// intent drawn without one, with the refinement stating everything the base cannot. One request compiles,
/// applies the refinement — the outlines reshaped and bent in the document — gates, stores the documents as one
/// change, rasterizes the drawing and projects the intent. A map already stored under the slug is replaced, by
/// someone who may edit it, and keeps its notes, its history and its kept views.
///
/// <para>A map made from a refinement refuses a source applied over changes it has not seen, 409 with the changes
/// handed over (<c>SR1</c>), unless <c>?discard=</c> names them. <c>?dry=true</c> decides everything the source
/// can be refused for, answers what it would change in the documents the map holds and the layout and intent it
/// would store, and stores nothing. The work itself is <see cref="MapSource"/> — the order the steps run in is
/// the operation's, not this route's.</para>
/// </summary>
public sealed class MapSourceEndpoint(
    MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts,
    WorldFeatureWriter features, PgmDb db, PlayerLookup players, MapChangeLog log, LibraryNames names)
    : EndpointWithoutRequest<MapSourceDto>
{
    public override void Configure()
    {
        Put("/map/{slug}/source");
        Description(b => b.Accepts<MapSourceRequest>("application/json").Refuses(409, 422).Reads(
            new QueryWord("dry", "Decide everything the source can be refused for and answer what it would change "
                + "and the layout and intent it would store, storing nothing. Absent stores it.", ["true", "false"]),
            new QueryWord("discard", "The changes the source drops, by number — `8,9`: changes it has not seen, "
                + "which it replaces rather than takes in. The change it lands as records them.")));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var applied = await MapSource.ApplyAsync(
            HttpContext, Route<string>("slug")!, await RawBody.ReadAsync(HttpContext, ct),
            string.Equals(Query<string?>("dry", isRequired: false), "true", StringComparison.OrdinalIgnoreCase),
            Query<string?>("discard", isRequired: false),
            repo, reader, writer, artifacts, features, db, players, log, names, ct);
        if (applied.Refusal is { } refusal)
        {
            await Refusals.WriteAsync(HttpContext, refusal, ct);
            return;
        }
        await Send.OkAsync(applied.Answer!, ct);
    }
}

/// <summary>GET /api/map/{slug}/refinement — the refinement the map's source last stated, <c>{}</c> where it stated
/// none. 404 where no source has been applied to the map.</summary>
public sealed class MapRefinementGetEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/map/{slug}/refinement");
        // The document as stored; see SketchGetEndpoint for why the shape is declared rather than sent.
        Description(b => b.Produces<Refinement>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        if (await artifacts.LoadAsync(map.Id, ArtifactKind.RefinementJson, ct) is not { } data)
        {
            await Refusals.NotFoundAsync(HttpContext, "refinement", ct);
            return;
        }
        if (await artifacts.RevisionAsync(map.Id, ArtifactKind.RefinementJson, ct) is { } revision)
            Revisions.Answer(HttpContext, revision);
        await Send.OkAsync(JsonSerializer.Deserialize<JsonElement>(data), ct);
    }
}

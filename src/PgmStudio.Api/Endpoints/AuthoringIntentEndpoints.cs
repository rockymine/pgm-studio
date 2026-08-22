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
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }
        if (await artifacts.RevisionAsync(map.Id, ArtifactKind.MapIntentJson, ct) is { } revision)
            Revisions.Answer(HttpContext, revision);
        await Send.OkAsync(await artifacts.LoadJsonOrEmptyAsync<MapIntent>(map.Id, ArtifactKind.MapIntentJson, ct), ct);
    }
}

/// <summary>
/// Storing an intent on a map: persist the <c>map_intent_json</c> artifact, then project it into the PGM
/// document (<see cref="TeamsGenerator"/>) and save through the normal codec path. Idempotent — the
/// generator clears its own prior output and the save path is entity-replace, so re-storing a corrected
/// intent rewrites the spawn structure cleanly.
/// <para>Both routes below go through here, so an author's edit and a rebuild from the plan cannot
/// regenerate a map differently; they differ only in what reaches this point.</para>
/// </summary>
internal static class IntentWrite
{
    /// <summary>Store the intent and project it into the map document. The <c>If-Match</c> guards the
    /// <b>intent</b>, which is the document the caller read and posted; the projection that follows rewrites
    /// the map from it, so guarding both would refuse a write against a map the caller never claimed to have
    /// read.</summary>
    public static async Task<(int Status, object? Body)> StoreAndProjectAsync(
        HttpContext http, MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts,
        MojangClient mojang, string slug, long mapId, string body, CancellationToken ct)
    {
        var intent = Stated(body) ?? new MapIntent();

        var stored = await Writes.StoreAsync(http, artifacts, mapId, ArtifactKind.MapIntentJson,
            JsonSerializer.SerializeToUtf8Bytes(intent, MapArtifactStore.Json), ct);
        if (stored is null)
            return (Revisions.StaleStatus, Revisions.Stale(http, "intent",
                await artifacts.RevisionAsync(mapId, ArtifactKind.MapIntentJson, ct)));

        // A stated name is looked up here (async, outside the pure generator) so an account gets its uuid.
        var authors = await ResolveAuthorsAsync(mojang, intent, ct);
        return await WriteSupport.RunEditAsync(http, repo, reader, writer, slug,
            doc => { IntentGenerator.Apply(doc, intent); if (authors is not null) doc["authors"] = authors; return new Dict(); },
            ct, guarded: false);
    }

    /// <summary>What a body states as an intent, or null where it states none. A body that will not read as
    /// one is the request's own fault (<c>RQ1</c>), answered where the body is read.</summary>
    public static MapIntent? Stated(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<MapIntent>(json, MapArtifactStore.Json); }
        catch (JsonException) { return null; }
    }

    // Turn each stated author/contributor into {uuid, name, role, contribution}. PGM takes a person as an
    // account — a `uuid` it resolves to a player — or a pseudonym, the element's own text, and either alone
    // is a whole author, so a name Mojang does not know is kept as a pseudonym rather than dropped: the
    // intent already models one (`AuthorIntentJson` reads a bare string into `Name`) and the codec already
    // writes one (`XmlWriter.WriteAuthors` emits `<author>Name</author>` when the uuid is empty).
    //
    // Null is what an intent naming nobody answers, and the caller leaves the map's own people alone for it.
    // The intent owns the map's structure and not its credits: those are stated through PATCH …/metadata and
    // live in the map's rows, so a projection that wrote an empty list here would clear an answer it was
    // never given — which is what a compiled intent does, since it carries a `meta` naming the map and no
    // people in it. Clearing the authors is the metadata route's, where a stated empty list means exactly
    // that.
    private static async Task<List<object?>?> ResolveAuthorsAsync(MojangClient mojang, MapIntent intent, CancellationToken ct)
    {
        if (intent.Meta is not { } m) return null;
        var resolved = new List<object?>();
        async Task Add(IEnumerable<AuthorIntent> people, string role)
        {
            foreach (var person in people.Where(p => p.Name.Trim().Length > 0))
            {
                var stated = person.Name.Trim();
                var (uuid, name) = ("", stated);
                try { (uuid, name) = await mojang.LookupAsync(stated, ct); }
                catch { /* not an account — the stated name stands on its own as a pseudonym */ }
                resolved.Add(new Dict
                {
                    ["uuid"] = uuid, ["name"] = name, ["role"] = role,
                    ["contribution"] = person.Contribution,
                });
            }
        }
        await Add(m.Authors, "author");
        await Add(m.Contributors, "contributor");
        return resolved.Count > 0 ? resolved : null;
    }
}

/// <summary>PUT /api/map/{slug}/intent — store the intent the author edited and regenerate the map from
/// it. Replaces the stored intent wholesale, which is what makes a deletion in Configure stick.</summary>
public sealed class IntentPutEndpoint(MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts, MojangClient mojang) : EndpointWithoutRequest
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
        var map = await repo.GetBySlugAsync(slug, ct);
        if (map is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }

        var body = await RawBody.ReadAsync(HttpContext, ct);
        Complaints.Unread(HttpContext, body, IntentWrite.Stated(body));

        var (status, resp) = await IntentWrite.StoreAndProjectAsync(HttpContext, repo, reader, writer, artifacts, mojang, slug, map.Id, body, ct);
        await Send.ResponseAsync(resp!, status, ct);
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
public sealed class IntentFromPlanEndpoint(MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts, MojangClient mojang) : EndpointWithoutRequest
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
        var map = await repo.GetBySlugAsync(slug, ct);
        if (map is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }

        var compiled = await RawBody.ReadAsync(HttpContext, ct);
        // Over the posted document rather than the merged one: the carry only adds back this map's own
        // credits, so a field named here is one the caller wrote and can correct.
        Complaints.Unread(HttpContext, compiled, IntentWrite.Stated(compiled));

        var stored = await artifacts.LoadAsync(map.Id, ArtifactKind.MapIntentJson, ct);
        var merged = IntentCarry.CarryAuthored(compiled, stored is null ? null : Encoding.UTF8.GetString(stored));

        var (status, resp) = await IntentWrite.StoreAndProjectAsync(HttpContext, repo, reader, writer, artifacts, mojang, slug, map.Id, merged, ct);
        await Send.ResponseAsync(resp!, status, ct);
    }
}

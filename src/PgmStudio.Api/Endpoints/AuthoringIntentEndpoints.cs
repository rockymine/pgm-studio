using System.Text;
using System.Text.Json;
using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Api.Services;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Declarative authoring intent store (docs/contracts/new-map-authoring.md): the <c>map_intent_json</c>
/// artifact that is the source of truth for a new map. Mirrors <see cref="RegionDraftStore"/> — it lives
/// outside the entity-replace codec so it survives <c>MapWriter.SaveDocAsync</c>.
/// </summary>
internal static class IntentStore
{
    // Web defaults = camelCase + case-insensitive, so the blob matches what the Blazor client sends.
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<MapIntent> LoadAsync(PgmDb db, long mapId, CancellationToken ct)
    {
        var art = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == mapId && a.Kind == ArtifactKind.MapIntentJson, ct);
        return art is null ? new MapIntent() : JsonSerializer.Deserialize<MapIntent>(art.Data, Json) ?? new MapIntent();
    }

    /// <summary>True iff the map was authored through the declarative intent model (has a stored intent
    /// blob). The export gate uses this to scope the traversability check to intent maps only — corpus
    /// maps have no intent and keep exporting unconditionally.</summary>
    public static Task<bool> HasAsync(PgmDb db, long mapId, CancellationToken ct) =>
        db.Artifacts.AnyAsync(a => a.MapId == mapId && a.Kind == ArtifactKind.MapIntentJson, ct);

    public static async Task SaveAsync(PgmDb db, long mapId, MapIntent intent, CancellationToken ct)
    {
        await db.Artifacts.Where(a => a.MapId == mapId && a.Kind == ArtifactKind.MapIntentJson).DeleteAsync(ct);
        await db.InsertAsync(new MapArtifactRow
        {
            MapId = mapId, Kind = ArtifactKind.MapIntentJson,
            Data = JsonSerializer.SerializeToUtf8Bytes(intent, Json),
        }, token: ct);
    }
}

/// <summary>GET /api/map/{slug}/intent — the map's declarative authoring intent (empty if none yet).</summary>
public sealed class IntentGetEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/intent"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(await IntentStore.LoadAsync(db, map.Id, ct), ct);
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
    public static async Task<(int Status, object? Body)> StoreAndProjectAsync(
        MapRepository repo, MapReader reader, MapWriter writer, PgmDb db, MojangClient mojang,
        string slug, long mapId, string body, CancellationToken ct)
    {
        var intent = (string.IsNullOrWhiteSpace(body) ? null : JsonSerializer.Deserialize<MapIntent>(body, IntentStore.Json)) ?? new MapIntent();

        await IntentStore.SaveAsync(db, mapId, intent, ct);
        // Authors/contributors are usernames → resolve to uuids here (async, outside the pure generator).
        var authors = await ResolveAuthorsAsync(mojang, intent, ct);
        return await WriteSupport.RunEditAsync(repo, reader, writer, slug,
            doc => { IntentGenerator.Apply(doc, intent); if (authors is not null) doc["authors"] = authors; return new Dict(); }, ct);
    }

    // Resolve each author/contributor username to {uuid, name, role, contribution}; unresolved names
    // are skipped (mirrors MetadataEndpoint, which drops entries without a uuid). Null = leave authors
    // untouched.
    private static async Task<List<object?>?> ResolveAuthorsAsync(MojangClient mojang, MapIntent intent, CancellationToken ct)
    {
        if (intent.Meta is not { } m) return null;
        var resolved = new List<object?>();
        async Task Add(IEnumerable<AuthorIntent> people, string role)
        {
            foreach (var person in people.Where(p => p.Name.Trim().Length > 0))
            {
                try
                {
                    var (uuid, canonical) = await mojang.LookupAsync(person.Name.Trim(), ct);
                    resolved.Add(new Dict
                    {
                        ["uuid"] = uuid, ["name"] = canonical, ["role"] = role,
                        ["contribution"] = person.Contribution,
                    });
                }
                catch { /* unknown username / lookup failed — skip this entry */ }
            }
        }
        await Add(m.Authors, "author");
        await Add(m.Contributors, "contributor");
        return resolved;
    }
}

/// <summary>PUT /api/map/{slug}/intent — store the intent the author edited and regenerate the map from
/// it. Replaces the stored intent wholesale, which is what makes a deletion in Configure stick.</summary>
public sealed class IntentPutEndpoint(MapRepository repo, MapReader reader, MapWriter writer, PgmDb db, MojangClient mojang) : EndpointWithoutRequest
{
    public override void Configure() { Put("/map/{slug}/intent"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        var map = await repo.GetBySlugAsync(slug, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        using var sr = new StreamReader(HttpContext.Request.Body);
        var body = await sr.ReadToEndAsync(ct);

        var (status, resp) = await IntentWrite.StoreAndProjectAsync(repo, reader, writer, db, mojang, slug, map.Id, body, ct);
        await Send.ResponseAsync(resp!, status, ct);
    }
}

/// <summary>
/// PUT /api/map/{slug}/intent/from-plan — store a freshly compiled intent, carrying the map's authored
/// slices onto it first (<see cref="IntentCarry"/>), then project it exactly as a normal PUT does.
///
/// <para>The plan owns the map's structure, so a rebuild is meant to replace its teams, spawns, wools and
/// build zones. What it does not own is who wrote the map — the compiler states <c>authors</c> and leaves it
/// empty, so a plain replace wiped the credits off every map that had been through Configure. That is the
/// whole of what <see cref="IntentCarry.CarryAuthored"/> carries: <c>meta.authors</c> and
/// <c>meta.contributors</c>. <b>The confirmed symmetry and the island-team tags are deliberately NOT carried</b>
/// — see <see cref="IntentCarry"/> for why each would do harm rather than preserve an answer — so a rebuild
/// clears both and Configure's World and Teams phases are walked again. The layout write is the same shape for
/// a different reason (<c>…/sketch/from-plan</c>), where the finish does ride across.</para>
/// </summary>
public sealed class IntentFromPlanEndpoint(MapRepository repo, MapReader reader, MapWriter writer, PgmDb db, MojangClient mojang) : EndpointWithoutRequest
{
    public override void Configure() { Put("/map/{slug}/intent/from-plan"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        var map = await repo.GetBySlugAsync(slug, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        using var sr = new StreamReader(HttpContext.Request.Body);
        var compiled = await sr.ReadToEndAsync(ct);

        var stored = await db.Artifacts
            .FirstOrDefaultAsync(a => a.MapId == map.Id && a.Kind == ArtifactKind.MapIntentJson, ct);
        var merged = IntentCarry.CarryAuthored(compiled, stored is null ? null : Encoding.UTF8.GetString(stored.Data));

        var (status, resp) = await IntentWrite.StoreAndProjectAsync(repo, reader, writer, db, mojang, slug, map.Id, merged, ct);
        await Send.ResponseAsync(resp!, status, ct);
    }
}

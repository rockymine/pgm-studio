using System.Text.Json;
using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Api.Services;
using PgmStudio.Data;
using PgmStudio.Data.Repositories;
using PgmStudio.Pgm.Editing;

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
/// PUT /api/map/{slug}/intent — store the intent and regenerate the map from it. Persists the
/// <c>map_intent_json</c> artifact, then projects the intent into the PGM document
/// (<see cref="TeamsGenerator"/>) and saves it through the normal codec path. Idempotent: the generator
/// clears its own prior output, and the save path is entity-replace, so re-PUTting a corrected intent
/// rewrites the spawn structure cleanly.
/// </summary>
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
        var intent = (string.IsNullOrWhiteSpace(body) ? null : JsonSerializer.Deserialize<MapIntent>(body, IntentStore.Json)) ?? new MapIntent();

        await IntentStore.SaveAsync(db, map.Id, intent, ct);
        // Authors/contributors are usernames → resolve to uuids here (async, outside the pure generator).
        var authors = await ResolveAuthorsAsync(intent, ct);
        var (status, resp) = await WriteSupport.RunEditAsync(repo, reader, writer, slug,
            doc => { IntentGenerator.Apply(doc, intent); if (authors is not null) doc["authors"] = authors; return new Dict(); }, ct);
        await Send.ResponseAsync(resp!, status, ct);
    }

    // Resolve each author/contributor username to {uuid, name, role}; unresolved names are skipped
    // (mirrors MetadataEndpoint, which drops entries without a uuid). Null = leave authors untouched.
    private async Task<List<object?>?> ResolveAuthorsAsync(MapIntent intent, CancellationToken ct)
    {
        if (intent.Meta is not { } m) return null;
        var resolved = new List<object?>();
        async Task Add(IEnumerable<string> names, string role)
        {
            foreach (var name in names.Select(n => n.Trim()).Where(n => n.Length > 0))
            {
                try
                {
                    var (uuid, canonical) = await mojang.LookupAsync(name, ct);
                    resolved.Add(new Dict { ["uuid"] = uuid, ["name"] = canonical, ["role"] = role });
                }
                catch { /* unknown username / lookup failed — skip this entry */ }
            }
        }
        await Add(m.Authors, "author");
        await Add(m.Contributors, "contributor");
        return resolved;
    }
}

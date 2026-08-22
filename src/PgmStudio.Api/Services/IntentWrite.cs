using System.Text.Json;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Storing an intent on a map: persist the <c>map_intent_json</c> artifact, then project it into the PGM
/// document (<see cref="TeamsGenerator"/>) and save through the normal codec path. Idempotent — the
/// generator clears its own prior output and the save path is entity-replace, so re-storing a corrected
/// intent rewrites the spawn structure cleanly.
/// <para>Both routes below go through here, so an author's edit and a rebuild from the plan cannot
/// regenerate a map differently; they differ only in what reaches this point.</para>
/// </summary>
public static class IntentWrite
{
    /// <summary>Store the intent and project it into the map document. The <c>If-Match</c> guards the
    /// <b>intent</b>, which is the document the caller read and posted; the projection that follows rewrites
    /// the map from it, so guarding both would refuse a write against a map the caller never claimed to have
    /// read.</summary>
    public static async Task<EditApplied> StoreAndProjectAsync(
        MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts,
        MojangClient mojang, string slug, long mapId, string body, long? expected, CancellationToken ct)
    {
        var intent = Stated(body) ?? new MapIntent();

        var written = await DocumentWrite.StoreAsync(artifacts, mapId, ArtifactKind.MapIntentJson, "intent",
            JsonSerializer.SerializeToUtf8Bytes(intent, MapArtifactStore.Json), expected, ct);
        if (written.Refusal is { } refusal) return new(refusal);

        // A stated name is looked up here (async, outside the pure generator) so an account gets its uuid.
        var authors = await ResolveAuthorsAsync(mojang, intent, ct);
        var applied = await MapEdit.RunAsync(repo, reader, writer, slug,
            doc => { IntentGenerator.Apply(doc, intent); if (authors is not null) doc["authors"] = authors; return new Dict(); },
            expected: null, ct);

        // The caller guarded the intent, and the intent is what it now holds a revision of; the map's own
        // number is a different one and answering it would arm the caller's next write against the wrong
        // document.
        return applied with { Revision = written.Revision };
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

using System.Text.Json;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Editing;
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
        MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts, FeatureData features,
        PlayerLookup players, string slug, long mapId, string body, long? expected, CancellationToken ct)
    {
        if (Unbindable(body) is { } unreadable) return new(new Refusal(400, "unreadable document", [unreadable]));
        var intent = Stated(body) ?? new MapIntent();
        if (Unnamable(intent) is { } named) return new(named);

        if (await ProjectionRefusalAsync(reader, slug, intent, ct) is { } unprojectable) return new(unprojectable);
        if (await UnseatedMonumentsAsync(features, artifacts, mapId, intent, ct) is { } unseated) return new(unseated);

        var stored = await WithMapPeopleAsync(reader, slug, intent, ct);
        var written = await DocumentWrite.StoreAsync(artifacts, mapId, ArtifactKind.MapIntentJson, "intent",
            JsonSerializer.SerializeToUtf8Bytes(stored, MapArtifactStore.Json), expected, ct);
        if (written.Refusal is { } refusal) return new(refusal);

        // A stated name is looked up here (async, outside the pure generator) so an account gets its uuid.
        var authors = await ResolveAuthorsAsync(players, intent, ct);
        var applied = await MapEdit.RunAsync(repo, reader, writer, slug,
            doc => { IntentGenerator.Apply(doc, intent); if (authors is not null) doc["authors"] = authors; return new Dict(); },
            expected: null, ct);

        // The caller guarded the intent, and the intent is what it now holds a revision of; the map's own
        // number is a different one and answering it would arm the caller's next write against the wrong
        // document.
        return applied with { Revision = written.Revision };
    }

    /// <summary>The refusal the projection answers for <paramref name="intent"/> over the map's document as it
    /// stands, or null where it applies. Asked before the intent is stored, because a refused projection leaves
    /// the document as it was: an intent stored over a document it never reached is a map whose export says
    /// less than its intent does.</summary>
    private static async Task<Refusal?> ProjectionRefusalAsync(
        MapReader reader, string slug, MapIntent intent, CancellationToken ct)
    {
        if (await reader.ReadDocAsync(slug, ct) is not { } doc) return null;
        try { IntentGenerator.Apply(doc, intent); return null; }
        catch (EditException fault) { return new Refusal(fault.Status, fault.Error, [fault.Finding]); }
    }

    /// <summary>The refusal for a wool monument nothing can be put into (<c>OB33</c>): its block is solid in the
    /// scanned world, or no block touches it. Asked only of a map read from a world: a sketch map's monuments
    /// are where its build puts them, and a column the scan never read has nothing to answer.</summary>
    private static async Task<Refusal?> UnseatedMonumentsAsync(
        FeatureData features, MapArtifactStore artifacts, long mapId, MapIntent intent, CancellationToken ct)
    {
        if (intent.Wools is not { Count: > 0 } wools
            || await artifacts.HasAsync(mapId, ArtifactKind.SketchLayoutJson, ct)) return null;

        var findings = new List<Finding>();
        foreach (var wool in wools)
            foreach (var monument in wool.Monuments)
            {
                int x = (int)Math.Floor(monument.Location.X), y = (int)Math.Floor(monument.Location.Y);
                int z = (int)Math.Floor(monument.Location.Z);
                var seat = await BlockSeats.ReadAsync(features, mapId, x, y, z, ct);
                if (!seat.Scanned || (seat.Clear && seat.Support)) continue;
                findings.Add(new Finding(ObjectiveRules.MonumentNotSeated,
                    $"the {wool.Color} wool's monument for team '{monument.Team}' at ({x}, {y}, {z}) "
                    + (seat.Clear ? "touches no block" : "is a solid block"),
                    Field: "wools", Subjects: [wool.Color, monument.Team]));
            }
        return findings.Count == 0 ? null : new Refusal(422, "a monument no wool can be put into", findings);
    }

    /// <summary>The intent to store: as stated where it names anyone, and otherwise carrying the people the map
    /// already credits — the map's rows are its record of who made it, and the export reads the stored intent,
    /// so an intent naming nobody would leave a credited map telling its players it names nobody (<c>EX6</c>).
    /// The map's rows are left alone either way.</summary>
    private static async Task<MapIntent> WithMapPeopleAsync(
        MapReader reader, string slug, MapIntent intent, CancellationToken ct)
    {
        if (intent.Meta is { } stated && (stated.Authors.Count > 0 || stated.Contributors.Count > 0)) return intent;
        if (await reader.ReadDocAsync(slug, ct) is not { } doc
            || doc.GetValueOrDefault("authors") is not IEnumerable<object?> entries)
            return intent;

        var people = entries.Select(MapAuthors.Read).OfType<MapAuthors.Person>()
            .Where(person => person.Name.Length > 0).ToList();
        if (people.Count == 0) return intent;

        List<AuthorIntent> Named(bool contributors) =>
        [
            .. people.Where(person => person.Contributor == contributors)
                .Select(person => new AuthorIntent { Name = person.Name, Contribution = person.Contribution }),
        ];
        return intent with
        {
            Meta = (intent.Meta ?? new MetaIntent()) with { Authors = Named(false), Contributors = Named(true) },
        };
    }

    /// <summary>The refusal for a person the intent states under a name nobody could be called, or null
    /// where every one of them is storable. A name Mojang does not know is a pseudonym and passes here; what
    /// does not is a string that is not a name at all — over
    /// <see cref="AuthorNames.MaxLength"/> characters, opening or closing on a space, or carrying something
    /// outside letters, digits, spaces and <c>.,-_'</c>. It is <c>RQ1</c> because it is the posted document
    /// that cannot be acted on, and it names the field so the caller does not have to search for which
    /// person it meant, under <paramref name="member"/> where the intent is posted beside other documents.
    /// Refusing is the point: a row dropped in silence is what makes an author believe somebody was
    /// credited.</summary>
    public static Refusal? Unnamable(MapIntent intent, string member = "")
    {
        var prefix = member.Length == 0 ? "" : member + ".";
        if (intent.Meta is not { } meta) return null;
        List<Finding> refused = [];
        foreach (var (people, role) in new[] { (meta.Authors, "authors"), (meta.Contributors, "contributors") })
        {
            for (var at = 0; at < people.Count; at++)
            {
                var stated = people[at].Name.Trim();
                if (AuthorNames.Refuse(stated) is { } failed)
                    refused.Add(new Finding(RequestRules.Unreadable,
                        $"name '{stated}' in the {role} of the game settings {failed}",
                        Field: $"{prefix}meta.{role}[{at}].name"));
            }
        }
        return refused.Count == 0 ? null : new Refusal(400, "not a name", refused);
    }

    /// <summary>What a body states as an intent, or null where it states none or will not bind — the lenient
    /// read, for a caller only asking what the intent says. A body that will not bind is refused by
    /// <see cref="Unbindable"/> before anything is stored.</summary>
    public static MapIntent? Stated(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return Read(json); }
        catch (JsonException) { return null; }
    }

    /// <summary>The <c>RQ1</c> finding for an intent the binder cannot read, naming the field it gave up at
    /// under <paramref name="member"/> (<c>modes[0]</c>, or <c>intent.modes[0]</c> where the intent is posted
    /// beside other documents), or null where it binds.</summary>
    public static Finding? Unbindable(string json, string member = "") =>
        DocumentBinding.Unbindable(member, json, stated => Read(stated));

    private static MapIntent? Read(string json) => JsonSerializer.Deserialize<MapIntent>(json, MapArtifactStore.Json);

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
    private static async Task<List<object?>?> ResolveAuthorsAsync(PlayerLookup players, MapIntent intent, CancellationToken ct)
    {
        if (intent.Meta is not { } m) return null;
        var resolved = new List<object?>();
        async Task Add(IEnumerable<AuthorIntent> people, string role)
        {
            foreach (var person in people.Where(p => p.Name.Trim().Length > 0))
            {
                var stated = person.Name.Trim();
                // Null is "no account is called that" — the stated name stands on its own as a pseudonym,
                // which is a whole author in PGM's model. A name that could not be stored at all was
                // refused before the intent was written (Unnamable), so every name reaching here is one.
                var (uuid, name) = await players.ResolveAsync(stated, ct) ?? ("", stated);
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

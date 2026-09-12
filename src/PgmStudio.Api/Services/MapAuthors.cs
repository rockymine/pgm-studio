using System.Text.Json;
using System.Text.Json.Nodes;
using LinqToDB;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Services;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Who a map is credited to, and the one rule for what counts as a person. Stating authors is a <b>full
/// replace</b> — the map's rows are wiped and rewritten from what was given — which mirrors the contract,
/// where the <c>&lt;authors&gt;</c> block is rewritten whole rather than appended to.
///
/// <para>PGM takes a person as an <b>account or a pseudonym</b>: a <c>uuid</c> it resolves to a player, or the
/// element's own text. Both halves of the codec keep that rule, so an entry carrying either is an author and
/// only one carrying neither is skipped. A bare string is a pseudonym and nothing else, which is the form an
/// author writes when there is no account behind the name.</para>
///
/// <para><b>The rows and the intent are written together.</b> The rows are the map's own record and
/// <c>meta.authors</c> is what the export reads — the observer platform's board is stamped from the intent —
/// so a caller writing one without the other credits the map on the row and is told it names nobody
/// (<c>EX6</c>). <see cref="ReplaceAsync"/> runs inside the caller's transaction and
/// <see cref="ProjectAsync"/> outside it, because storing an artifact opens its own; every caller does both.</para>
/// </summary>
public static class MapAuthors
{
    /// <summary>One credited person, as both halves read them. <paramref name="Name"/> is a pseudonym or an
    /// account's display name and <paramref name="Uuid"/> the account; an entry with neither is nobody.</summary>
    public readonly record struct Person(string Name, string Uuid, bool Contributor, string? Contribution);

    /// <summary>
    /// What a stated entry credits, or null where it credits nobody.
    ///
    /// <para>An entry arrives in three shapes and all three mean the same person: a bare string, a
    /// <c>Dictionary&lt;string, object?&gt;</c>, and a <see cref="JsonElement"/> — including one nested as a
    /// dictionary's <em>value</em>, which is what <c>Deserialize&lt;Dictionary&lt;string, object?&gt;&gt;</c>
    /// puts there. Reading a field as a CLR string alone drops the whole object form, and drops it on a 200.</para>
    /// </summary>
    public static Person? Read(object? entry)
    {
        if (Text(entry) is { Length: > 0 } pseudonym) return new Person(pseudonym, "", false, null);
        var person = Fields(entry);
        if (person is null) return null;
        var name = Text(person.GetValueOrDefault("name"))?.Trim() ?? "";
        var uuid = Text(person.GetValueOrDefault("uuid"))?.Trim() ?? "";
        if (name.Length == 0 && uuid.Length == 0) return null;
        return new Person(name, uuid,
            Text(person.GetValueOrDefault("role")) == "contributor",
            NullIfEmpty(Text(person.GetValueOrDefault("contribution"))?.Trim()));
    }

    /// <summary>Replace a map's authors with what the caller stated. Runs inside whatever transaction the
    /// caller opened.</summary>
    public static async Task ReplaceAsync(PgmDb db, long mapId, IEnumerable<object?> stated, CancellationToken ct)
    {
        await db.Authors.Where(row => row.MapId == mapId).DeleteAsync(ct);
        foreach (var entry in stated)
        {
            if (Read(entry) is not { } person) continue;
            await db.InsertAsync(new AuthorRow
            {
                MapId = mapId,
                Uuid = person.Uuid,
                Role = person.Contributor ? "contributor" : "author",
                Contribution = person.Contribution,
                Name = NullIfEmpty(person.Name),
            }, token: ct);
        }
    }

    /// <summary>Write the same people into the stored intent's <c>meta.authors</c> and
    /// <c>meta.contributors</c>, split by the role each states.
    ///
    /// <para>The document is patched as JSON rather than round-tripped through <c>MapIntent</c>: a rename is
    /// not the moment to rewrite an intent through a model, and a key the model does not carry would be lost
    /// by one that did. A map holding no intent has nothing to project into and is left alone.</para>
    ///
    /// <para>No uuid goes across. An intent names a person and the export resolves the name, which is why
    /// <c>AuthorIntent</c> carries no uuid field to put one in.</para></summary>
    public static async Task ProjectAsync(MapArtifactStore artifacts, long mapId, IEnumerable<object?> stated,
                                          CancellationToken ct)
    {
        if (await artifacts.LoadAsync(mapId, ArtifactKind.MapIntentJson, ct) is not { } bytes) return;
        JsonNode? root;
        try { root = JsonNode.Parse(bytes); } catch (JsonException) { return; }
        if (root is not JsonObject intent) return;

        var people = stated.Select(Read).OfType<Person>().ToList();
        if (intent["meta"] is not JsonObject meta) intent["meta"] = meta = [];
        meta["authors"] = Named(people, contributors: false);
        meta["contributors"] = Named(people, contributors: true);
        await artifacts.SaveAsync(mapId, ArtifactKind.MapIntentJson,
                                  JsonSerializer.SerializeToUtf8Bytes(intent, MapArtifactStore.Json), ct);
    }

    /// <summary>One role's people, as the intent spells them. A person with no name states nobody — the rows
    /// keep a uuid-only credit and an intent cannot, having no field for it.</summary>
    private static JsonArray Named(List<Person> people, bool contributors)
    {
        var named = new JsonArray();
        foreach (var person in people)
        {
            if (person.Name.Length == 0 || person.Contributor != contributors) continue;
            var one = new JsonObject { ["name"] = person.Name };
            if (person.Contribution is { Length: > 0 } what) one["contribution"] = what;
            named.Add(one);
        }
        return named;
    }

    /// <summary>A value's text, whether it arrived as a CLR string or as JSON.</summary>
    private static string? Text(object? value) => value switch
    {
        string text => text,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
        JsonValue node => node.TryGetValue<string>(out var text) ? text : null,
        _ => null,
    };

    /// <summary>An entry's fields, whether it arrived as a dictionary or as a JSON object.</summary>
    private static Dict? Fields(object? entry) => entry switch
    {
        Dict dict => dict,
        JsonElement { ValueKind: JsonValueKind.Object } element =>
            JsonSerializer.Deserialize<Dict>(element.GetRawText()),
        JsonObject node => JsonSerializer.Deserialize<Dict>(node.ToJsonString()),
        _ => null,
    };

    private static string? NullIfEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;
}

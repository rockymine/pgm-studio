using System.Text.Json;
using System.Text.Json.Nodes;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Services;

using Dict = Dictionary<string, object?>;

/// <summary>
/// A map's identity: what it is called, what version it states, what it is played for, how high it may be
/// built, and who wrote it.
///
/// <para><b>Only what the request named.</b> Every field is optional and an absent one is left alone, which
/// is what lets a rename be one call rather than a read-modify-write of the whole row — so the payload is
/// asked whether it <em>holds</em> each key rather than what its value is.</para>
///
/// <para><b>The authors are a replace and the rest are a patch</b>, and the two happen in one transaction so
/// a map is never briefly credited to nobody. Who counts as a person is <see cref="MapAuthors"/>'s rule, the
/// same one the load from documents states them by.</para>
///
/// <para>The <c>gamemode</c> column is not writable here: it holds the author's original
/// <c>&lt;gamemode&gt;</c> label, round-tripped as written, while the gamemode itself is derived from the
/// map's objective modules and so cannot be set by hand.</para>
///
/// <para><b>An author named here is named in the map's intent too</b>, where the map holds one. The rows are
/// what the map document is written from and <c>meta.authors</c> is what the export reads — the observer
/// platform's board is stamped from the intent — so a map whose only editor wrote one of the two would be
/// credited on the row and told it names nobody (<c>EX6</c>), with nothing in the interface able to settle
/// it. The traffic already ran the other way, an intent write resolving its names into the rows; this is the
/// return leg.</para>
/// </summary>
public static class MapMetadata
{
    public static async Task ApplyAsync(PgmDb db, MapArtifactStore artifacts, long mapId, Dict stated,
                                        CancellationToken ct)
    {
        await Write(db, mapId, stated, ct);
        // Outside the transaction above, because storing an artifact opens its own — and because the rows are
        // the map's own record of who wrote it while the intent's copy is a projection of them.
        if (stated.TryGetValue("authors", out var raw) && raw is List<object?> people)
            await ProjectAsync(artifacts, mapId, people, ct);
    }

    private static Task Write(PgmDb db, long mapId, Dict stated, CancellationToken ct) =>
        db.InOneWriteAsync(async () =>
        {
            var update = db.Maps.Where(map => map.Id == mapId).AsUpdatable();
            if (stated.ContainsKey("name"))
                update = update.Set(map => map.Name, stated["name"] as string ?? "");
            if (stated.ContainsKey("version"))
                update = update.Set(map => map.Version, NullIfEmpty(stated["version"] as string));
            if (stated.ContainsKey("objective"))
                update = update.Set(map => map.Objective, NullIfEmpty(stated["objective"] as string));
            if (stated.ContainsKey("max_build_height"))
                update = update.Set(map => map.MaxBuildHeight,
                    stated["max_build_height"] is { } height ? Convert.ToDouble(height) : null);
            update = update.Set(map => map.UpdatedAt, DateTime.UtcNow);
            await update.UpdateAsync(ct);

            if (stated.TryGetValue("authors", out var raw) && raw is List<object?> authors)
                await MapAuthors.ReplaceAsync(db, mapId, authors, ct);
        }, ct);

    /// <summary>Write the same people into the stored intent's <c>meta.authors</c> and
    /// <c>meta.contributors</c>, split by the role each states.
    ///
    /// <para>The document is patched as JSON rather than round-tripped through <c>MapIntent</c>: a rename is
    /// not the moment to rewrite an intent through a model, and a key the model does not carry would be lost
    /// by one that did. A map holding no intent has nothing to project into and is left alone.</para>
    ///
    /// <para>No uuid goes across. An intent names a person and the export resolves the name, which is why
    /// <c>AuthorIntent</c> carries no uuid field to put one in.</para></summary>
    private static async Task ProjectAsync(MapArtifactStore artifacts, long mapId, List<object?> people,
                                           CancellationToken ct)
    {
        if (await artifacts.LoadAsync(mapId, ArtifactKind.MapIntentJson, ct) is not { } bytes) return;
        JsonNode? root;
        try { root = JsonNode.Parse(bytes); } catch (JsonException) { return; }
        if (root is not JsonObject intent) return;

        if (intent["meta"] is not JsonObject meta) intent["meta"] = meta = [];
        meta["authors"] = Named(people, contributors: false);
        meta["contributors"] = Named(people, contributors: true);
        await artifacts.SaveAsync(mapId, ArtifactKind.MapIntentJson,
                                  JsonSerializer.SerializeToUtf8Bytes(intent, MapArtifactStore.Json), ct);
    }

    /// <summary>One role's people, as the intent spells them. A row with no name states nobody — the rows
    /// keep a uuid-only credit and an intent cannot, having no field for it.</summary>
    private static JsonArray Named(List<object?> people, bool contributors)
    {
        var named = new JsonArray();
        foreach (var entry in people)
        {
            var person = entry as Dict;
            var name = (person is null ? entry as string : person.GetValueOrDefault("name") as string)?.Trim() ?? "";
            if (name.Length == 0) continue;
            var role = person?.GetValueOrDefault("role") as string == "contributor";
            if (role != contributors) continue;
            var one = new JsonObject { ["name"] = name };
            if ((person?.GetValueOrDefault("contribution") as string)?.Trim() is { Length: > 0 } what)
                one["contribution"] = what;
            named.Add(one);
        }
        return named;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

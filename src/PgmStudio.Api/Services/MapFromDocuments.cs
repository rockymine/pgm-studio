using System.Text.Json;
using Microsoft.AspNetCore.Http;
using PgmStudio.Api.Endpoints;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Contracts;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>What loading a map from its documents came to: a refusal, or the map that now exists.</summary>
public sealed record MapLoad(
    int? ErrorStatus, string? Error, IReadOnlyList<Finding>? Findings,
    string? Slug = null, bool Replaced = false, int Cells = 0, int Islands = 0, Findings? Complaints = null)
{
    public bool IsError => ErrorStatus is not null;
}

/// <summary>
/// A map, whole, from the three documents it is made of — the plan it was drawn from, the layout that was
/// actually built, and the intent it is played for. The map that comes out is a studio map rather than a
/// world: it can be re-planned, re-read and pre-flighted, which is what an imported world can never be.
///
/// <para><b>The order is the operation.</b> Storing an intent projects the map document from the intent's own
/// <c>meta</c>, and a compiled intent carries an empty <c>meta.authors</c> — so authors stated before the
/// intent are overwritten by the projection and have to be applied after it. That rule was written down in
/// four places and enforced in none; here it is the sequence itself, and no caller can get it wrong.</para>
///
/// <para>A map already stored under the slug is <b>replaced</b>: the documents name one map, and loading them
/// twice is a reload rather than a second map. A load that fails partway takes back what it wrote, the way
/// the world imports do, so a refusal never leaves half a map behind.</para>
/// </summary>
public static class MapFromDocuments
{
    public static async Task<MapLoad> LoadAsync(
        HttpContext http, MapFromDocumentsRequest request,
        MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts,
        WorldFeatureWriter features, PgmDb db, MojangClient mojang, CancellationToken ct)
    {
        var name = request.Name;
        if (string.IsNullOrWhiteSpace(name)) name = StatedName(request.Intent);
        if (string.IsNullOrWhiteSpace(name))
            return Refuse(400, "no name given", new Finding(RequestRules.Unreadable,
                "neither a name nor the intent's own meta.name says what this map is called", Field: "name"));

        var slug = SketchSlug.Slugify(string.IsNullOrWhiteSpace(request.Slug) ? name : request.Slug!);
        var existing = await repo.GetBySlugAsync(slug, ct);
        if (existing is not null) await repo.DeleteMapAsync(existing.Id, ct);   // FK cascade takes the artifacts

        var now = DateTime.UtcNow;
        var mapId = await repo.InsertAsync(new MapRow
        {
            Slug = slug, Name = name, Gamemode = "ctw", Stage = MapStage.Plan, CreatedAt = now, UpdatedAt = now,
        });

        try
        {
            await artifacts.SaveAsync(mapId, ArtifactKind.PlanJson, Bytes(request.Plan), ct);
            await artifacts.SaveAsync(mapId, ArtifactKind.SketchLayoutJson, Bytes(request.Layout), ct);

            // The drawing is declared done here rather than left for a second call: a map loaded without its
            // geometry is a map Configure cannot open, which is the whole of what this operation is for.
            var finished = await SketchFinish.RunAsync(mapId, repo, artifacts, features, ct);
            if (finished.IsError)
            {
                await repo.DeleteMapAsync(mapId, ct);
                return new(finished.ErrorStatus, finished.Error, finished.Findings);
            }

            // Then the intent, which stores it and projects the document from it — and only then the authors,
            // because that projection is what would overwrite them.
            var (status, body) = await IntentWrite.StoreAndProjectAsync(
                http, repo, reader, writer, artifacts, mojang, slug, mapId, request.Intent.GetRawText(), ct);
            if (status != 200)
            {
                await repo.DeleteMapAsync(mapId, ct);
                return new(status, "intent not stored", Findings(body));
            }

            if (request.Authors is { Count: > 0 } authors)
                await MapAuthors.ReplaceAsync(db, mapId, authors.Select(Person), ct);

            return new(null, null, null, slug, existing is not null,
                       finished.Cells, finished.Islands, finished.Complaints);
        }
        catch
        {
            await repo.DeleteMapAsync(mapId, ct);
            throw;
        }
    }

    /// <summary>An author as the metadata write states one: a bare pseudonym, or the four fields of a
    /// person.</summary>
    private static object? Person(JsonElement entry) => entry.ValueKind switch
    {
        JsonValueKind.String => entry.GetString(),
        JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object?>>(entry.GetRawText()),
        _ => null,
    };

    private static string? StatedName(JsonElement intent) =>
        intent.ValueKind == JsonValueKind.Object
        && intent.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Object
        && meta.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
            ? name.GetString() : null;

    private static byte[] Bytes(JsonElement document) =>
        System.Text.Encoding.UTF8.GetBytes(document.GetRawText());

    /// <summary>The findings out of whatever the intent write handed back, which is the refusal envelope's
    /// own shape where it refused.</summary>
    private static IReadOnlyList<Finding> Findings(object? body) =>
        body is Dictionary<string, object?> dict && dict.GetValueOrDefault("findings") is List<Finding> list
            ? list : [];

    private static MapLoad Refuse(int status, string error, params Finding[] findings) =>
        new(status, error, findings);
}

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using PgmStudio.Api.Endpoints;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Contracts;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>What loading a map from its documents came to: a refusal, or the map that now exists.</summary>
public sealed record MapLoad(
    Refusal? Refusal, string? Slug = null, bool Replaced = false, int Cells = 0, int Islands = 0,
    Findings? Complaints = null);

/// <summary>
/// A map, whole, from the three documents it is made of — the plan it was drawn from, the layout that was
/// actually built, and the intent it is played for. The map that comes out is a studio map rather than a
/// world: it can be re-planned, re-read and pre-flighted, which is what an imported world can never be.
///
/// <para><b>The authors ride in the body.</b> A compiled intent names nobody, so who a map is credited to is
/// stated beside the three documents and written as part of the load rather than in a second call. The
/// projection leaves the map's people alone where the intent names none, so the two may be applied in either
/// order; stating them here is what makes one request enough.</para>
///
/// <para>A map already stored under the slug is <b>replaced</b>: the documents name one map, and loading them
/// twice is a reload rather than a second map. A load that fails partway takes back what it wrote, the way
/// the world imports do, so a refusal never leaves half a map behind.</para>
///
/// <para><b>All three documents answer <c>RQ3</c>.</b> Each is read into a type, so each can carry a name
/// that type has nowhere to keep, and the paths are prefixed with the member the document was posted under —
/// a bare <c>meta.athors</c> cannot say which of the three said it.</para>
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

        Complaints.Unread(http, [
            .. Unread("plan", request.Plan, PlanModel.Stated),
            .. Unread("layout", request.Layout, SketchLayout.Stated),
            .. Unread("intent", request.Intent, IntentWrite.Stated)]);

        // The gate every other road to a stored layout runs: a house is stamped from the style that arrives
        // here, and this is the last point before it is built. Its complaints ride on the success the way
        // they do on the other two roads, so what is worth saying is said either way.
        var layoutJson = request.Layout.GetRawText();
        var styles = SketchMaterialGate.Check(layoutJson);
        if (styles.Refuses) return new(new Refusal(400, "invalid style or theme", [.. styles.Refusals]));
        Complaints.Add(http, styles.Complaints);

        var slug = Slugs.Of(string.IsNullOrWhiteSpace(request.Slug) ? name : request.Slug!);
        var existing = await repo.GetBySlugAsync(slug, ct);
        var mapId = await MapOrigin.ReplacingAsync(repo, slug, name, MapStage.Plan, ct);

        try
        {
            await artifacts.SaveAsync(mapId, ArtifactKind.PlanJson,
                                      Bytes(request.Plan ?? Empty), ct);
            await artifacts.SaveAsync(mapId, ArtifactKind.SketchLayoutJson, Bytes(request.Layout), ct);

            // The drawing is declared done here rather than left for a second call: a map loaded without its
            // geometry is a map Configure cannot open, which is the whole of what this operation is for.
            var finished = await SketchFinish.RunAsync(mapId, repo, artifacts, features, ct);
            if (finished.Refusal is not null)
            {
                await repo.DeleteMapAsync(mapId, ct);
                return new(finished.Refusal);
            }

            // Then the intent, which stores it and projects the document from it — and only then the authors,
            // because that projection is what would overwrite them.
            var applied = await IntentWrite.StoreAndProjectAsync(
                repo, reader, writer, artifacts, mojang, slug, mapId, request.Intent.GetRawText(),
                expected: null, ct);
            if (applied.Refusal is { } refused)
            {
                await repo.DeleteMapAsync(mapId, ct);
                return new(refused);
            }

            if (request.Authors is { Count: > 0 } authors)
                await MapAuthors.ReplaceAsync(db, mapId, authors.Select(Person), ct);

            return new(null, slug, existing is not null,
                       finished.Cells, finished.Islands, finished.Complaints);
        }
        catch
        {
            await repo.DeleteMapAsync(mapId, ct);
            throw;
        }
    }

    /// <summary>The fields <paramref name="read"/> had nowhere to keep, under the member the document was
    /// posted as. A document that is absent or is not an object has nothing to compare.</summary>
    private static IEnumerable<string> Unread(string member, JsonElement? document, Func<string, object?> read)
    {
        if (document is not { ValueKind: JsonValueKind.Object } stated) return [];
        var json = stated.GetRawText();
        JsonNode? node;
        try { node = JsonNode.Parse(json); }
        catch (JsonException) { return []; }
        return DocumentShape.Unread(node, read(json)).Select(field => $"{member}.{field}");
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

    /// <summary>What a map with no plan stores under one — the same empty document an origination writes, so
    /// a grid board and a freshly originated plan read alike.</summary>
    private static readonly JsonElement Empty = JsonDocument.Parse("{}").RootElement;

    /// <summary>The findings out of whatever the intent write handed back, which is the refusal envelope's
    /// own shape where it refused.</summary>
    private static IReadOnlyList<Finding> Findings(object? body) =>
        body is Dictionary<string, object?> dict && dict.GetValueOrDefault("findings") is List<Finding> list
            ? list : [];

    private static MapLoad Refuse(int status, string error, params Finding[] findings) =>
        new(Refusal.At(status, error, findings));
}

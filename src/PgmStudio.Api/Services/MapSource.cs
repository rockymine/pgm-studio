using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using PgmStudio.Api.Access;
using PgmStudio.Api.Endpoints;
using PgmStudio.Contracts;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>What applying a map's source came to: a refusal, or the answer. What was worth saying about it rides
/// on the answer as its warnings.</summary>
public sealed record MapSourceApplied(Refusal? Refusal, MapSourceDto? Answer = null);

/// <summary>
/// A map stored from its source: the base it is built from — a plan compiled here, or a layout and an intent drawn
/// without one, the plan beside them kept as the one they were drawn from — and the refinement stating everything
/// the base cannot, applied onto it before anything is judged.
///
/// <para>Everything the source can be refused for is decided before the stored map is touched: which base it
/// states, whether each document binds onto its record, whether the plan compiles, whether the refinement says
/// what it means, a style or theme the gate refuses, a person nobody could be called, and a drawing the finish
/// would refuse. Only then is the map replaced — its plan, its layout and its intent stored as one change, the
/// drawing rasterized, the intent projected and the credits written — so a refused source leaves the board it
/// would have replaced. A dry run decides all of it, stores nothing, and answers the layout and intent it would
/// have stored.</para>
///
/// <para>The map's row is replaced and what hangs off the slug is not: its notes, its history, and the pictures
/// kept of it. Every document answers <c>RQ3</c> for a field its reader has nowhere to keep, the path prefixed
/// with the member it was stated under.</para>
/// </summary>
public static class MapSource
{
    /// <summary>The members a source states, as its body spells them.</summary>
    private static readonly string[] Members = ["plan", "layout", "intent", "refinement", "name", "origin", "note"];

    public static async Task<MapSourceApplied> ApplyAsync(
        HttpContext http, string slug, string body, bool dry,
        MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts,
        WorldFeatureWriter features, PgmDb db, PlayerLookup players, MapChangeLog log, CancellationToken ct)
    {
        if (Slugs.OfFolder(slug) != slug)
            return Refuse(400, "not a slug", new Finding(RequestRules.Unreadable,
                $"'{slug}' is not a slug: a map is stored under lowercase letters, digits, '-' and '_' — '{Slugs.Of(slug)}' "
                + "is the one the name would take", Field: "slug"));

        JsonObject stated;
        MapSourceRequest request;
        try
        {
            stated = JsonNode.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body) as JsonObject
                     ?? throw new JsonException("the body is not a JSON object");
            request = stated.Deserialize<MapSourceRequest>(MapArtifactStore.Json) ?? new MapSourceRequest();
        }
        catch (JsonException fault)
        {
            return Refuse(400, "unreadable source", new Finding(RequestRules.Unreadable, fault.Message));
        }
        Complaints.Unread(http, [.. stated.Select(member => member.Key).Where(key => !Members.Contains(key))]);

        var plan = Raw(request.Plan);
        var drawnLayout = Raw(request.Layout);
        var drawnIntent = Raw(request.Intent);
        var refinement = Raw(request.Refinement);
        if ((drawnLayout is null) != (drawnIntent is null))
            return Refuse(400, "no base", new Finding(RequestRules.Unreadable,
                $"the source states a drawn {(drawnLayout is null ? "intent" : "layout")} without its "
                + $"{(drawnLayout is null ? "layout" : "intent")}: a drawing is the base together with what it is "
                + "played for", Field: drawnLayout is null ? "layout" : "intent"));
        if (plan is null && drawnLayout is null)
            return Refuse(400, "no base", new Finding(RequestRules.Unreadable,
                "the source states no base: a plan, which the studio compiles, or a drawn layout and its intent",
                Field: "plan"));
        if (request.Note is { Length: > MapChangeLog.NoteLength })
            return Refuse(400, "note too long", new Finding(RequestRules.Unreadable,
                $"the note is {request.Note.Length} characters and a change keeps at most {MapChangeLog.NoteLength}",
                Field: "note"));

        // Each document binds onto its record before anything is made of it: a lenient read of one that does not
        // is a default instance, and a map stored from it is a map with none of what the document stated.
        Finding?[] unbindable =
        [
            plan is null ? null : DocumentBinding.Unbindable("plan", plan, json => PlanModel.Parse(json)),
            drawnLayout is null ? null : DocumentBinding.Unbindable("layout", drawnLayout, json => SketchLayout.Parse(json)),
            drawnIntent is null ? null : IntentWrite.Unbindable(drawnIntent, "intent"),
            refinement is null ? null : DocumentBinding.Unbindable("refinement", refinement,
                json => JsonSerializer.Deserialize<Refinement>(json, Refinement.Json)),
        ];
        if (unbindable.OfType<Finding>().ToList() is { Count: > 0 } unread)
            return new(new Refusal(400, "unreadable document", unread));
        Complaints.Unread(http, [
            .. Unread("plan", plan, PlanModel.Stated),
            .. Unread("layout", drawnLayout, SketchLayout.Stated),
            .. Unread("intent", drawnIntent, IntentWrite.Stated),
            .. Unread("refinement", refinement, Refinement.Stated)]);

        // The base: the drawn pair as it was stated, with the plan beside it kept as the one it was drawn from;
        // or the plan compiled.
        string layoutJson, intentJson;
        if (drawnLayout is null)
        {
            var compiled = PlanCompile.Run(PlanModel.Stated(plan!)!);
            if (compiled.Refusal is { } uncompilable) return new(uncompilable);
            Complaints.Add(http, compiled.Complaints ?? Findings.None);
            layoutJson = JsonSerializer.Serialize(compiled.Layout, SketchLayout.Json);
            intentJson = JsonSerializer.Serialize(compiled.Intent, MapArtifactStore.Json);
        }
        else
        {
            layoutJson = drawnLayout!;
            intentJson = drawnIntent!;
        }
        if (refinement is not null)
        {
            var refined = Refinement.Apply(refinement, layoutJson, intentJson);
            if (refined.Findings.Refuses) return new(new Refusal(422, "refinement not applicable", [.. refined.Findings.Refusals]));
            Complaints.Add(http, refined.Findings.Complaints);
            (layoutJson, intentJson) = (refined.LayoutJson, refined.IntentJson);
        }

        var name = request.Name is { Length: > 0 } called ? called : NameOf(intentJson);
        if (string.IsNullOrWhiteSpace(name))
            return Refuse(400, "no name given", new Finding(RequestRules.Unreadable,
                "neither a name nor the intent's own meta.name says what this map is called", Field: "name"));

        // The gate every other road to a stored layout runs: a house is stamped from the style that arrives here.
        var styles = SketchMaterialGate.Check(layoutJson);
        if (styles.Refuses) return new(new Refusal(400, "invalid style or theme", [.. styles.Refusals]));
        Complaints.Add(http, styles.Complaints);
        if (IntentWrite.Stated(intentJson) is { } intent && IntentWrite.Unnamable(intent, "intent") is { } unnamable)
            return new(unnamable);
        var planBytes = Encoding.UTF8.GetBytes(plan ?? "{}");
        var prepared = SketchFinish.Prepare(layoutJson, planBytes);
        if (prepared.Refusal is { } unbuildable) return new(unbuildable);
        Complaints.Add(http, prepared.Judged.Complaints);

        var existing = await repo.GetBySlugAsync(slug, ct);
        var storedIntent = JsonSerializer.Serialize(IntentWrite.Stated(intentJson), MapArtifactStore.Json);
        var edits = await EditsAsync(log, slug, plan, layoutJson, storedIntent, ct);
        var configure = $"/maps/{slug}/configure";
        if (dry)
            return new(null, new MapSourceDto(slug, null, existing is not null, edits, prepared.Cells.Count,
                prepared.Islands.Count, configure, Element(layoutJson), Element(storedIntent)));

        // The views an author kept are pictures of the board rather than part of it, so a rebuild keeps them.
        var keptViews = existing is null ? null : await artifacts.LoadAsync(existing.Id, ArtifactKind.MapViewsJson, ct);
        var mapId = await MapOrigin.ReplacingAsync(repo, slug, name, MapStage.Plan, Callers.OriginatorOf(http), ct);
        artifacts.Stamp = artifacts.Stamp with
        {
            OriginJson = request.Origin is { } origin ? JsonSerializer.Serialize(origin, MapArtifactStore.Json) : null,
            Note = request.Note,
        };

        try
        {
            if (keptViews is not null) await artifacts.SaveAsync(mapId, ArtifactKind.MapViewsJson, keptViews, ct);
            await artifacts.SaveAsync(mapId, ArtifactKind.PlanJson, planBytes, ct);
            var layoutRevision = await artifacts.SaveAsync(mapId, ArtifactKind.SketchLayoutJson,
                Encoding.UTF8.GetBytes(layoutJson), ct);
            var finished = await SketchFinish.WriteAsync(mapId, prepared, layoutRevision, repo, features, ct);
            if (finished.Refusal is { } refused)
            {
                await repo.DeleteMapAsync(mapId, ct);
                return new(refused);
            }

            // Then the intent, which stores it and projects the document from it — and only then the credits,
            // because that projection is what would overwrite them.
            var applied = await IntentWrite.StoreAndProjectAsync(
                repo, reader, writer, artifacts, players, slug, mapId, intentJson, expected: null, ct);
            if (applied.Refusal is { } unprojected)
            {
                await repo.DeleteMapAsync(mapId, ct);
                return new(unprojected);
            }
            if (Credits(intentJson) is { Count: > 0 } people)
            {
                await MapAuthors.ReplaceAsync(db, mapId, people, ct);
                await MapAuthors.ProjectAsync(artifacts, mapId, people, ct);
            }

            var change = await log.LatestAsync(slug, ct);
            return new(null, new MapSourceDto(slug, change, existing is not null, edits, finished.Cells,
                finished.Islands, configure));
        }
        catch
        {
            await repo.DeleteMapAsync(mapId, ct);
            throw;
        }
    }

    /// <summary>What the source changes in the documents the slug holds at its latest change — every document
    /// stated for the first time where it holds none. The intent is compared in the form it is stored in.</summary>
    private static async Task<IReadOnlyList<DocumentEdit>> EditsAsync(
        MapChangeLog log, string slug, string? plan, string layoutJson, string storedIntent, CancellationToken ct)
    {
        var latest = await log.LatestAsync(slug, ct);
        var held = latest > 0 ? await log.DocumentsAtAsync(slug, latest, ct) : new Dictionary<string, byte[]>();
        var posted = new Dictionary<string, string>
        {
            [MapDocuments.Plan] = plan ?? "{}", [MapDocuments.Layout] = layoutJson, [MapDocuments.Intent] = storedIntent,
        };
        return
        [
            .. MapDocuments.All.SelectMany(document => DocumentDiff.Between(document,
                held.TryGetValue(MapChangeRead.KindOf(document), out var bytes) ? Encoding.UTF8.GetString(bytes) : null,
                posted[document])),
        ];
    }

    /// <summary>Who the map credits, as the intent names them: its authors, and its contributors under their
    /// role, so projecting the credits back writes both lists as they were stated.</summary>
    private static List<object?> Credits(string intentJson)
    {
        var meta = (JsonNode.Parse(intentJson) as JsonObject)?["meta"] as JsonObject;
        var people = new List<object?>();
        foreach (var person in (meta?["authors"] as JsonArray ?? []).OfType<JsonNode>())
            people.Add(JsonSerializer.SerializeToElement(person));
        foreach (var person in (meta?["contributors"] as JsonArray ?? []).OfType<JsonNode>())
        {
            var contributor = person is JsonObject record ? record.DeepClone().AsObject() : new JsonObject { ["name"] = person.DeepClone() };
            contributor["role"] = "contributor";
            people.Add(JsonSerializer.SerializeToElement(contributor));
        }
        return people;
    }

    /// <summary>The fields <paramref name="read"/> had nowhere to keep, under the member the document was
    /// stated as — a bare <c>meta.athors</c> cannot say which of four documents said it.</summary>
    private static IEnumerable<string> Unread(string member, string? json, Func<string, object?> read)
    {
        if (json is null) return [];
        JsonNode? node;
        try { node = JsonNode.Parse(json); }
        catch (JsonException) { return []; }
        if (node is not JsonObject) return [];
        return DocumentShape.Unread(node, read(json)).Select(field => $"{member}.{field}");
    }

    private static JsonElement Element(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string? Raw(JsonElement? document) =>
        document is { ValueKind: not (JsonValueKind.Undefined or JsonValueKind.Null) } stated ? stated.GetRawText() : null;

    private static string? NameOf(string intentJson) =>
        (JsonNode.Parse(intentJson) as JsonObject)?["meta"]?["name"] is JsonValue name && name.TryGetValue<string>(out var text)
            ? text : null;

    private static MapSourceApplied Refuse(int status, string error, params Finding[] findings) =>
        new(Refusal.At(status, error, findings));
}

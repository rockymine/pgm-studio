using System.Globalization;
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
    /// <summary>The documents a source states, as its body spells them, each read by its own reader.</summary>
    private static readonly string[] Documents = ["plan", "layout", "intent", "refinement"];

    /// <summary>The members that say something about the source itself, which bind to its record.</summary>
    private static readonly string[] About = ["after", "name", "origin", "note"];

    public static async Task<MapSourceApplied> ApplyAsync(
        HttpContext http, string slug, string body, bool dry, string? discarding,
        MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts,
        WorldFeatureWriter features, PgmDb db, PlayerLookup players, MapChangeLog log, LibraryNames names,
        CancellationToken ct)
    {
        if (Slugs.OfFolder(slug) != slug)
            return Refuse(400, "not a slug", new Finding(RequestRules.Unreadable,
                $"slug '{slug}' has characters other than lowercase letters, digits, '-' and '_'", Field: "slug"));

        JsonObject stated;
        MapSourceRequest request;
        try
        {
            stated = JsonNode.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body) as JsonObject
                     ?? throw new JsonException("the body is not a JSON object");
            var about = new JsonObject();
            foreach (var key in About)
                if (stated[key] is { } value) about[key] = value.DeepClone();
            request = about.Deserialize<MapSourceRequest>(MapArtifactStore.Json) ?? new MapSourceRequest();
        }
        catch (JsonException fault)
        {
            return Refuse(400, "unreadable source", new Finding(RequestRules.Unreadable, fault.Message));
        }
        Complaints.Unread(http, [.. stated.Select(member => member.Key)
            .Where(key => !Documents.Contains(key) && !About.Contains(key))]);

        var discard = new List<long>();
        foreach (var word in (discarding ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (long.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out var number)) discard.Add(number);
            else
                return Refuse(400, "unreadable discard", new Finding(RequestRules.Unreadable,
                    $"the `discard` of the request holds '{word}', not a change number", Field: "discard"));

        var plan = Raw(stated["plan"]);
        var drawnLayout = Raw(stated["layout"]);
        var drawnIntent = Raw(stated["intent"]);
        var refinement = Raw(stated["refinement"]);
        if ((drawnLayout is null) != (drawnIntent is null))
            return Refuse(400, "no base", new Finding(RequestRules.Unreadable,
                $"the source states a drawn {(drawnLayout is null ? "game settings" : "layout")} and no "
                + $"{(drawnLayout is null ? "layout" : "game settings")}",
                Field: drawnLayout is null ? "layout" : "intent"));
        if (plan is null && drawnLayout is null)
            return Refuse(400, "no base", new Finding(RequestRules.Unreadable,
                "the source states neither a plan nor a drawn layout with game settings",
                Field: "plan"));
        if (request.Note is { Length: > MapChangeLog.NoteLength })
            return Refuse(400, "note too long", new Finding(RequestRules.Unreadable,
                $"the note is {request.Note.Length} characters, more than {MapChangeLog.NoteLength}",
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

        // A map made from a refinement is not replaced over a change its source has not seen.
        var existing = await repo.GetBySlugAsync(slug, ct);
        var (unseen, discarded) = await UnseenAsync(existing, slug, request.After, discard, artifacts, log, db, ct);
        if (unseen is not null) return new(unseen);

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
        // A library name is resolved into the copy the board holds, and kept recorded with the row it came from.
        var kept = refinement;
        if (refinement is not null)
        {
            var named = await names.ResolveAsync(refinement, ct);
            if (named.Findings.Refuses)
                return new(new Refusal(422, "refinement not applicable", [.. named.Findings.Refusals]));
            kept = named.Kept;
            var refined = Refinement.Apply(named.Applied, layoutJson, intentJson);
            if (refined.Findings.Refuses) return new(new Refusal(422, "refinement not applicable", [.. refined.Findings.Refusals]));
            Complaints.Add(http, refined.Findings.Complaints);
            (layoutJson, intentJson) = (LibraryNames.Recorded(refined.LayoutJson, named), refined.IntentJson);
        }

        var name = request.Name is { Length: > 0 } called ? called : NameOf(intentJson);
        if (string.IsNullOrWhiteSpace(name))
            return Refuse(400, "no name given", new Finding(RequestRules.Unreadable,
                "the request has no `name`, and the game settings have no `meta.name`", Field: "name"));

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

        var storedIntent = JsonSerializer.Serialize(IntentWrite.Stated(intentJson), MapArtifactStore.Json);
        var edits = await EditsAsync(log, slug, plan, kept, layoutJson, storedIntent, ct);
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
            Discarded = discarded,
        };

        try
        {
            if (keptViews is not null) await artifacts.SaveAsync(mapId, ArtifactKind.MapViewsJson, keptViews, ct);
            await artifacts.SaveAsync(mapId, ArtifactKind.PlanJson, planBytes, ct);
            await artifacts.SaveAsync(mapId, ArtifactKind.RefinementJson, Encoding.UTF8.GetBytes(kept ?? "{}"), ct);
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
        MapChangeLog log, string slug, string? plan, string? refinement, string layoutJson, string storedIntent,
        CancellationToken ct)
    {
        var latest = await log.LatestAsync(slug, ct);
        var held = latest > 0 ? await log.DocumentsAtAsync(slug, latest, ct) : new Dictionary<string, byte[]>();
        var posted = new Dictionary<string, string>
        {
            [MapDocuments.Plan] = plan ?? "{}", [MapDocuments.Refinement] = refinement ?? "{}",
            [MapDocuments.Layout] = layoutJson, [MapDocuments.Intent] = storedIntent,
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

    /// <summary>
    /// What a source applied over changes it has not seen comes to: the refusal handing them over, or the changes
    /// it drops. Only a map made from a refinement — one whose stored refinement states anything — is asked. The
    /// changes it has not seen are those after <paramref name="after"/>, or after the change its source was last
    /// applied as, less the ones <paramref name="discard"/> names, and each is handed over as the edits that would
    /// make the source state it (<see cref="Handover"/>); a change that edited nothing hands over nothing and
    /// refuses nothing.
    /// </summary>
    private static async Task<(Refusal? Refusal, IReadOnlyList<long> Discarded)> UnseenAsync(
        MapRow? existing, string slug, long? after, IReadOnlyList<long> discard, MapArtifactStore artifacts,
        MapChangeLog log, PgmDb db, CancellationToken ct)
    {
        var changes = await log.ListAsync(slug, ct);
        var latest = changes.Count > 0 ? changes[^1].Number : 0;
        if (after is { } built && (built < 0 || built > latest))
            return (Refusal.At(400, "no such change", new Finding(RequestRules.Unreadable,
                $"the `after` of the request for map '{slug}' is {built}, not between 0 and {latest}",
                Field: "after")), []);

        var held = existing is null ? null : await artifacts.LoadAsync(existing.Id, ArtifactKind.RefinementJson, ct);
        var applied = changes.LastOrDefault(change => change.Kinds.Contains(ArtifactKind.RefinementJson))?.Number ?? 0;
        var since = States(held) ? changes.Where(change => change.Number > (after ?? applied)).ToList() : [];
        var strays = discard.Where(number => since.All(change => change.Number != number)).ToList();
        if (strays.Count > 0)
            return (Refusal.At(400, "no such change", new Finding(RequestRules.Unreadable,
                $"the `discard` of the request names {Numbers(strays)}, and the changes the source has not seen are "
                + (since.Count == 0 ? "none" : Numbers(since.Select(change => change.Number))),
                Field: "discard")), []);

        var findings = new List<Finding>();
        var unseen = since.Where(change => !discard.Contains(change.Number)).ToList();
        var notes = unseen.Count == 0 ? [] : await new MapNoteStore(db).OfMapAsync(slug, ct);
        foreach (var change in unseen)
        {
            var before = await log.DocumentsAtAsync(slug, change.Number - 1, ct);
            var at = await log.DocumentsAtAsync(slug, change.Number, ct);
            var edits = MapDocuments.All.SelectMany(document => DocumentDiff.Between(document,
                MapChangeRead.Text(before, MapChangeRead.KindOf(document)),
                MapChangeRead.Text(at, MapChangeRead.KindOf(document)))).ToList();
            var handed = Handover.Of(edits, held is null ? null : Encoding.UTF8.GetString(held),
                (MapChangeRead.Text(before, ArtifactKind.SketchLayoutJson), MapChangeRead.Text(before, ArtifactKind.MapIntentJson)),
                (MapChangeRead.Text(at, ArtifactKind.SketchLayoutJson), MapChangeRead.Text(at, ArtifactKind.MapIntentJson)),
                applied: change.Kinds.Contains(ArtifactKind.RefinementJson));
            var said = Said(change, notes);
            findings.AddRange(handed.Select(edit => new Finding(SourceRules.UnseenChange,
                $"change {change.Number} by {said} has an edit the source has not seen, {edit.Says}", Field: "after",
                Subjects: [change.Number.ToString(CultureInfo.InvariantCulture)], Edit: edit)));
        }
        return findings.Count > 0 ? (new Refusal(409, "changes not seen", findings), []) : (null, [.. discard]);
    }

    /// <summary>Who made a change and when, what they noted on it, and the notes written at it.</summary>
    private static string Said(MapChange change, IReadOnlyList<StoredNote> notes)
    {
        var said = new StringBuilder(change.WriterName ?? "unsigned");
        if (change.TokenLabel is { } token) said.Append(" (token ").Append(token).Append(')');
        said.Append(CultureInfo.InvariantCulture, $", {DateTime.SpecifyKind(change.At, DateTimeKind.Utc):yyyy-MM-dd HH:mm}Z");
        if (change.Note is { Length: > 0 } noted) said.Append(", noted «").Append(Clip(noted)).Append('»');
        foreach (var stored in notes)
            foreach (var message in stored.Messages.Where(message => message.Change == change.Number))
                said.Append(CultureInfo.InvariantCulture, $", note {stored.Note.Id}: «{Clip(message.Body)}»");
        return said.ToString();
    }

    private static string Clip(string text)
    {
        var line = text.ReplaceLineEndings(" ").Trim();
        return line.Length <= 120 ? line : line[..119] + "…";
    }

    private static string Numbers(IEnumerable<long> numbers) =>
        string.Join(", ", numbers.Select(number => $"#{number}"));

    /// <summary>Whether a stored refinement states anything: a source that stated none keeps <c>{}</c>.</summary>
    private static bool States(byte[]? refinement)
    {
        if (refinement is null) return false;
        try { return JsonNode.Parse(refinement) is JsonObject { Count: > 0 }; }
        catch (JsonException) { return false; }
    }

    private static string? Raw(JsonNode? document) =>
        document is null || document.GetValueKind() == JsonValueKind.Null ? null : document.ToJsonString();

    private static string? NameOf(string intentJson) =>
        (JsonNode.Parse(intentJson) as JsonObject)?["meta"]?["name"] is JsonValue name && name.TryGetValue<string>(out var text)
            ? text : null;

    private static MapSourceApplied Refuse(int status, string error, params Finding[] findings) =>
        new(Refusal.At(status, error, findings));
}

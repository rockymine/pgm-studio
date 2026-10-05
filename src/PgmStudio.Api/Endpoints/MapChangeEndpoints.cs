using System.Globalization;
using System.Text;
using System.Text.Json;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Minecraft.Render;
using PgmStudio.Pgm;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>GET /api/map/{slug}/changes — every change to the map's documents, oldest first: its number, when it
/// landed, who wrote it and with which token, where its documents were built from, its note, and which
/// documents it wrote. <c>since</c> keeps the changes after a number, which is what a round asks for: what
/// landed after the change it last read. <c>?format=text</c> answers one line a change.</summary>
public sealed class MapChangesEndpoint(MapRepository repo, MapChangeLog log) : EndpointWithoutRequest<MapChangesDto>
{
    public override void Configure()
    {
        Get("/map/{slug}/changes");
        Description(b => b.Refuses(404).Reads(
            new QueryWord("since", "Keep only the changes numbered after this one. Absent lists them all.", Min: 0))
            .AlsoText());
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var since = Query<long?>("since", isRequired: false) ?? 0;
        var answer = new MapChangesDto(map.Slug,
            [.. (await log.ListAsync(map.Slug, ct)).Where(change => change.Number > since).Select(MapChangeRead.Dto)]);
        if (TextAnswer.Wanted(HttpContext))
        {
            await TextAnswer.WriteAsync(HttpContext, MapChangeRead.Text(answer), ct);
            return;
        }
        await Send.OkAsync(answer, ct);
    }
}

/// <summary>GET /api/map/{slug}/changes/{number} — the map's documents as they stood at one change: for each,
/// what the latest change at or before it wrote. A document no change had written by then is absent. 404 for a
/// change the map does not have.</summary>
public sealed class MapChangeDocumentsEndpoint(MapRepository repo, MapChangeLog log)
    : EndpointWithoutRequest<MapChangeDocumentsDto>
{
    public override void Configure()
    {
        Get("/map/{slug}/changes/{number}");
        Description(b => b.Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var number = Route<long>("number");
        if (!await MapChangeRead.HasAsync(HttpContext, log, map.Slug, number, ct)) return;
        var documents = await log.DocumentsAtAsync(map.Slug, number, ct);
        await Send.OkAsync(new MapChangeDocumentsDto(number,
            MapChangeRead.Element(documents, ArtifactKind.PlanJson),
            MapChangeRead.Element(documents, ArtifactKind.RefinementJson),
            MapChangeRead.Element(documents, ArtifactKind.SketchLayoutJson),
            MapChangeRead.Element(documents, ArtifactKind.MapIntentJson)), ct);
    }
}

/// <summary>GET /api/map/{slug}/diff — what changed between two of the map's changes: every edit taking the plan,
/// the refinement, the layout and the intent at <c>from</c> to those at <c>to</c>, each named by the path it lands on and
/// carrying the value it replaced. Unasked, it is what the latest change did.
///
/// <para><c>world=true</c> builds the board at both changes and adds the columns the two disagree on, sorted
/// into ground, surface block and structure, each as a count and its largest runs with the box to find each
/// in. <c>?format=png</c> draws those columns over both boards' ground, and <c>?format=text</c> answers the
/// edits one a line with the columns beneath. A side with no layout has no world to build, and asking for one
/// there is a 422.</para></summary>
[Queued]
public sealed class MapDiffEndpoint(MapRepository repo, MapChangeLog log) : EndpointWithoutRequest<MapDiffDto>
{
    public override void Configure()
    {
        Get("/map/{slug}/diff");
        Summary(s => s.Summary = WorldReadCatalog.Sentence("diff"));
        Description(b => b.Refuses(404, 422).AlsoPicture().AlsoText().Reads(
            new QueryWord("from", "The change to compare from. Absent is the change before `to`, and 0 is before "
                + "the map's first change, where nothing was stated.", Min: 0),
            new QueryWord("to", "The change to compare to. Absent is the map's latest change.", Min: 1),
            new QueryWord("world", "Build the board at both changes and add the columns whose ground, surface "
                + "block or structure differs. Absent compares the documents alone; a picture always builds.",
                ["true", "false"]),
            new QueryWord("scale", "For the picture, pixels a block takes, 1 to 16. Absent draws at 4, and out "
                + "of range clamps.", Min: 1, Max: 16)));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var changes = await log.ListAsync(map.Slug, ct);
        if (changes.Count == 0)
        {
            await Refusals.WriteAsync(HttpContext, 404, "no changes", [new Finding(RequestRules.NoSuchSubject,
                $"map '{map.Slug}' has no kept changes")], ct);
            return;
        }
        var to = Query<long?>("to", isRequired: false) ?? changes[^1].Number;
        if (!await MapChangeRead.HasAsync(HttpContext, log, map.Slug, to, ct, changes)) return;
        var from = Query<long?>("from", isRequired: false)
                   ?? changes.LastOrDefault(change => change.Number < to)?.Number ?? 0;
        if (from != 0 && !await MapChangeRead.HasAsync(HttpContext, log, map.Slug, from, ct, changes)) return;

        var before = from == 0 ? new Dictionary<string, byte[]>() : await log.DocumentsAtAsync(map.Slug, from, ct);
        var after = await log.DocumentsAtAsync(map.Slug, to, ct);
        var edits = MapDocuments.All.SelectMany(document => DocumentDiff.Between(document,
            MapChangeRead.Text(before, MapChangeRead.KindOf(document)),
            MapChangeRead.Text(after, MapChangeRead.KindOf(document)))).ToList();

        var picture = PngAnswer.Wanted(HttpContext);
        if (!picture && !string.Equals(Query<string?>("world", isRequired: false), "true", StringComparison.OrdinalIgnoreCase))
        {
            await Answer(new MapDiffDto(from, to, edits), map.Slug, ct);
            return;
        }

        foreach (var (number, documents) in new[] { (from, before), (to, after) })
            if (!documents.ContainsKey(ArtifactKind.SketchLayoutJson))
            {
                await Refusals.WriteAsync(HttpContext, 422, "no world to build", [new Finding(PgmStudio.Pgm.Sketch.SketchRules.NothingStored,
                    $"change {number} holds no sketch layout")], ct);
                return;
            }

        var wasBuilt = MapChangeRead.Build(before);
        var nowBuilt = MapChangeRead.Build(after);
        var world = WorldDiff.Between(wasBuilt, nowBuilt);
        if (picture)
        {
            var scale = Query<int?>("scale", isRequired: false) is { } asked ? Math.Clamp(asked, 1, 16) : 4;
            if (world.Png(wasBuilt, nowBuilt, scale) is not { } png)
            {
                await Refusals.WriteAsync(HttpContext, 422, "nothing to draw",
                    [new Finding(RequestRules.Conflict, "neither of the two layouts has any ground")], ct);
                return;
            }
            HttpContext.Response.ContentType = "image/png";
            await HttpContext.Response.Body.WriteAsync(png, ct);
            return;
        }
        await Answer(new MapDiffDto(from, to, edits, MapChangeRead.Dto(world)), map.Slug, ct);
    }

    private async Task Answer(MapDiffDto diff, string slug, CancellationToken ct)
    {
        if (TextAnswer.Wanted(HttpContext))
        {
            await TextAnswer.WriteAsync(HttpContext, MapChangeRead.Text(slug, diff), ct);
            return;
        }
        await Send.OkAsync(diff, ct);
    }
}

/// <summary>POST /api/map/{slug}/changes/{number}/restore — write the map's documents back as they stood at one
/// of its changes, as one new change, noted as the restore unless the body says otherwise. Only the documents
/// that differ are written, each through the road that stores it anywhere else, and the map row, its notes and
/// its kept pictures are left alone. A restore to what the map already holds writes nothing and answers no
/// change. 404 for a change the map does not have, 400 for a note over 1,000 characters.</summary>
public sealed class MapRestoreEndpoint(
    MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts, PlayerLookup players,
    MapChangeLog log) : Endpoint<MapRestoreRequest, MapRestoredDto>
{
    public override void Configure()
    {
        Post("/map/{slug}/changes/{number}/restore");
        Description(b => b.Refuses(400, 404));
    }

    public override async Task HandleAsync(MapRestoreRequest request, CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var number = Route<long>("number");
        var restored = await MapRestore.RunAsync(
            map, number, request.Note, repo, reader, writer, artifacts, players, log, ct);
        if (restored.Refusal is { } refusal)
        {
            await Refusals.WriteAsync(HttpContext, refusal, ct);
            return;
        }
        await Send.OkAsync(new MapRestoredDto(number, restored.Change, restored.Documents ?? []), ct);
    }
}

/// <summary>How a map's changes are read: a change as the wire states it, the documents one held, the world they
/// build, and the lines a reader with no JSON parser takes.</summary>
internal static class MapChangeRead
{
    /// <summary>The runs of one kind of changed column a diff names — the largest; the count covers the
    /// rest.</summary>
    private const int NamedRuns = 12;

    public static MapChangeDto Dto(MapChange change) => new(
        change.Number, DateTime.SpecifyKind(change.At, DateTimeKind.Utc), change.WriterName, change.WriterUuid,
        change.TokenLabel,
        change.OriginJson is { } origin ? JsonSerializer.Deserialize<ChangeOrigin>(origin, MapArtifactStore.Json) : null,
        change.Note,
        [.. MapDocuments.All.Where(word =>
            change.Kinds.Any(kind => ArtifactKind.Kept.TryGetValue(kind, out var named) && named == word))],
        change.Discarded);

    public static WorldChangesDto Dto(WorldDiff world) => new(
        Columns(world, WorldDiff.Changes.Ground), Columns(world, WorldDiff.Changes.Surface),
        Columns(world, WorldDiff.Changes.Structure));

    private static ColumnChangesDto Columns(WorldDiff world, WorldDiff.Changes kind) => new(
        world.Of(kind).Count,
        [.. world.Runs(kind).Take(NamedRuns).Select(run =>
            new CellRunDto(run.Cells, run.Box.X, run.Box.Z, run.Box.MaxX - 1, run.Box.MaxZ - 1))]);

    /// <summary>Whether the map has change <paramref name="number"/>, answering the 404 where it does not.</summary>
    public static async Task<bool> HasAsync(
        HttpContext http, MapChangeLog log, string slug, long number, CancellationToken ct,
        IReadOnlyList<MapChange>? changes = null)
    {
        changes ??= await log.ListAsync(slug, ct);
        if (changes.Any(change => change.Number == number)) return true;
        await Refusals.WriteAsync(http, 404, "no such change", [new Finding(RequestRules.NoSuchSubject,
            $"map '{slug}' has no change {number}")], ct);
        return false;
    }

    /// <summary>The artifact a document is kept as.</summary>
    public static string KindOf(string document) =>
        ArtifactKind.Kept.First(kept => kept.Value == document).Key;

    public static JsonElement? Element(IReadOnlyDictionary<string, byte[]> documents, string kind) =>
        documents.TryGetValue(kind, out var data) ? JsonSerializer.Deserialize<JsonElement>(data) : null;

    public static string? Text(IReadOnlyDictionary<string, byte[]> documents, string kind) =>
        documents.TryGetValue(kind, out var data) ? Encoding.UTF8.GetString(data) : null;

    /// <summary>The board a change's documents build — the one already built where they have been asked
    /// before.</summary>
    public static BuiltWorld Build(IReadOnlyDictionary<string, byte[]> documents) => BuiltWorlds.Of(
        Text(documents, ArtifactKind.SketchLayoutJson)!,
        Text(documents, ArtifactKind.MapIntentJson) is { } intent ? IntentWrite.Stated(intent) ?? new() : new());

    public static string Text(MapChangesDto changes)
    {
        var lines = new StringBuilder($"changes {changes.Slug}: {changes.Changes.Count}\n");
        foreach (var change in changes.Changes)
        {
            lines.Append(CultureInfo.InvariantCulture, $"#{change.Number}  {change.At:yyyy-MM-dd HH:mm}Z  ");
            lines.Append(change.Writer ?? "unsigned");
            if (change.Token is { } token) lines.Append($" (token {token})");
            lines.Append("  ").Append(string.Join(", ", change.Documents));
            if (change.Origin is { } origin)
                lines.Append("  ").Append(origin.Repo).Append('@').Append(origin.Commit)
                     .Append(origin.Dirty == true ? "+dirty" : "").Append(' ').Append(origin.Path);
            if (change.Discarded is { Count: > 0 } discarded)
                lines.Append("  dropped ").Append(string.Join(", ", discarded.Select(number => $"#{number}")));
            if (change.Note is { Length: > 0 } note) lines.Append("  — ").Append(note.ReplaceLineEndings(" "));
            lines.Append('\n');
        }
        return lines.ToString();
    }

    public static string Text(string slug, MapDiffDto diff)
    {
        var lines = new StringBuilder(
            $"diff {slug} #{diff.From} → #{diff.To}: {diff.Edits.Count} edit{(diff.Edits.Count == 1 ? "" : "s")}\n");
        foreach (var edit in diff.Edits)
            lines.Append(CultureInfo.InvariantCulture, $"{edit.Document,-7}{edit.Op,-7}{edit.Path}  {edit.Says}\n");
        if (diff.World is not { } world) return lines.ToString();

        var total = world.Ground.Columns + world.Surface.Columns + world.Structure.Columns;
        lines.Append(CultureInfo.InvariantCulture,
            $"world: {total} columns changed — ground {world.Ground.Columns}, surface {world.Surface.Columns}, "
            + $"structure {world.Structure.Columns}\n");
        foreach (var (name, kind) in (ReadOnlySpan<(string, ColumnChangesDto)>)[
                     ("ground", world.Ground), ("surface", world.Surface), ("structure", world.Structure)])
            foreach (var run in kind.Runs)
                lines.Append(CultureInfo.InvariantCulture,
                    $"  {name,-10}{run.Cells,6} column{(run.Cells == 1 ? " " : "s")}  x {run.MinX}..{run.MaxX}  z {run.MinZ}..{run.MaxZ}\n");
        return lines.ToString();
    }
}

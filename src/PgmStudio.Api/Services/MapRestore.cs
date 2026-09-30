using System.Text;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>What writing a map's documents back came to: a refusal, or the change it landed as and the documents
/// it wrote. <para><b>Change</b> — Null where every document already stood as it did at the change restored, and
/// nothing was written.</para></summary>
public sealed record MapRestored(Refusal? Refusal, long? Change = null, IReadOnlyList<string>? Documents = null);

/// <summary>
/// A map's documents written back as they stood at one of its changes, as one new change.
///
/// <para>Only a document that differs from what the map holds is written, each through the road that stores it
/// anywhere else: the intent is stored and projected into the map document, the plan and the layout are
/// stored, and a finished board's ground is read again from the layout the next time anything asks for it
/// (<see cref="SketchFinish.RefreshAsync"/>). The map row, its notes and the pictures kept of it are not
/// touched, which is what separates a restore from a reload. A document no change had written by then is left
/// as it stands.</para>
///
/// <para>What the restore can be refused for is decided before anything is written, and the intent — the one
/// write that can still refuse — goes first, so a refused restore writes nothing.</para>
/// </summary>
public static class MapRestore
{
    public static async Task<MapRestored> RunAsync(
        MapRow map, long number, string? note, MapRepository repo, MapReader reader, MapWriter writer,
        MapArtifactStore artifacts, PlayerLookup players, MapChangeLog log, CancellationToken ct)
    {
        if (!(await log.ListAsync(map.Slug, ct)).Any(change => change.Number == number))
            return new(Refusal.At(404, "no such change", new Finding(RequestRules.NoSuchSubject,
                $"'{map.Slug}' has no change {number} — GET /api/map/{map.Slug}/changes lists the ones it has")));
        if (note is { Length: > MapChangeLog.NoteLength })
            return new(Refusal.At(400, "note too long", new Finding(RequestRules.Unreadable,
                $"the note is {note.Length} characters and a change keeps at most {MapChangeLog.NoteLength}",
                Field: "note")));

        var writes = new Dictionary<string, byte[]>();
        foreach (var (kind, data) in await log.DocumentsAtAsync(map.Slug, number, ct))
            if (await artifacts.LoadAsync(map.Id, kind, ct) is not { } held || !held.AsSpan().SequenceEqual(data))
                writes[kind] = data;
        if (writes.Count == 0) return new(null, null, []);

        if (writes.TryGetValue(ArtifactKind.SketchLayoutJson, out var layout))
        {
            var styles = SketchMaterialGate.Check(Encoding.UTF8.GetString(layout));
            if (styles.Refuses) return new(new Refusal(400, "invalid style or theme", [.. styles.Refusals]));
        }

        artifacts.Stamp = artifacts.Stamp with { Note = note ?? $"restores change {number}" };
        if (writes.TryGetValue(ArtifactKind.MapIntentJson, out var intent))
        {
            var applied = await IntentWrite.StoreAndProjectAsync(
                repo, reader, writer, artifacts, players, map.Slug, map.Id, Encoding.UTF8.GetString(intent),
                expected: null, ct);
            if (applied.Refusal is { } refused) return new(refused);
        }
        foreach (var kind in (string[])[ArtifactKind.PlanJson, ArtifactKind.SketchLayoutJson])
            if (writes.TryGetValue(kind, out var data)) await artifacts.SaveAsync(map.Id, kind, data, ct);

        var landed = (await log.ListAsync(map.Slug, ct))[^1].Number;
        return new(null, landed, [.. MapDocuments.All.Where(word => writes.Keys.Any(kind => ArtifactKind.Kept[kind] == word))]);
    }
}
